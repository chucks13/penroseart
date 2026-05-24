// Copyright © 2026 Hunter Luisi. All rights reserved.
// Origin: RaveSystem.Osc; adapted for PenroseArt's Unity runtime.

#nullable enable
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Threading;

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Buffers;
using System.Buffers.Binary;

namespace RaveSystem.Osc {

/// <summary>
///     The OSC handler delegate. Invoked once per registered handler whose literal address
///     matches the incoming packet's address pattern.
/// </summary>
/// <param name="address">The matched address as raw ASCII bytes (slice of the packet buffer; no allocation).</param>
/// <param name="reader">A fresh <see cref="OscReader" /> positioned past the address; ready for <see cref="OscReader.MoveNext" />.</param>
/// <param name="bundleTimeTag">The enclosing bundle's NTP timetag, or <see cref="OscTimeTag.Immediately" /> if the message arrived outside a bundle.</param>
public delegate void OscMessageHandler(ReadOnlySpan<byte> address, ref OscReader reader, OscTimeTag bundleTimeTag);

/// <summary>
///     OSC receiver-side dispatcher. Maintains a registration list of (literal address,
///     handler) pairs and routes incoming packets through <see cref="OscAddressPattern" />
///     to every handler whose address matches the packet's pattern. Bundles are decomposed
///     recursively; each contained message is dispatched independently with the bundle's
///     timetag passed through. Future-dated bundles are scheduled and dispatched when due.
/// </summary>
/// <remarks>
///     Concurrency: <see cref="Register" /> and <see cref="Unregister" /> serialize behind an
///     internal lock; <see cref="Dispatch" /> reads a volatile snapshot of the registration
///     array without locking and iterates allocation-free. A registration change concurrent
///     with a dispatch may or may not be visible to that dispatch, but the dispatcher will
///     never observe a torn array.
///     <para>
///     Exception policy: <see cref="Dispatch" /> propagates <see cref="OscFormatException" />,
///     <see cref="OscAddressException" />, and immediate handler exceptions. Future scheduled
///     dispatch failures are reported via <see cref="DispatchFailed" /> or, if no handler is
///     registered, rethrown from <see cref="Dispose" /> as an <see cref="AggregateException" />.
///     </para>
/// </remarks>
public sealed class OscDispatcher : IDisposable {
    private static readonly TimeSpan s_maxTimerDueTime = TimeSpan.FromMilliseconds(int.MaxValue);

    private readonly object _modifyLock = new object();
    private readonly object _scheduleLock = new object();
    private readonly List<ScheduledDispatch> _scheduledDispatches = new List<ScheduledDispatch>();
    private readonly List<Exception> _scheduledFailures = new List<Exception>();
    private volatile Registration[] _registrations = Array.Empty<Registration>();
    private bool _disposed;

    /// <summary>Creates a dispatcher using the system UTC clock for bundle scheduling.</summary>
    public OscDispatcher() {
    }

    /// <summary>
    ///     Raised when a future scheduled bundle fails during background dispatch. Subscribe to
    ///     observe handler failures without waiting until disposal.
    /// </summary>
    public event Action<Exception>? DispatchFailed;

    /// <summary>The number of currently registered handlers.</summary>
    public int RegistrationCount => _registrations.Length;

    /// <summary>Registers a handler against the literal OSC address <paramref name="address" />.</summary>
    /// <param name="address">A literal OSC address (no wildcards). Validated via <see cref="OscAddress" />.</param>
    /// <param name="handler">The handler to invoke when an incoming pattern matches this address.</param>
    /// <returns>A token that can be passed to <see cref="Unregister" /> to remove this registration.</returns>
    /// <exception cref="OscAddressException">Thrown if <paramref name="address" /> is not a valid literal address.</exception>
    public OscRegistrationToken Register(string address, OscMessageHandler handler) {
        if (address == null) { throw new ArgumentNullException(nameof(address)); }
        if (handler == null) { throw new ArgumentNullException(nameof(handler)); }
        OscAddress.ValidateLiteralAddress(address);

        var addressBytes = Encoding.ASCII.GetBytes(address);
        var token = new OscRegistrationToken(Guid.NewGuid());
        var registration = new Registration(token, addressBytes, handler);

        lock (_modifyLock) {
            var current = _registrations;
            var next = new Registration[current.Length + 1];
            Array.Copy(current, next, current.Length);
            next[^1] = registration;
            _registrations = next;
        }

        return token;
    }

    /// <summary>Removes the registration identified by <paramref name="token" />. Returns <see langword="true" /> if a registration was removed.</summary>
    public bool Unregister(OscRegistrationToken token) {
        lock (_modifyLock) {
            var current = _registrations;
            var index = -1;
            for (var i = 0; i < current.Length; i++) {
                if (current[i].Token == token) {
                    index = i;
                    break;
                }
            }
            if (index < 0) {
                return false;
            }

            var next = new Registration[current.Length - 1];
            Array.Copy(current, 0, next, 0, index);
            Array.Copy(current, index + 1, next, index, current.Length - index - 1);
            _registrations = next;
            return true;
        }
    }

    /// <summary>Removes all registrations.</summary>
    public void Clear() {
        lock (_modifyLock) {
            _registrations = Array.Empty<Registration>();
        }
    }

    /// <summary>Dispatches an OSC packet (message or bundle) to all matching handlers.</summary>
    /// <param name="packet">The raw OSC packet bytes.</param>
    /// <returns>The number of handler invocations performed (handlers × matching messages).</returns>
    public int Dispatch(ReadOnlySpan<byte> packet) {
        if (_disposed) { throw new ObjectDisposedException(nameof(OscDispatcher)); }
        return DispatchPacket(packet, OscTimeTag.Immediately);
    }

    private int DispatchPacket(ReadOnlySpan<byte> packet, OscTimeTag enclosingTimeTag) {
        var kind = OscPacket.Classify(packet);
        return kind == OscPacketKind.Bundle
            ? DispatchBundle(packet)
            : DispatchMessage(packet, enclosingTimeTag);
    }

    private int DispatchBundle(ReadOnlySpan<byte> bundle) {
        var bundleReader = new OscBundleReader(bundle);
        var timeTag = bundleReader.TimeTag;
        if (ShouldSchedule(timeTag)) {
            ScheduleBundle(bundle, timeTag);
            return 0;
        }

        var dispatched = 0;
        while (bundleReader.HasMoreElements) {
            var element = bundleReader.ReadNextElement();
            dispatched += DispatchPacket(element, timeTag);
        }
        return dispatched;
    }

    private bool ShouldSchedule(OscTimeTag timeTag) {
        if (timeTag.IsImmediately) {
            return false;
        }

        return timeTag.ToDateTimeOffset() > DateTimeOffset.UtcNow;
    }

    private void ScheduleBundle(ReadOnlySpan<byte> bundle, OscTimeTag timeTag) {
        var dueTime = timeTag.ToDateTimeOffset() - DateTimeOffset.UtcNow;
        if (dueTime > s_maxTimerDueTime) {
            dueTime = s_maxTimerDueTime;
        }
        var scheduled = new ScheduledDispatch(this, bundle.ToArray());
        scheduled.Timer = new System.Threading.Timer(
            state => ((ScheduledDispatch)state!).Dispatcher.FireScheduled((ScheduledDispatch)state!),
            scheduled,
            Timeout.InfiniteTimeSpan,
            Timeout.InfiniteTimeSpan);

        lock (_scheduleLock) {
            if (_disposed) { throw new ObjectDisposedException(nameof(OscDispatcher)); }
            _scheduledDispatches.Add(scheduled);
            scheduled.Timer.Change(dueTime, Timeout.InfiniteTimeSpan);
        }
    }

    private void FireScheduled(ScheduledDispatch scheduled) {
        lock (_scheduleLock) {
            _scheduledDispatches.Remove(scheduled);
        }

        try {
            scheduled.Timer?.Dispose();
            Dispatch(scheduled.Packet);
        } catch (Exception ex) {
            RecordScheduledFailure(ex);
        }
    }

    private void RecordScheduledFailure(Exception exception) {
        var handler = DispatchFailed;
        if (handler is not null) {
            try {
                handler(exception);
                return;
            } catch (Exception eventException) {
                StoreScheduledFailure(new AggregateException(
                    "Scheduled OSC dispatch failed, and the DispatchFailed handler also threw",
                    exception,
                    eventException));
                return;
            }
        }

        StoreScheduledFailure(exception);
    }

    private void StoreScheduledFailure(Exception exception) {
        lock (_scheduleLock) {
            _scheduledFailures.Add(exception);
        }
    }

    private int DispatchMessage(ReadOnlySpan<byte> message, OscTimeTag bundleTimeTag) {
        var probe = new OscReader(message);
        var addressBytes = probe.ReadAddress();

        var snapshot = _registrations;
        var dispatched = 0;
        foreach (var reg in snapshot) {
            if (OscAddressPattern.Matches(addressBytes, reg.AddressBytes)) {
                var freshReader = new OscReader(message);
                freshReader.ReadAddress();
                reg.Handler(addressBytes, ref freshReader, bundleTimeTag);
                dispatched++;
            }
        }
        return dispatched;
    }

    /// <inheritdoc />
    [SuppressMessage(
        "Major Bug",
        "S3877:Exceptions should not be thrown from unexpected methods",
        Justification = "Documented contract on the class: scheduled-dispatch failures are surfaced via the DispatchFailed event, or, if no subscriber is attached, rethrown from Dispose as an AggregateException so they cannot be silently lost. Removing the throw would silently change the dispatcher's observability contract.")]
    public void Dispose() {
        ScheduledDispatch[] scheduled;
        Exception[] failures;
        lock (_scheduleLock) {
            if (_disposed) {
                return;
            }
            _disposed = true;
            scheduled = _scheduledDispatches.ToArray();
            failures = _scheduledFailures.ToArray();
            _scheduledDispatches.Clear();
            _scheduledFailures.Clear();
        }

        foreach (var item in scheduled) {
            item.Timer?.Dispose();
        }

        if (failures.Length > 0) {
            throw new AggregateException("One or more scheduled OSC dispatches failed", failures);
        }
    }

    private sealed class Registration {
        public Registration(OscRegistrationToken token, byte[] addressBytes, OscMessageHandler handler) {
            Token = token;
            AddressBytes = addressBytes;
            Handler = handler;
        }

        public OscRegistrationToken Token { get; }

        public byte[] AddressBytes { get; }

        public OscMessageHandler Handler { get; }
    }

    private sealed class ScheduledDispatch {
        public ScheduledDispatch(OscDispatcher dispatcher, byte[] packet) {
            Dispatcher = dispatcher;
            Packet = packet;
        }

        public OscDispatcher Dispatcher { get; }

        public byte[] Packet { get; }

        public System.Threading.Timer? Timer { get; set; }
    }
}

/// <summary>An opaque token identifying a single <see cref="OscDispatcher" /> registration.</summary>
public readonly struct OscRegistrationToken : IEquatable<OscRegistrationToken> {
    private readonly Guid _id;

    internal OscRegistrationToken(Guid id) {
        _id = id;
    }

    /// <inheritdoc />
    public bool Equals(OscRegistrationToken other) => _id == other._id;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is OscRegistrationToken other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => _id.GetHashCode();

    /// <summary>Equality operator.</summary>
    public static bool operator ==(OscRegistrationToken left, OscRegistrationToken right) => left.Equals(right);

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(OscRegistrationToken left, OscRegistrationToken right) => !left.Equals(right);
}

}
