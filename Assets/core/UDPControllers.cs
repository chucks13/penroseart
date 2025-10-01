
using UnityEngine;
using System.Collections;

using System.Collections.Generic;

using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Runtime.InteropServices;


public class UDPReceive //: MonoBehaviour
{

    // receiving Thread
    Thread receiveThread;
    public Display parent;

    // udpclient object
    UdpClient client;

    // public
    // public string IP = "127.0.0.1"; default local
    public int port; // define > init

    public delegate void handler(byte[] data);
    handler thehandler;

    public UDPReceive(int p, handler myhandler)
    {
        thehandler = myhandler;
        port = p;
        receiveThread = new Thread(
            new ThreadStart(ReceiveData));
        receiveThread.IsBackground = true;
        receiveThread.Start();
    }

    /*
    //Creates a UdpClient for reading incoming data.
UdpClient receivingUdpClient = new UdpClient(11000);

//Creates an IPEndPoint to record the IP Address and port number of the sender.
// The IPEndPoint will allow you to read datagrams sent from any source.
IPEndPoint RemoteIpEndPoint = new IPEndPoint(IPAddress.Any, 0);
try{

    // Blocks until a message returns on this socket from a remote host.
    Byte[] receiveBytes = receivingUdpClient.Receive(ref RemoteIpEndPoint);

    string returnData = Encoding.ASCII.GetString(receiveBytes);

    Console.WriteLine("This is the message you received " +
                              returnData.ToString());
    Console.WriteLine("This message was sent from " +
                                RemoteIpEndPoint.Address.ToString() +
                                " on their port number " +
                                RemoteIpEndPoint.Port.ToString());
}
catch ( Exception e ){
    Console.WriteLine(e.ToString());
}
    */

    // receive thread
    private void ReceiveData()
    {

        client = new UdpClient(port);
        while (true)
        {

            IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, 0);
            try
            {
                // Bytes empfangen.
                byte[] data = client.Receive(ref anyIP);
                thehandler(data);
            }
            catch (Exception err)
            {
                //               print(err.ToString());
            }
        }
    }

}


