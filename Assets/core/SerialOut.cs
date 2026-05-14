using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

public class SerialOut
{
    private class S2MiniBoard
    {
        public SerialPort Port;
        public string PortName;
        public Thread BoardThread;
        public AutoResetEvent FrameSignal = new AutoResetEvent(false);
        public List<PixelSegment> Segments = new List<PixelSegment>();
        public byte[] ReusableBuffer = new byte[4096]; // Increased for full-frame consolidation
        public int TotalPixels = 0;
        public bool IsReady = false;

        public struct PixelSegment
        {
            public int StartIndex;
            public int Count;
        }
    }

    private List<S2MiniBoard> activeBoards = new List<S2MiniBoard>();
    private HashSet<string> ignoredPorts = new HashSet<string>();
    private HashSet<string> connectingPorts = new HashSet<string>();
    private int targetBaudRate;
    private float lastDiscoveryTime = 0f;
    
    private bool threadsRunning = false;
    
    private Color[] simulationFrameCopy = new Color[Penrose.Total];
    private byte globalLevel = 0;

    private string[] lastScannedPorts = new string[0];
    private const float DiscoveryInterval = 2.0f; // Scan for new USB devices every 2 seconds

    private const int MAX_PIXELS_PER_SEGMENT = 300; // Sanity limit for buffer allocation
    private const byte CMD_QUERY = 0x3F; // '?'
    private const byte CMD_DATA  = 0x44; // 'D'
    private const byte CMD_SYNC  = 0x53; // 'S'
    private const byte ACK_BYTE  = 0x06;
    private const byte NACK_BYTE = 0x15;

    public void Init(int baudRate)
    {
        // 2,000,000 baud is required for 900 pixels @ 60fps (~1.6Mbps raw data)
        this.targetBaudRate = 2000000; 
        threadsRunning = true;
        DiscoverBoards();
    }

    public void ResetDiscovery()
    {
        ignoredPorts.Clear();
        connectingPorts.Clear();
        Debug.Log("[SerialOut] Discovery reset. Re-scanning all ports...");
    }

    private void DiscoverBoards()
    {
        string[] ports = SerialPort.GetPortNames();
        lastScannedPorts = ports;

        // Cleanup ignored ports that are no longer physically present so they can be retried if replugged
        ignoredPorts.RemoveWhere(p => !ports.Contains(p));

        // 1. Remove disconnected or failed boards
        for (int i = activeBoards.Count - 1; i >= 0; i--)
        {
            bool stillPresent = ports.Contains(activeBoards[i].PortName);
            if (!stillPresent || !activeBoards[i].IsReady)
            {
                Debug.Log($"[SerialOut] Removing board {activeBoards[i].PortName} (Present: {stillPresent}, Ready: {activeBoards[i].IsReady})");
                activeBoards[i].FrameSignal.Set(); // Wake thread to exit
                if (activeBoards[i].Port.IsOpen)
                {
                    activeBoards[i].Port.Close();
                    activeBoards[i].Port.Dispose();
                }
                activeBoards.RemoveAt(i);
            }
        }

        // 2. Add new boards
        foreach (string portName in ports)
        {
            if (!activeBoards.Any(b => b.PortName == portName) && 
                !ignoredPorts.Contains(portName) && 
                !connectingPorts.Contains(portName))
            {
                // Fire and forget connection task to avoid blocking the render thread
                _ = TryConnectBoardAsync(portName);
            }
        }
    }

    private async Task TryConnectBoardAsync(string portName)
    {
        connectingPorts.Add(portName);
        SerialPort sp = null;
        try
        {
            sp = new SerialPort(portName, targetBaudRate);
            sp.ReadTimeout = 500; 
            sp.WriteTimeout = 1000;
            sp.WriteBufferSize = 65536; // Large buffer to handle USB burst jitter

            // ESP32-S2 CDC often requires DTR/RTS to be set to receive data properly
            sp.DtrEnable = true;
            sp.RtsEnable = true;

            Debug.Log($"[SerialOut] Attempting to open {portName}...");
            sp.Open();

            Debug.Log($"[SerialOut] {portName} opened. Waiting 2s for ESP32 bootloader...");
            await Task.Delay(2000); 
            sp.DiscardInBuffer();

            Debug.Log($"[SerialOut] {portName}: Sending Handshake Query '?'...");
            sp.Write(new byte[] { CMD_QUERY }, 0, 1);

            byte[] header = new byte[1];
            int bytesRead = await Task.Run(() => sp.Read(header, 0, 1));
            if (bytesRead == 0) 
            {
                // One last try to discard any lingering boot noise
                sp.DiscardInBuffer();
                throw new Exception("Handshake timeout: No response byte received.");
            }
            int numSegments = header[0];
            Debug.Log($"[SerialOut] {portName}: Handshake response received. {numSegments} segments reported.");

            S2MiniBoard board = new S2MiniBoard { 
                Port = sp, 
                PortName = portName
            };

            for (int i = 0; i < numSegments; i++)
            {
                byte[] segData = new byte[4];
                int read = 0;
                while (read < 4) 
                {
                    int r = await Task.Run(() => sp.Read(segData, read, 4 - read));
                    if (r == 0) throw new Exception($"Timeout reading segment {i} definition.");
                    read += r;
                }

                int start = (segData[0] << 8) | segData[1];
                int count = (segData[2] << 8) | segData[3];
                Debug.Log($"[SerialOut] {portName}: Segment {i} mapped to Pixels {start}..{start + count - 1}");
                board.Segments.Add(new S2MiniBoard.PixelSegment { StartIndex = start, Count = count });
            }

            // Total the pixels to size the bulk transfer buffer correctly
            board.TotalPixels = board.Segments.Sum(s => s.Count);
            board.ReusableBuffer = new byte[1 + (board.TotalPixels * 3)]; // CMD_DATA + Pixels
            
            board.IsReady = true;
            
            // Spawn dedicated I/O thread for this specific board
            board.BoardThread = new Thread(() => BoardIOThreadLoop(board));
            board.BoardThread.IsBackground = true;
            board.BoardThread.Start();

            activeBoards.Add(board);
            Debug.Log($"[SerialOut] Connected S2 Mini on {portName} handling {numSegments} segments.");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SerialOut] Failed to initialize port {portName}: {e.Message}");
            ignoredPorts.Add(portName);
            if (sp != null)
            {
                if (sp.IsOpen) sp.Close();
                sp.Dispose();
            }
        }
        finally
        {
            connectingPorts.Remove(portName);
        }
    }

    private bool AttemptRecovery(S2MiniBoard board)
    {
        try
        {
            board.Port.DiscardInBuffer();
            board.Port.DiscardOutBuffer();
            board.Port.Write(new byte[] { CMD_SYNC }, 0, 1);
            
            board.Port.ReadTimeout = 600; // Wait for the S2 Mini to flush and respond
            int response = board.Port.ReadByte();
            // Any standard response (ACK or NACK) means the command loop is alive
            return (response == ACK_BYTE || response == NACK_BYTE);
        }
        catch { return false; }
    }

    private void BoardIOThreadLoop(S2MiniBoard board)
    {
        while (threadsRunning && board.IsReady)
        {
            board.FrameSignal.WaitOne();
            if (!threadsRunning || !board.IsReady) break;

            try {
                // Pack the buffer for this specific board's segments on the background thread
                int p = 0;
                board.ReusableBuffer[p++] = CMD_DATA;
                foreach (var seg in board.Segments)
                {
                    for (int i = 0; i < seg.Count; i++)
                    {
                        int globalIdx = seg.StartIndex + i;
                        if (globalIdx < simulationFrameCopy.Length) {
                            Color c = simulationFrameCopy[globalIdx];
                            // Apply brightness level and pack into bytes
                            // Explicitly converting here on the background thread
                            board.ReusableBuffer[p++] = (byte)(c.r * globalLevel);
                            board.ReusableBuffer[p++] = (byte)(c.g * globalLevel);
                            board.ReusableBuffer[p++] = (byte)(c.b * globalLevel);
                        } else {
                            board.ReusableBuffer[p++] = 0; board.ReusableBuffer[p++] = 0; board.ReusableBuffer[p++] = 0;
                        }
                    }
                }
                // Use the underlying stream for lower overhead in some Mono versions
                board.Port.BaseStream.Write(board.ReusableBuffer, 0, board.ReusableBuffer.Length);
            } catch { board.IsReady = false; }
        }
    }

    public void send(Color[] data, byte level)
    {
        // Handle Hot-Plugging Discovery - Only scan if no boards are active or every 5s
        // SerialPort.GetPortNames() is an extremely expensive blocking call.
        float interval = activeBoards.Count > 0 ? 5.0f : DiscoveryInterval;
        if (Time.time - lastDiscoveryTime > interval)
        {
            DiscoverBoards();
            lastDiscoveryTime = Time.time;
        }

        if (activeBoards.Count == 0) return;

        // 1. Snapshot the simulation data for the IO thread
        globalLevel = level;
        int pixelsToCopy = Math.Min(data.Length, simulationFrameCopy.Length);
        // Use a high-speed array copy instead of a per-element loop
        Array.Copy(data, simulationFrameCopy, pixelsToCopy);

        // 2. Signal threads to pack their specific segments and transmit
        foreach (var board in activeBoards)
        {
            if (board.IsReady) board.FrameSignal.Set();
        }
    }

    /// <summary>
    /// Returns a debug string with information about active and connecting serial ports.
    /// </summary>
    public string GetDebugInfo()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        // Show all COM ports currently reported by the OS
        sb.Append("\nOS Ports: ").Append(lastScannedPorts.Length > 0 ? string.Join(", ", lastScannedPorts) : "None");

        if (activeBoards.Count > 0)
        {
            sb.Append("\nActive Penrose Boards:");
            foreach (var board in activeBoards)
            {
                // Show specific pixel ranges for each segment on this board
                string ranges = string.Join(", ", board.Segments.Select(s => $"[{s.StartIndex}-{s.StartIndex + s.Count - 1}]"));
                sb.Append($"\n  - {board.PortName}: {ranges}");
            }
        }

        if (connectingPorts.Count > 0) {
            sb.Append($"\nConnecting: {string.Join(", ", connectingPorts)}");
        }

        if (ignoredPorts.Count > 0) {
            sb.Append($"\nFailed/Ignored: {string.Join(", ", ignoredPorts)}");
        }

        return sb.ToString();
    }
}
