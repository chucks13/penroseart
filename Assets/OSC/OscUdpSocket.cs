// Copyright © 2026 Hunter Luisi. All rights reserved.
// Origin: RaveSystem.Osc; adapted for PenroseArt's Unity runtime.

#nullable enable
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Buffers;
using System.Buffers.Binary;

namespace RaveSystem.Osc {

/// <summary>
///     Callback invoked by <see cref="OscUdpSocket" /> for each received OSC packet.
/// </summary>
/// <param name="packet">The received bytes (slice of the socket's internal buffer; valid only for the duration of the callback).</param>
/// <param name="sender">The sending host's <see cref="SocketAddress" /> (mutated in-place per receive; copy if you need to retain it past the callback).</param>
public delegate void OscUdpPacketHandler(ReadOnlySpan<byte> packet, SocketAddress sender);

/// <summary>
///     Bidirectional UDP transport for OSC packets. Binds a single <see cref="Socket" /> to a
///     local endpoint, runs an asynchronous receive loop that invokes a caller-supplied
///     <see cref="OscUdpPacketHandler" /> for each datagram, and exposes
///     <see cref="Send(ReadOnlySpan{byte}, SocketAddress)" /> for replying through the same
///     bound port. Allocates zero per-packet on the happy path in both directions: a single
///     fixed-size buffer and a single <see cref="SocketAddress" /> are reused for every receive,
///     and sends use the documented allocation-free
///     <see cref="Socket.SendTo(ReadOnlySpan{byte}, SocketFlags, SocketAddress)" /> overload
///     with caller-owned buffers.
/// </summary>
/// <remarks>
///     Threading: the callback runs on whatever thread the I/O completion lands on (typically
///     a thread-pool worker). Callers that mutate UI or thread-affine state must marshal back
///     to their own thread.
///     <para>
///     The packet buffer is sized for the maximum UDP datagram payload (65,535 bytes by spec,
///     rounded up to 65,536). The same buffer is reused across receives, so the
///     <c>packet</c> span passed to the handler is only valid for the duration
///     of the callback.
///     </para>
///     <para>
///     Lifecycle: construct, optionally subscribe via <see cref="PacketReceived" />, call
///     <see cref="Start" /> to begin the receive loop, and <see cref="Dispose" /> to stop.
///     <see cref="Dispose" /> is idempotent. Use
///     <see cref="Send(ReadOnlySpan{byte}, SocketAddress)" /> to reply on the bound port; this
///     is the conventional UDP request/reply shape so clients observe replies originating from
///     the port they queried.
///     </para>
/// </remarks>
public sealed class OscUdpSocket : IDisposable {
    /// <summary>The maximum UDP datagram payload size (theoretical max = 65,507 bytes; we round to 65,536).</summary>
    public const int MaxDatagramSize = 65_536;

    private readonly Socket _socket;
    private readonly byte[] _buffer = new byte[MaxDatagramSize];
    private readonly CancellationTokenSource _cts = new CancellationTokenSource();
    private Task? _receiveLoopTask;
    private bool _disposed;

    /// <summary>The local endpoint the socket is bound to (after construction).</summary>
    public IPEndPoint LocalEndPoint { get; }

    /// <summary>Fired for each received packet. Subscribe before calling <see cref="Start" />.</summary>
    public event OscUdpPacketHandler? PacketReceived;

    /// <summary>
    ///     The receive-loop task. Await this to observe handler or socket failures after
    ///     <see cref="Start" />; before start, it is already completed.
    /// </summary>
    public Task Completion => _receiveLoopTask ?? Task.CompletedTask;

    /// <summary>
    ///     Creates a UDP socket bound to <paramref name="localEndPoint" />. Pass
    ///     <c>new IPEndPoint(IPAddress.Any, port)</c> to listen on all interfaces, or
    ///     <c>port = 0</c> to let the kernel assign one (read back via <see cref="LocalEndPoint" />).
    /// </summary>
    public OscUdpSocket(IPEndPoint localEndPoint) {
        if (localEndPoint == null) { throw new ArgumentNullException(nameof(localEndPoint)); }

        _socket = new Socket(localEndPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
        _socket.Bind(localEndPoint);
        LocalEndPoint = (IPEndPoint)_socket.LocalEndPoint!;
    }

    /// <summary>
    ///     Sends one OSC packet to <paramref name="destination" /> through the bound socket. The
    ///     reply originates from <see cref="LocalEndPoint" />, giving callers the conventional
    ///     UDP request/reply shape (clients see replies coming from the port they queried).
    /// </summary>
    /// <param name="packet">The encoded OSC packet bytes (typically the output of <see cref="OscWriter.Finish" /> or <see cref="OscBundleWriter.Finish" />).</param>
    /// <param name="destination">The destination <see cref="SocketAddress" /> (typically the <c>sender</c> argument from a <see cref="PacketReceived" /> callback).</param>
    /// <returns>The number of bytes sent (always equal to <c>packet.Length</c> on success for UDP datagrams).</returns>
    /// <exception cref="ArgumentNullException"><paramref name="destination" /> is <c>null</c>.</exception>
    /// <exception cref="ObjectDisposedException">If the socket has been disposed.</exception>
    /// <exception cref="SocketException">Propagated from the underlying socket on transport failure.</exception>
    /// <remarks>Allocation-free per call: caller-owned span, caller-owned <see cref="SocketAddress" />, and the .NET 8+ <see cref="Socket.SendTo(ReadOnlySpan{byte}, SocketFlags, SocketAddress)" /> overload.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Send(ReadOnlySpan<byte> packet, SocketAddress destination) {
        if (destination == null) { throw new ArgumentNullException(nameof(destination)); }
        var endPoint = LocalEndPoint.Create(destination);
        return _socket.SendTo(packet.ToArray(), SocketFlags.None, endPoint);
    }

    /// <summary>Starts the asynchronous receive loop. Calling more than once is a no-op.</summary>
    public void Start() {
        if (_disposed) { throw new ObjectDisposedException(nameof(OscUdpSocket)); }
        if (_receiveLoopTask is not null) {
            return;
        }
        _receiveLoopTask = Task.Run((Action)ReceiveLoop);
    }

    private void ReceiveLoop() {
        try {
            while (!_cts.IsCancellationRequested) {
                EndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                var received = _socket.ReceiveFrom(_buffer, ref sender);
                PacketReceived?.Invoke(_buffer.AsSpan(0, received), sender.Serialize());
            }
        } catch (SocketException) when (_cts.IsCancellationRequested) {
            // Socket closed during shutdown.
        } catch (ObjectDisposedException) {
            // Socket disposed during shutdown.
        }
    }

    /// <inheritdoc />
    public void Dispose() {
        if (_disposed) {
            return;
        }
        _disposed = true;
        _cts.Cancel();
        _socket.Dispose();
        try {
            _receiveLoopTask?.Wait(TimeSpan.FromSeconds(1));
        } finally {
            _cts.Dispose();
        }
    }
}

}
