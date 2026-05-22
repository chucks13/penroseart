using System;
using UnityEngine;

[Serializable]
/// <summary>
/// Receives external RGB pixel frames over UDP for optional blending into the native Penrose output.
/// </summary>
public class PixelReceiver
{
    public Color[] buffer;
    public int timeout;
    private UDPReceive listener;

    /// <summary>
    /// Allocates the 900-tile receive buffer and starts the UDP listener on port 7778.
    /// </summary>
    public void Init()
    {
        // Listen on 7778 for the AI/Playback stream
        listener = new UDPReceive(7778, handlePixel);
        buffer = new Color[900];
        timeout = 0;
    }

    /// <summary>
    /// Advances the frame-count timeout and reports whether recent external pixels are still active.
    /// </summary>
    public bool Update()
    {
        if (timeout > 0)
        {
            timeout--;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Decodes one incoming RGB packet into the receive buffer. Packet bytes 4-5 are the byte offset; payload starts at byte 6.
    /// </summary>
    public void handlePixel(byte[] packet)
    {
        // Our new header is exactly 6 bytes
        if (packet.Length < 6) return;

        // 1. Extract Header Info
        // ushort pLen = (ushort)((packet[0] << 8) | packet[1]); // Not strictly needed if we use packet.Length
        byte context = packet[2];
        // byte seq = packet[3]; // Optional: track sequence if you want to detect dropped frames
        ushort byteOffset = (ushort)((packet[4] << 8) | packet[5]);

        // 2. Triage by Context (0x00 is our RGB Data)
        if (context == 0x00)
        {
            int dataStart = 6;
            int dataLength = packet.Length - dataStart;

            // Start 'j' at the specific pixel index (byteOffset / 3)
            int j = byteOffset / 3;

            for (int i = 0; i < dataLength; i += 3)
            {
                if (j >= buffer.Length) break;

                byte r = packet[dataStart + i];
                byte g = packet[dataStart + i + 1];
                byte b = packet[dataStart + i + 2];

                // Update the buffer
                buffer[j] = new Color32(r, g, b, 255);
                j++;
            }

            timeout = 100; // Keep the display active
        }
    }
}
