using System;
using UnityEngine;
using Random = UnityEngine.Random;

[Serializable]
public class ACNHandler
{
    public  Color[] buffer;
    int timeout;

    private UDPReceive listenerACN;
    [HideInInspector]

    public string DebugText() => "ACN";

    public void Init()
    {
        listenerACN = new UDPReceive(5568, handleACN);
        buffer = new Color[900];
        timeout = 0;
    }
     public bool Update()
    {
        if (timeout > 0)
        {
            timeout--;
            return true;
        }
        return false;
    }
    public void handleACN(byte[] packet)
    {
        int dataidx = 126;
        int length = packet.Length - dataidx;
        int universe = (packet[113] << 8) + packet[114];
        universe--;     // acn starts at 1, I start at 0
                        //        print(string.Format("{0},{1}",universe,length));
                        // copy packets into buffer location
        int j = universe * 170;
        for(int i=0;i<length;i+=3)
        {
            if (j >= 900)
                return;
            byte r = packet[dataidx++];
            byte g = packet[dataidx++];
            byte b = packet[dataidx++];
            buffer[j] = new Color32(r, g, b,255);
            j++;
        }
        timeout = 20;           // numer of frames time is reset to
     }

}


