using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using UnityEngine;

namespace Hive.TransportLayer.Shared
{
    public class StateObject
    {
        public const int BufferSize = 8 * 1024;
        public byte[] Buffer = new byte[BufferSize];
        public ArraySegment<byte> Segment;
        public Socket WorkSocket = null;
    }
    
    public class TCPSocket
    {
        public Socket Socket;
    }

    public class UDPSocket
    {
        public Socket Socket;
    }
    
    public enum SendProtocol : int
    {
        Unreliable = 0,
        Reliable = 1
    }
}