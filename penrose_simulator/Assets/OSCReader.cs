/*
using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
*/
using System;
using System.IO;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;
using UnityEngine;


public class UDPPacketIO
{
    private UdpClient Sender;
    private UdpClient Receiver;
    private bool socketsOpen;
    private string remoteHostName;
    private int remotePort;
    private int localPort;



    public UDPPacketIO(string hostIP, int remotePort, int localPort)
    {
        RemoteHostName = hostIP;
        RemotePort = remotePort;
        LocalPort = localPort;
        socketsOpen = false;
    }


    ~UDPPacketIO()
    {
        // latest time for this socket to be closed
        if (IsOpen())
        {
            Debug.Log("closing udpclient listener on port " + localPort);
            Close();
        }

    }

    /// <summary>
    /// Open a UDP socket and create a UDP sender.
    /// 
    /// </summary>
    /// <returns>True on success, false on failure.</returns>
    public bool Open()
    {
        try
        {
            Sender = new UdpClient();
            Debug.Log("Opening OSC listener on port " + localPort);

            IPEndPoint listenerIp = new IPEndPoint(IPAddress.Any, localPort);
            Receiver = new UdpClient(listenerIp);


            socketsOpen = true;

            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning("cannot open udp client interface at port " + localPort);
            Debug.LogWarning(e);
        }

        return false;
    }

    /// <summary>
    /// Close the socket currently listening, and destroy the UDP sender device.
    /// </summary>
    public void Close()
    {
        if (Sender != null)
            Sender.Close();

        if (Receiver != null)
        {
            Receiver.Close();
            // Debug.Log("UDP receiver closed");
        }
        Receiver = null;
        socketsOpen = false;

    }

    public void OnDisable()
    {
        Close();
    }

    /// <summary>
    /// Query the open state of the UDP socket.
    /// </summary>
    /// <returns>True if open, false if closed.</returns>
    public bool IsOpen()
    {
        return socketsOpen;
    }

    /// <summary>
    /// Send a packet of bytes out via UDP.
    /// </summary>
    /// <param name="packet">The packet of bytes to be sent.</param>
    /// <param name="length">The length of the packet of bytes to be sent.</param>
    public void SendPacket(byte[] packet, int length)
    {
        if (!IsOpen())
            Open();
        if (!IsOpen())
            return;

        Sender.Send(packet, length, remoteHostName, remotePort);
        //Debug.Log("osc message sent to "+remoteHostName+" port "+remotePort+" len="+length);
    }

    /// <summary>
    /// Receive a packet of bytes over UDP.
    /// </summary>
    /// <param name="buffer">The buffer to be read into.</param>
    /// <returns>The number of bytes read, or 0 on failure.</returns>
    public int ReceivePacket(byte[] buffer)
    {
        if (!IsOpen())
            Open();
        if (!IsOpen())
            return 0;


        IPEndPoint iep = new IPEndPoint(IPAddress.Any, localPort);
        byte[] incoming = Receiver.Receive(ref iep);
        int count = Math.Min(buffer.Length, incoming.Length);
        System.Array.Copy(incoming, buffer, count);
        return count;


    }



    /// <summary>
    /// The address of the board that you're sending to.
    /// </summary>
    public string RemoteHostName
    {
        get
        {
            return remoteHostName;
        }
        set
        {
            remoteHostName = value;
        }
    }

    /// <summary>
    /// The remote port that you're sending to.
    /// </summary>
    public int RemotePort
    {
        get
        {
            return remotePort;
        }
        set
        {
            remotePort = value;
        }
    }

    /// <summary>
    /// The local port you're listening on.
    /// </summary>
    public int LocalPort
    {
        get
        {
            return localPort;
        }
        set
        {
            localPort = value;
        }
    }
}


public class OSCReader : MonoBehaviour
{
    public int inPort = 8005;
    public string outIP = "192.168.1.255";
    public int outPort = 6161;

    private UDPPacketIO OscPacketIO;
    Thread ReadThread;
    private bool ReaderRunning;
    private OscMessageHandler AllMessageHandler;

    Hashtable AddressTable;

   public ArrayList messagesReceived;

    private object ReadThreadLock = new object();

    byte[] buffer;

    bool paused = false;

    void Awake()
    {
        //print("Opening OSC listener on port " + inPort);

        OscPacketIO = new UDPPacketIO(outIP, outPort, inPort);
        AddressTable = new Hashtable();

        messagesReceived = new ArrayList();

        buffer = new byte[1000];


        ReadThread = new Thread(Read);
        ReaderRunning = true;
        ReadThread.IsBackground = true;
        ReadThread.Start();

//#if UNITY_EDITOR
//        //UnityEditor.EditorApplication.playmodeStateChanged = HandleOnPlayModeChanged;
//        UnityEditor.EditorApplication.playModeStateChanged += HandleOnPlayModeChanged;  //FIX FOR UNITY POST 2017
//#endif

    }

    void OnDestroy()
    {
        Close();
    }

    void OnApplicationPause(bool pauseStatus)
    {
//#if !UNITY_EDITOR
//		paused = pauseStatus;
//		print ("Application paused : " + pauseStatus);
//#endif
    }


    void Update()
    {


        if (messagesReceived.Count > 0)
        {
            //Debug.Log("received " + messagesReceived.Count + " messages");
            lock (ReadThreadLock)
            {
                foreach (OscMessage om in messagesReceived)
                {

                    if (AllMessageHandler != null)
                        AllMessageHandler(om);

                    ArrayList al = (ArrayList)Hashtable.Synchronized(AddressTable)[om.address];
                    if (al != null)
                    {
                        foreach (OscMessageHandler h in al)
                        {
                            h(om);
                        }
                    }

                }
                messagesReceived.Clear();
            }
        }
    }



    /// <summary>
    /// Make sure the PacketExchange is closed.
    /// </summary>
    /// 
    /*
	~OSC()
    {           
    	Cancel();
        //Debug.LogError("~Osc");
    }
    */
    public void Close()
    {
        //Debug.Log("Osc Cancel start");


        if (ReaderRunning)
        {
            ReaderRunning = false;
            ReadThread.Abort();

        }

        if (OscPacketIO != null && OscPacketIO.IsOpen())
        {
            OscPacketIO.Close();
            OscPacketIO = null;
            print("Closed OSC listener");
        }

    }


    /// <summary>
    /// Read Thread.  Loops waiting for packets.  When a packet is received, it is 
    /// dispatched to any waiting All Message Handler.  Also, the address is looked up and
    /// any matching handler is called.
    /// </summary>
    private void Read()
    {
        try
        {
            while (ReaderRunning)
            {


                int length = OscPacketIO.ReceivePacket(buffer);

                if (length > 0)
                {
                    lock (ReadThreadLock)
                    {

                        if (paused == false)
                        {
                            ArrayList newMessages = OSC.PacketToOscMessages(buffer, length);
                            messagesReceived.AddRange(newMessages);
                        }

                    }


                }
                else
                    Thread.Sleep(5);
            }
        }

        catch (Exception e)
        {
            Debug.Log("ThreadAbortException" + e);
        }
        finally
        {

        }

    }

    /// <summary>
    /// Send an individual OSC message.  Internally takes the OscMessage object and 
    /// serializes it into a byte[] suitable for sending to the PacketIO.
    /// </summary>
    /// <param name="oscMessage">The OSC Message to send.</param>   
    public void Send(OscMessage oscMessage)
    {
        byte[] packet = new byte[1000];
        int length = OSC.OscMessageToPacket(oscMessage, packet, 1000);
        OscPacketIO.SendPacket(packet, length);
    }

    /// <summary>
    /// Sends a list of OSC Messages.  Internally takes the OscMessage objects and 
    /// serializes them into a byte[] suitable for sending to the PacketExchange.
    /// </summary>
    /// <param name="oms">The OSC Message to send.</param>   
    public void Send(ArrayList oms)
    {
        byte[] packet = new byte[1000];
        int length = OSC.OscMessagesToPacket(oms, packet, 1000);
        OscPacketIO.SendPacket(packet, length);
    }

    /// <summary>
    /// Set the method to call back on when any message is received.
    /// The method needs to have the OscMessageHandler signature - i.e. void amh( OscMessage oscM )
    /// </summary>
    /// <param name="amh">The method to call back on.</param>   
    public void SetAllMessageHandler(OscMessageHandler amh)
    {
        AllMessageHandler = amh;
    }

}
