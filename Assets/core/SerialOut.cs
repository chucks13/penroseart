using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;

public class SerialOut
{
    private class S2MiniBoard
    {
        public SerialPort Port;
        public string PortName;
        public List<PixelSegment> Segments = new List<PixelSegment>();
        public byte[] ReusableBuffer = new byte[1024]; // Pre-allocated for performance
        public bool IsReady = false;

        public struct PixelSegment
        {
            public int StartIndex;
            public int Count;
        }
    }

    private List<S2MiniBoard> activeBoards = new List<S2MiniBoard>();
    private HashSet<string> ignoredPorts = new HashSet<string>();
    private int targetBaudRate;
    private float lastDiscoveryTime = 0f;
    private const float DiscoveryInterval = 2.0f; // Scan for new USB devices every 2 seconds

    private const int MAX_PIXELS_PER_SEGMENT = 300; // Sanity limit for buffer allocation
    private const byte CMD_QUERY = 0x3F; // '?'
    private const byte CMD_DATA  = 0x44; // 'D'
    private const byte CMD_LATCH = 0x4C; // 'L'

    public void Init(int baudRate)
    {
        this.targetBaudRate = 230400; // S2 Mini handles high speeds well
        DiscoverBoards();
    }

    private void DiscoverBoards()
    {
        string[] currentPorts = SerialPort.GetPortNames();

        // 1. Remove disconnected boards
        for (int i = activeBoards.Count - 1; i >= 0; i--)
        {
            if (!currentPorts.Contains(activeBoards[i].PortName))
            {
                Debug.Log($"[SerialOut] Board removed: {activeBoards[i].PortName}");
                if (activeBoards[i].Port.IsOpen) activeBoards[i].Port.Close();
                activeBoards.RemoveAt(i);
            }
        }

        // 2. Add new boards
        foreach (string portName in currentPorts)
        {
            if (!activeBoards.Any(b => b.PortName == portName) && !ignoredPorts.Contains(portName))
            {
                if (!TryConnectBoard(portName))
                    ignoredPorts.Add(portName); // Don't keep trying busy or non-responsive ports
            }
        }
    }

    private bool TryConnectBoard(string portName)
    {
        try
        {
            SerialPort sp = new SerialPort(portName, targetBaudRate);
            sp.ReadTimeout = 500;
            sp.WriteTimeout = 500;
            sp.Open();

            // S2 Mini / ESP32 often reboots on serial connection. 
            // We wait briefly for the bootloader to hand off to your sketch.
            System.Threading.Thread.Sleep(100); 
            sp.DiscardInBuffer();

            // Send Handshake/Query
            sp.Write(new byte[] { CMD_QUERY }, 0, 1);

            // The Arduino should respond with: [NumSegments][StartH][StartL][CountH][CountL]...
            int numSegments = sp.ReadByte();
            S2MiniBoard board = new S2MiniBoard { Port = sp, PortName = portName };
            
            // Ensure we have a large enough buffer for the biggest possible segment
            // (5 bytes header + 3 bytes per pixel)
            board.ReusableBuffer = new byte[5 + (MAX_PIXELS_PER_SEGMENT * 3)];

            for (int i = 0; i < numSegments; i++)
            {
                int start = (sp.ReadByte() << 8) | sp.ReadByte();
                int count = (sp.ReadByte() << 8) | sp.ReadByte();
                board.Segments.Add(new S2MiniBoard.PixelSegment { StartIndex = start, Count = count });
            }

            board.IsReady = true;
            activeBoards.Add(board);
            Debug.Log($"[SerialOut] Connected S2 Mini on {portName} handling {numSegments} segments.");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SerialOut] Failed to initialize port {portName}: {e.Message}");
            return false;
        }
    }

    public void send(Color[] data, byte level)
    {
        // Handle Hot-Plugging Discovery
        if (Time.time - lastDiscoveryTime > DiscoveryInterval)
        {
            DiscoverBoards();
            lastDiscoveryTime = Time.time;
        }

        if (activeBoards.Count == 0) return;

        // 1. Send pixel data to all segments across all boards
        foreach (var board in activeBoards)
        {
            if (!board.IsReady || !board.Port.IsOpen) continue;

            try
            {
                foreach (var seg in board.Segments)
                {
                    // Use the pre-allocated buffer to avoid GC spikes
                    board.ReusableBuffer[0] = CMD_DATA;
                    board.ReusableBuffer[1] = (byte)(seg.StartIndex >> 8);
                    board.ReusableBuffer[2] = (byte)(seg.StartIndex & 0xFF);
                    board.ReusableBuffer[3] = (byte)(seg.Count >> 8);
                    board.ReusableBuffer[4] = (byte)(seg.Count & 0xFF);

                    int p = 5;
                    for (int i = 0; i < seg.Count; i++)
                    {
                        int dataIdx = seg.StartIndex + i;
                        if (dataIdx >= data.Length) break;

                        board.ReusableBuffer[p++] = (byte)(data[dataIdx].r * level);
                        board.ReusableBuffer[p++] = (byte)(data[dataIdx].g * level);
                        board.ReusableBuffer[p++] = (byte)(data[dataIdx].b * level);
                    }

                    board.Port.Write(board.ReusableBuffer, 0, p);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SerialOut] Write error on {board.PortName}: {e.Message}");
                board.IsReady = false; // Mark for cleanup in next discovery cycle
            }
        }

        // 2. Synchronous Latch - Tell all boards to show()
        foreach (var board in activeBoards)
        {
            if (board.IsReady && board.Port.IsOpen)
            {
                try
                {
                    board.Port.Write(new byte[] { CMD_LATCH }, 0, 1);
                }
                catch { }
            }
        }
    }
}
