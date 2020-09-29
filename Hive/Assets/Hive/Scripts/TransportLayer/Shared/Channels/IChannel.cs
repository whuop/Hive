using System.Collections.Generic;
using System.Net.Sockets;
using Google.Protobuf;
using Hive.TransportLayer.Shared.Pipelines;

namespace Hive.TransportLayer.Shared.Channels
{
    public interface IChannel
    {
        Socket Socket { get; }
        
        Protocol Protocol { get; }
        byte[] Buffer { get; }
        bool IsStale { get; }
        void Send(IReadOnlyList<IOutputPipeline> pipelines);
        void GenerateSocket(AddressFamily addressFamily);
        void Teardown();
    }

    public enum Protocol
    {
        TCP = 0,
        UDP = 1
    }
    
    public class ReliableChannel : IChannel
    {
        public Socket Socket
        {
            get => m_socket;
        }
        
        public Protocol Protocol
        {
            get => Protocol.TCP;
        }

        public byte[] Buffer
        {
            get => m_messageBuffer;
        }

        public bool IsStale
        {
            get => m_isStale;
            set => m_isStale = true;
        }

        private Socket m_socket;
        private bool m_isStale = false;
        
        public const int BYTES_PER_CHANNEL = 1024;
        private byte[] m_messageBuffer;
        private CodedOutputStream m_outputStream;
        
        public ReliableChannel()
        {
            m_messageBuffer = new byte[BYTES_PER_CHANNEL];
            m_outputStream = new CodedOutputStream(m_messageBuffer);
        }

        public ReliableChannel(Socket socket) : this()
        {
            m_socket = socket;
        }

        public void GenerateSocket(AddressFamily addressFamily)
        {
            m_socket = new Socket(addressFamily, SocketType.Stream, ProtocolType.Tcp);
        }

        public void Teardown()
        {
            m_socket.Dispose();
            m_socket = null;
        }

        private int m_count = 0;
        public void Send(IReadOnlyList<IOutputPipeline> pipelines)
        {
            m_outputStream.Flush();
            m_count = 0;
            for (int i = 0; i < pipelines.Count; i++)
            {
                var pipeline = pipelines[i];
                m_count += pipeline.MessageCount;
                if (pipeline.MessageCount == 0)
                {
                    continue;
                }
                pipeline.PackMessages(m_outputStream);
            }

            if (m_count == 0)
                return;
            m_socket.Send(m_messageBuffer);
        }
    }
}


