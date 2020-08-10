using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Google.Protobuf;
using Hive.TransportLayer.Pipelines;
using Leopotam.Ecs;
using UnityEngine;

namespace Hive.TransportLayer
{
    public class NetworkServer
    {
        private SystemInformation m_info = new SystemInformation(SystemType.Server);
        private StateObject m_state = new StateObject();
        
        private MessageLookupTable m_lookupTable;

        private TCPSocket m_tcpSocket;

        private AsyncCallback m_listenCallback = null;

        private EcsWorld m_world;
        private EcsSystems m_updateSystems;
        private EcsSystems m_fixedUpdateSystems;
        private EcsSystems m_lateUpdatesystems;

        public EcsSystems UpdateSystem => m_updateSystems;
        public EcsSystems FixedUpdateSystem => m_fixedUpdateSystems;
        public EcsSystems LateUpdateSystem => m_lateUpdatesystems;

        private IPipelineManager m_pipelineManager;
        public IPipelineManager PipelineManager
        {
            get { return m_pipelineManager; }
        }
        
        public NetworkServer(MessageLookupTable lookupTable)
        {
            m_lookupTable = lookupTable;
            m_pipelineManager = new PipelineManager(lookupTable);
            
            m_tcpSocket = new TCPSocket();

            m_world = new EcsWorld();
            m_updateSystems = new EcsSystems(m_world);
            m_updateSystems.Add(new AcceptConnectionsSystem());
            m_updateSystems.Add(new MultiReceiveReliableMessageSystem());
            m_updateSystems.Add(new MultiSendPipelineMessages());
            //m_updateSystems.Add(new SendPipelineMessages());
            m_updateSystems.Inject(m_world);
            m_updateSystems.Inject(m_tcpSocket);
            m_updateSystems.Inject(m_state);
            m_updateSystems.Inject(m_pipelineManager);
            m_updateSystems.Inject(m_info);
            m_updateSystems.Inject(m_lookupTable);
            m_updateSystems.Inject(new Dictionary<Socket, PipelineManager>());
            
            m_lateUpdatesystems = new EcsSystems(m_world);
            m_fixedUpdateSystems = new EcsSystems(m_world);
        }

        public void Start()
        {
            m_state = new StateObject();
            
            m_updateSystems.Init();
            m_lateUpdatesystems.Init();
            m_fixedUpdateSystems.Init();
        }

        public void Stop()
        {
            
        }

        public void Disconnect()
        {
            m_tcpSocket.Socket.Disconnect(true);
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
        
        /*public void Send(Socket destination, IMessage message, SendProtocol sendProtocol)
        {
            Send(destination,m_lookupTable.Serialize(message), sendProtocol);
        }*/

        /*public void Send(Socket destination, byte[] data, SendProtocol sendProtocol)
        {
            destination.BeginSendTo(data, 0, data.Length, SocketFlags.None, destination.RemoteEndPoint, (ar) =>
            {
                Socket so = (Socket)ar.AsyncState;
                int bytes = so.EndSendTo(ar);
            }, destination);
        }*/
    }

    public enum SendProtocol : int
    {
        Unreliable = 0,
        Reliable = 1
    }

    public class TCPSocket
    {
        public Socket Socket;
    }

    public class UDPSocket
    {
        public Socket Socket;
    }

    public class AcceptConnectionsSystem : IEcsInitSystem, IEcsRunSystem, IEcsDestroySystem
    {
        private SystemInformation m_systemInfo;
        private EcsWorld m_world;
        private TCPSocket m_listener;
        private Task<Socket> m_acceptTask;

        private MessageLookupTable m_lookupTable;
        private Dictionary<Socket, PipelineManager> m_managers;
        
        public void Init()
        {
            IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, 11000);
            
            //    Create TCP/IP socket
            m_listener.Socket = new Socket(localEndPoint.AddressFamily,  
                SocketType.Stream, ProtocolType.Tcp);
            m_listener.Socket.Bind(localEndPoint);
            m_listener.Socket.Listen(100);
            m_acceptTask = m_listener.Socket.AcceptAsync();
            Debug.Log($"{m_systemInfo.GetTag()} Booting new accept job!");
        }

        public void Run()
        {
            if (m_acceptTask.IsCompleted || m_acceptTask.IsCanceled)
            {
                if (m_acceptTask.IsCompleted)
                {
                    var res = m_acceptTask.Result;
                    Debug.Log($"{m_systemInfo.GetTag()} Client with Local EndPoint {((IPEndPoint)res.LocalEndPoint).Address} Remote EndPoint {((IPEndPoint)res.RemoteEndPoint).Address}");

                    var entity = m_world.NewEntity();
                    ref HiveConnection connection = ref entity.Get<HiveConnection>();
                    connection.Socket = res;
                    
                    m_managers.Add(res, new PipelineManager(m_lookupTable));
                }
                m_acceptTask = m_listener.Socket.AcceptAsync();
            }
        }

        public void Destroy()
        {
        }
    }

    public class MultiReceiveReliableMessageSystem : IEcsInitSystem, IEcsRunSystem, IEcsDestroySystem
    {
        private SystemInformation m_systemInfo;
        private IPipelineManager m_pipelineManager;
        private TCPSocket m_listener;
        
        private List<Task<int>> m_receiveMessageTasks = new List<Task<int>>();
        private List<EcsEntity> m_queuedEntities = new List<EcsEntity>();

        private EcsFilter<HiveConnection> m_filter;
        
        private Dictionary<EcsEntity, Task<int>> m_entityToTaskMap = new Dictionary<EcsEntity, Task<int>>();
        private Dictionary<Task<int>, EcsEntity> m_taskToEntityMap = new Dictionary<Task<int>, EcsEntity>();
        
        private Dictionary<EcsEntity, StateObject> m_states = new Dictionary<EcsEntity, StateObject>();
        
        public void Init()
        {
            Debug.LogError($"{m_systemInfo.GetTag()} Initing RecieveReliableSystem");
        }

        public void Run()
        {
            Debug.LogError($"{m_systemInfo.GetTag()} Running ReceiveReliable!");
            int len = m_filter.GetEntitiesCount();
            for (int i = 0; i < len; i++)
            {
                var entity = m_filter.GetEntity(i);
                ref HiveConnection connection = ref m_filter.Get1(i);
                
                //    Check if this entity has a task that has already been completed
                if (m_entityToTaskMap.ContainsKey(entity))
                {
                    Task<int> task = m_entityToTaskMap[entity];
                    if (task.IsCompleted || task.IsCanceled || task.IsFaulted)
                    {
                        m_entityToTaskMap.Remove(entity);
                        m_taskToEntityMap.Remove(task);
                        
                        //    Read the message itself
                        Debug.Log($"{m_systemInfo.GetTag()} Received Message!");

                        int messageSize = task.Result;

                        StateObject state = m_states[entity];
                        CodedInputStream stream = new CodedInputStream(state.Buffer, 0, messageSize);

                        int messageIndex = stream.ReadInt32();
                        int numMessages = stream.ReadInt32();

                        for (int j = 0; j < numMessages; j++)
                        {
                            IPipeline pipeline = m_pipelineManager.GetPipeline(messageIndex);
                            pipeline.PushMessage(stream, state.WorkSocket);
                        }
                        
                        Debug.Log($"{m_systemInfo.GetTag()} Successfully pushed messages to pipeline!");
                    }
                }
                
                //    Check if this entity has a task that's been started for it
                if (!m_entityToTaskMap.ContainsKey(entity))
                {
                    //    Wasn't found, lets boot up a new listening task
                    if (!m_states.ContainsKey(entity))
                    {
                        var state = new StateObject();
                        state.Segment = new ArraySegment<byte>(state.Buffer);
                        m_states.Add(entity, state);
                    }

                    var taskState = m_states[entity];
                    taskState.WorkSocket = connection.Socket;
                    var task = connection.Socket.ReceiveAsync(taskState.Segment, SocketFlags.None);
                    m_entityToTaskMap.Add(entity, task);
                    m_taskToEntityMap.Add(task, entity);
                    Debug.Log($"{m_systemInfo.GetTag()} Booted a new Receive Reliable Task for : " + connection.Socket.RemoteEndPoint);
                }
            }
        }

        public void Destroy()
        {
        }
    }

    public class CloseConnectionSystem : IEcsInitSystem, IEcsRunSystem, IEcsDestroySystem
    {
        public void Init()
        {
            
        }

        public void Run()
        {
        }

        public void Destroy()
        {
        }
    }
    
    public class MultiSendPipelineMessages : IEcsInitSystem, IEcsRunSystem, IEcsDestroySystem
    {
        private TCPSocket m_tcpSocket;
        private Dictionary<Socket, PipelineManager> m_managers;
        private EcsFilter<HiveConnection> m_connections;
        
        public void Init()
        {
            Debug.Log("Running Init SendPIpelineMessages");
        }

        public void Run()
        {
            Debug.Log("Running SendPipelineMessages!");

            int length = m_connections.GetEntitiesCount();
            for (int j = 0; j < length; j++)
            {
                var connection = m_connections.Get1(j);
                PipelineManager manager;
                bool found = m_managers.TryGetValue(connection.Socket, out manager);
                if (!found)
                {
                    Debug.LogError($"Failed to find PipelineManager for connection {connection.Socket.RemoteEndPoint}");
                    continue;
                }
                
                manager.OutputStream.Flush();
                
                int count = 0;
                IReadOnlyList<IOutputPipeline> outputs = manager.GetOutputPipelines();
                //    Pack output pipelines
                for (int i = 0; i < outputs.Count; i++)
                {
                    var pipeline = outputs[i];
                    count += pipeline.MessageCount;
                    if (pipeline.MessageCount == 0)
                        continue;
                    //    Pipelines are packed into the message buffer housed in the pipeline manager
                    pipeline.PackMessages(manager.OutputStream);
                }

                if (count == 0)
                    return;
                
                Send(connection.Socket, manager.MessageBuffer);
                Debug.Log("Sent Message to: " + connection.Socket.RemoteEndPoint);
            }
        }
        
        public void Send(Socket destination, byte[] data)
        {
            destination.BeginSend(data, 0, data.Length, SocketFlags.None, (ar) =>
            {
                Socket so = (Socket)ar.AsyncState;
                int bytes = so.EndSend(ar);
                Debug.Log("Completed Sending MEssage!!");
            }, destination);
        }

        public void Destroy()
        {
        }
    }

    public struct HiveConnection
    {
        public Socket Socket;
    }

    public class SystemInformation
    {
        private SystemType m_systemType;
        
        public SystemInformation(SystemType systemType)
        {
            m_systemType = systemType;
        }

        public string GetTag()
        {
            return $"[{m_systemType.ToString()}] ";
        }
    }

    public enum SystemType : int
    {
        Server = 0,
        Client = 1
    }
    
    public class StateObject
    {
        public const int BufferSize = 8 * 1024;
        public byte[] Buffer = new byte[BufferSize];
        public ArraySegment<byte> Segment;
        public Socket WorkSocket = null;
    }
}