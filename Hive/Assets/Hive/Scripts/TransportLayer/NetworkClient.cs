using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Google.Protobuf;
using Hive.TransportLayer.Pipelines;
using Leopotam.Ecs;
using NetMessage;
using UnityEngine;

namespace Hive.TransportLayer
{
    
    public class NetworkClient
    {
        public enum ConnectionState
        {
            Disconnected = 0,
            Connecting = 1,
            Connected = 2
        };
        
        private SystemInformation m_info = new SystemInformation(SystemType.Client);
        private StateObject m_state = new StateObject();
        private MessageLookupTable m_lookupTable;
        private TCPSocket m_tcpSocket;

        private IPipelineManager m_pipelineManager;
        public IPipelineManager PipelineManager
        {
            get { return m_pipelineManager; }
        }

        private EcsWorld m_world;
        private EcsSystems m_updateSystems;
        private EcsSystems m_fixedUpdateSystems;
        private EcsSystems m_lateUpdatesystems;

        public EcsSystems UpdateSystem => m_updateSystems;
        public EcsSystems FixedUpdateSystem => m_fixedUpdateSystems;
        public EcsSystems LateUpdateSystem => m_lateUpdatesystems;

        private ConnectionState m_connectionState = ConnectionState.Disconnected;
        public ConnectionState State
        {
            get { return m_connectionState; }
        }
        
        public NetworkClient(MessageLookupTable lookupTable)
        {
            m_lookupTable = lookupTable;
            m_pipelineManager = new PipelineManager(lookupTable);
            m_tcpSocket = new TCPSocket();
            
            m_world = new EcsWorld();
            m_updateSystems = new EcsSystems(m_world);
            m_updateSystems.Add(new AuthenticationSystem());
            m_updateSystems.Add(new ReceiveReliableMessageSystem());
            m_updateSystems.Add(new SendPipelineMessages());
            m_updateSystems.Inject(m_tcpSocket);
            m_updateSystems.Inject(m_state);
            m_updateSystems.Inject(m_pipelineManager);
            m_updateSystems.Inject(m_world);
            m_updateSystems.Inject(m_info);
            
            m_lateUpdatesystems = new EcsSystems(m_world);
            m_lateUpdatesystems.Inject(m_tcpSocket);
            m_lateUpdatesystems.Inject(m_state);
            m_lateUpdatesystems.Inject(m_pipelineManager);
            m_lateUpdatesystems.Inject(m_world);
            m_lateUpdatesystems.Inject(m_info);
            m_fixedUpdateSystems = new EcsSystems(m_world);
        }

        public void Connect(IPEndPoint address, string username, string password)
        {
            if (m_connectionState != ConnectionState.Disconnected)
                return;
            m_connectionState = ConnectionState.Connecting;
            
            
            m_tcpSocket.Socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            m_tcpSocket.Socket.BeginConnect(address.Address, address.Port,
                (ar) =>
                {
                    TCPSocket socket = (TCPSocket) ar.AsyncState;
                    socket.Socket.EndConnect(ar);
                    
                    Debug.LogError($"{m_info.GetTag()} Successfully established connection with server!");
                    
                    var entity = m_world.NewEntity();
                    ref HiveConnection connection = ref entity.Get<HiveConnection>();
                    connection.Socket = socket.Socket;
                    Debug.LogError($"{m_info.GetTag()} Created netconnection : {connection}");
                    
                    //    Init ECS Systems when connection has been fully established
                    //m_updateSystems.ProcessInjects();
                    //m_lateUpdatesystems.ProcessInjects();
                    //m_fixedUpdateSystems.ProcessInjects();
                    
                    m_updateSystems.Init();
                    m_lateUpdatesystems.Init();
                    m_fixedUpdateSystems.Init();
                    
                    m_connectionState = ConnectionState.Connected;
                    
                }, m_tcpSocket);  
        }

        public void Disconnect()
        {
        }
        
        public void Send(IMessage message)
        {
            Send(m_lookupTable.Serialize(message));
        }

        public void Update()
        {
            m_updateSystems.Run();
        }

        public void FixedUpdate()
        {
            m_fixedUpdateSystems.Run();
        }

        public void LateUpdate()
        {
            m_lateUpdatesystems.Run();
        }

        public void Send(byte[] data)
        {
            m_tcpSocket.Socket.BeginSend(data, 0, data.Length, SocketFlags.None, (ar) =>
            {
                StateObject so = (StateObject)ar.AsyncState;
                int bytes = m_tcpSocket.Socket.EndSend(ar);
            }, m_state);
        }
        
        private void Receive()
        {   
            /*
            try
            {
                EndPoint tempPoint = new IPEndPoint(IPAddress.Any, 0);
                m_socket.BeginReceiveFrom(m_state.Buffer, 0, BufferSize, SocketFlags.None, ref tempPoint, m_recv = (ar) =>
                {
                    State so = (State) ar.AsyncState;
                    SocketError error;
                    int bytes = m_socket.EndReceiveFrom(ar, ref tempPoint);
                    Assert.IsTrue(bytes >= 0);
                    if (bytes == 0)
                    {
                        Debug.Log("[Server] Disconnect: " + ((IPEndPoint)tempPoint).Address);
                    }
                    
                    CodedInputStream stream = new CodedInputStream(so.Buffer, 0, bytes);
                    Assert.IsNotNull(stream);
                    int messageIndex = stream.ReadInt32();
                    Assert.IsTrue(messageIndex >= 0);
                    IPipeline pipeline = m_pipelinesIntMap[messageIndex];
                    Assert.IsNotNull(pipeline);
                    pipeline.PushMessage(stream, tempPoint);
                    m_socket.BeginReceiveFrom(m_state.Buffer, 0, BufferSize, SocketFlags.None, ref tempPoint, m_recv, so);
                }, m_state);
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
            }*/
        }
    }

    /*public class ConnectSystem : IEcsInitSystem, IEcsRunSystem, IEcsDestroySystem
    {
        private TCPSocket m_listener;
        private Task<Socket> m_acceptTask;
        
        public void Init()
        {
            Debug.LogError("Started connecting!");
            m_tcpSocket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            m_tcpSocket.BeginConnect(address.Address, address.Port,
                (ar) =>
                {
                    Debug.LogError("Inside connection task!");
                    Socket socket = (Socket) ar.AsyncState;
                    Debug.LogError("Inside connection task again!");
                    socket.EndConnect(ar);
                    Debug.LogError("[Client] - Successfully established connection with server!");

                }, m_tcpSocket);  
        }

        public void Destroy()
        {
        }
    }*/

    public class AuthenticationSystem : IEcsInitSystem, IEcsRunSystem, IEcsDestroySystem
    {
        private IPipelineManager m_pipelines;

        private OutputPipeline<HandshakeRequest> m_handshakeRequests;
        private Pipeline<HandshakeResponse> m_handshakeResponses;
        
        public void Init()
        {
            m_handshakeRequests = m_pipelines.GetOutputPipeline<HandshakeRequest>();
            m_handshakeResponses = m_pipelines.GetPipeline<HandshakeResponse>();
            
            m_handshakeRequests.PushMessage(new HandshakeRequest()
            {
                Username = "whuop",
                Password = "bajs"
            });
        }

        public void Run()
        {
            while (m_handshakeResponses.Count > 0)
            {
                var response = m_handshakeResponses.PopMessageTyped();
                switch (response.Message.State)
                {
                    case ConnectionState.Connected:
                        Debug.Log("[Client] Successfully authenticated with the server!");
                        break;
                    case ConnectionState.Disconnected:
                        Debug.Log("[Client] !!Failed to authenticate with the server!");
                        break;
                }
                m_handshakeResponses.Release(response);
            }
        }

        public void Destroy()
        {
        }
    }
    
    public class ReceiveReliableMessageSystem : IEcsInitSystem, IEcsRunSystem, IEcsDestroySystem
    {
        private SystemInformation m_systemInfo;
        private IPipelineManager m_pipelineManager;
        private TCPSocket m_listener;
        
        private List<Task<int>> m_receiveMessageTasks = new List<Task<int>>();
        private List<EcsEntity> m_queuedEntities = new List<EcsEntity>();

        private EcsFilter<HiveConnection> m_filter;
        
        private Task<int> m_task;
        private StateObject m_state;
        
        public void Init()
        {
            m_state = new StateObject()
            {
                WorkSocket = m_listener.Socket,
                Buffer = new byte[1024]
            };
            m_state.Segment = new ArraySegment<byte>(m_state.Buffer);
            
            Debug.LogError($"{m_systemInfo.GetTag()} Initing RecieveReliableSystem");
            m_task = m_state.WorkSocket.ReceiveAsync(m_state.Segment, SocketFlags.None);
        }

        public void Run()
        {
            Debug.LogError($"{m_systemInfo.GetTag()} Running ReceiveReliable!");
            if (m_task.IsCompleted || m_task.IsCanceled || m_task.IsFaulted)
            {
                //    Read the message itself
                Debug.Log($"{m_systemInfo.GetTag()} Received Message!");

                int messageSize = m_task.Result;
                
                CodedInputStream stream = new CodedInputStream(m_state.Buffer, 0, messageSize);

                int messageIndex = stream.ReadInt32();
                int numMessages = stream.ReadInt32();

                for (int j = 0; j < numMessages; j++)
                {
                    IPipeline pipeline = m_pipelineManager.GetPipeline(messageIndex);
                    pipeline.PushMessage(stream, m_state.WorkSocket);
                }
                
                Debug.Log($"{m_systemInfo.GetTag()} Successfully pushed messages to pipeline!");
                m_task = m_state.WorkSocket.ReceiveAsync(m_state.Segment, SocketFlags.None);
            }
        }

        public void Destroy()
        {
        }
    }

    public class SendPipelineMessages : IEcsInitSystem, IEcsRunSystem, IEcsDestroySystem
    {
        private TCPSocket m_tcpSocket;
        private IPipelineManager m_pipelineManager;

        private IReadOnlyList<IPipeline> m_inputPipelines;
        private IReadOnlyList<IOutputPipeline> m_outputPipelines;

        private CodedOutputStream m_outputStream;
        
        public void Init()
        {
            m_inputPipelines = m_pipelineManager.GetInputPipelines();
            m_outputPipelines = m_pipelineManager.GetOutputPipelines();
            m_outputStream = new CodedOutputStream(m_pipelineManager.MessageBuffer);
            Debug.Log("Running Init SendPIpelineMessages");
        }

        public void Run()
        {
            m_outputStream.Flush();
            Debug.Log("Running SendPipelineMessages!");
            int count = 0;
            //    Pack output pipelines
            for (int i = 0; i < m_outputPipelines.Count; i++)
            {
                var pipeline = m_outputPipelines[i];
                count += pipeline.MessageCount;
                if (pipeline.MessageCount == 0)
                    continue;
                //    Pipelines are packed into the message buffer housed in the pipeline manager
                pipeline.PackMessages(m_outputStream);
            }

            if (count == 0)
                return;
            Send(m_pipelineManager.MessageBuffer);
            Debug.Log("Sent Message!");
        }
        
        public void Send(byte[] data)
        {
            m_tcpSocket.Socket.BeginSend(data, 0, data.Length, SocketFlags.None, (ar) =>
            {
                TCPSocket so = (TCPSocket)ar.AsyncState;
                int bytes = so.Socket.EndSend(ar);
            }, m_tcpSocket);
        }

        public void Destroy()
        {
        }
    }
}


