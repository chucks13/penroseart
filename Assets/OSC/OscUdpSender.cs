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
///     UDP transport for outbound OSC packets. Wraps a single <see cref="Socket" /> bound to
///     the IPv4 datagram protocol and caches a <see cref="SocketAddress" /> for the destination
///     so each call to <see cref="Send" /> hits the allocation-free
///     <see cref="Socket.SendTo(ReadOnlySpan{byte}, SocketFlags, SocketAddress)" /> overload
///     introduced in .NET 8.
/// </summary>
/// <remarks>
///     Construction options:
///     <list type="bullet">
///         <item><see cref="OscUdpSender(IPEndPoint)" /> — sends to a fixed remote endpoint.</item>
///         <item><see cref="OscUdpSender(IPEndPoint, bool)" /> — same, but configures <c>SO_BROADCAST</c> when <c>broadcast</c> is true.</item>
///     </list>
///     <para>
///     Allocation profile: zero per-send allocations on the happy path. The
///     <see cref="SocketAddress" /> is materialized once in the constructor; the buffer is
///     supplied by the caller as a <see cref="ReadOnlySpan{T}" />.
///     </para>
///     <para>
///     The sender does not bind a local port unless the caller passes one. Callers who want
///     to receive responses on a specific port (or to do request/reply on the same bound port)
///     should use <see cref="OscUdpSocket" /> instead.
///     </para>
/// </remarks>
public sealed class OscUdpSender : IDisposable {
    private readonly Socket _socket;
    private bool _disposed;

    /// <summary>The destination endpoint the sender writes to.</summary>
    public IPEndPoint RemoteEndPoint { get; }

    /// <summary>True if the sender is configured for UDP broadcast (<c>SO_BROADCAST</c>).</summary>
    public bool Broadcast { get; }

    /// <summary>Creates a UDP sender targeting <paramref name="remoteEndPoint" /> with broadcast disabled.</summary>
    public OscUdpSender(IPEndPoint remoteEndPoint) : this(remoteEndPoint, broadcast: false) { }

    /// <summary>
    ///     Creates a UDP sender targeting <paramref name="remoteEndPoint" />, optionally enabling
    ///     UDP broadcast (<c>SO_BROADCAST</c>) so packets sent to a broadcast address (e.g.
    ///     <c>255.255.255.255</c> or a subnet broadcast) reach every host.
    /// </summary>
    public OscUdpSender(IPEndPoint remoteEndPoint, bool broadcast) {
        if (remoteEndPoint == null) { throw new ArgumentNullException(nameof(remoteEndPoint)); }

        RemoteEndPoint = remoteEndPoint;
        Broadcast = broadcast;

        _socket = new Socket(remoteEndPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
        if (broadcast) {
            _socket.EnableBroadcast = true;
        }
    }

    /// <summary>Sends one OSC packet (message or bundle) to the cached remote endpoint.</summary>
    /// <param name="packet">The encoded OSC packet bytes (typically the output of <see cref="OscWriter.Finish" /> or <see cref="OscBundleWriter.Finish" />).</param>
    /// <returns>The number of bytes sent (always equal to <c>packet.Length</c> on success for UDP datagrams).</returns>
    /// <exception cref="ObjectDisposedException">If the sender has been disposed.</exception>
    /// <exception cref="SocketException">Propagated from the underlying socket on transport failure.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Send(ReadOnlySpan<byte> packet) {
        if (_disposed) { throw new ObjectDisposedException(nameof(OscUdpSender)); }
        return _socket.SendTo(packet.ToArray(), SocketFlags.None, RemoteEndPoint);
    }

    /// <inheritdoc />
    public void Dispose() {
        if (_disposed) {
            return;
        }
        _disposed = true;
        _socket.Dispose();
    }
}

}
