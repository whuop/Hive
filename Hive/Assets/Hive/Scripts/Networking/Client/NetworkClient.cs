using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using NetMessage;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using Unity.Jobs;
using Unity.Networking.Transport;
using UnityEngine;

namespace GameData.Networking
{
    public class NetworkClient : MonoBehaviour
    {
        private NetworkDriver m_driver;
        private NetworkConnection m_connection;

        public NetworkConnection.State ConnectionState { get { return m_driver.GetConnectionState(m_connection); } }

        private NetworkMessageBuffer m_messageBuffer;
        
        private Queue<IMessage> m_messages = new Queue<IMessage>();
        
        // Start is called before the first frame update
        void Start()
        {
            m_messageBuffer = new NetworkMessageBuffer();
            m_driver = NetworkDriver.Create();
            m_connection = default(NetworkConnection);

            var position = new NetMessage.Position
            {
                Id = 0,
                X = 10,
                Y = 10,
                Z = 10
            };

            using (var memStream = new MemoryStream())
            {
                    position.WriteTo(memStream);
                    Debug.Log("Message: " + memStream.Length);
            }

            ConnectLoopback();
        }

        void OnDestroy()
        {
            //Disconnect();
            m_driver.Dispose();
        }

        public void Connect(NetworkEndPoint endpoint)
        {
            m_connection = m_driver.Connect(endpoint);
            Debug.LogError("[NetworkClient] - Connecting to " + endpoint.Address);
        }

        [ContextMenu("Connect")]
        public void ConnectLoopback()
        {
            var endpoint = NetworkEndPoint.LoopbackIpv4;
            endpoint.Port = 9000;
            Connect(endpoint);
        }

        [ContextMenu("Disconnect")]
        public void Disconnect()
        {
            m_connection.Disconnect(m_driver);
        }

        public void PushMessage(IMessage message)
        {
            m_messages.Enqueue(message);
        }

        void Update()
        {
            m_driver.ScheduleUpdate().Complete();

            if (!m_connection.IsCreated)
                return;

            DataStreamReader stream;
            NetworkEvent.Type cmd;
            while ((cmd = m_connection.PopEvent(m_driver, out stream)) != NetworkEvent.Type.Empty)
            {
                switch(cmd)
                {
                    case NetworkEvent.Type.Connect:
                        Debug.Log("[NetworkClient] - Successfully connected to server!");

                        var handshake = new HandshakeRequest
                        {
                            Message = "Hello, World"
                        };

                        PushMessage(handshake);
                        break;
                    case NetworkEvent.Type.Disconnect:
                        Debug.Log("[NetworkClient] - Disconnected from server!");
                        break;
                    case NetworkEvent.Type.Data:
                        Debug.Log("[NetworkClient] - Received data message!");
                        break;
                    case NetworkEvent.Type.Empty:
                        Debug.LogError("[NetworkClient] - Received empty network message!");
                        break;
                }
            }
            
            //    Send network messages

            while (m_messages.Count > 0)
            {
                var message = m_messages.Dequeue();
                var packedMessage = new NativeArray<byte>(Any.Pack(message).ToByteArray(), Allocator.Temp);

                var writer = m_driver.BeginSend(m_connection);
                writer.WriteBytes(
                    packedMessage
                );
                m_driver.EndSend(writer);
                packedMessage.Dispose();
            }
            

        }
    }
}


