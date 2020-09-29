using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Google.Protobuf;
using Hive.TransportLayer.Shared;
using Hive.TransportLayer.Shared.Channels;
using Hive.TransportLayer.Shared.Components;
using Hive.TransportLayer.Shared.Pipelines;
using Leopotam.Ecs;
using UnityEngine;

namespace Hive.TransportLayer.Server.Systems
{
    public class AcceptConnectionsSystem : IEcsInitSystem, IEcsRunSystem, IEcsDestroySystem
    {
        private SystemInformation m_systemInfo;
        private EcsWorld m_world;
        private TCPSocket m_listener;
        private Task<Socket> m_acceptTask;

        private MessageLookupTable m_lookupTable;
        
        private Dictionary<HiveConnection, PipelineManager> m_managers;
        private Dictionary<HiveConnection, ReliableChannel> m_reliableChannels;

        private uint m_nextSessionID = 0;
        
        public void Init()
        {
            IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, 11000);
            
            //    Create TCP/IP socket
            m_listener.Socket = new Socket(localEndPoint.AddressFamily,  
                SocketType.Stream, ProtocolType.Tcp);
            m_listener.Socket.Bind(localEndPoint);
            m_listener.Socket.Listen(100);
            m_acceptTask = m_listener.Socket.AcceptAsync();
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
                    connection.IsStale = false;
                    connection.Socket = res;
                    connection.SessionID = m_nextSessionID;
                    m_nextSessionID++;
                    
                    m_managers.Add(connection, new PipelineManager(m_lookupTable));
                    m_reliableChannels.Add(connection, new ReliableChannel(connection.Socket));
                }
                m_acceptTask = m_listener.Socket.AcceptAsync();
            }
        }

        public void Destroy()
        {
        }
    }

    public class CleanupConnectionsSystem : IEcsInitSystem, IEcsRunSystem, IEcsDestroySystem
    {
        private SystemInformation m_systemInfo;
        private EcsWorld m_world;
        private EcsFilter<HiveConnection> m_filter;
        
        private Dictionary<HiveConnection, PipelineManager> m_managers;
        private Dictionary<HiveConnection, ReliableChannel> m_reliableChannels;
        
        public void Init()
        {
            
        }

        public void Run()
        {
            int len = m_filter.GetEntitiesCount();
            for (int i = 0; i < len; i++)
            {
                var entity = m_filter.GetEntity(i);
                ref HiveConnection connection = ref m_filter.Get1(i);
                
                bool isActive = IsActiveConnection(connection);
                if (!isActive)
                {
                    if (!connection.Socket.Connected)
                        Debug.LogError("Not connected!");
                    if (connection.IsStale)
                        Debug.LogError("Connection is stale!!");
                    Debug.Log($"{m_systemInfo.GetTag()} Cleaned up connection: " + connection.Username);
                    m_managers.Remove(connection);
                    m_reliableChannels.Remove(connection);
                    entity.Destroy();
                }
            }
        }

        private bool IsActiveConnection(HiveConnection connection)
        {
            try
            {
                if (!connection.Socket.Connected || connection.IsStale)
                {
                    //    Connection has been terminated either willingly or forcefully
                    return false;
                }

                return true;
            }

            catch (SocketException)
            {
                return false;
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
        }

        public void Run()
        {
            int len = m_filter.GetEntitiesCount();
            for (int i = 0; i < len; i++)
            {
                var entity = m_filter.GetEntity(i);
                ref HiveConnection connection = ref m_filter.Get1(i);

                //    Check if this entity has a task that has already been completed
                if (m_entityToTaskMap.ContainsKey(entity))
                {
                    Task<int> task = m_entityToTaskMap[entity];
                    if (task.IsCompleted)
                    {
                        m_entityToTaskMap.Remove(entity);
                        m_taskToEntityMap.Remove(task);
                        
                        //    Read the message itself
                        int messageSize = task.Result;

                        //    Make the connection stale if the message size is 0.
                        //    this means there is nothing left to receive and the user has disconnected.
                        if (messageSize == 0)
                        {
                            connection.IsStale = true;
                            continue;
                        }
                        
                        StateObject state = m_states[entity];
                        CodedInputStream stream = new CodedInputStream(state.Buffer, 0, messageSize);

                        int messageIndex = stream.ReadInt32();
                        int numMessages = stream.ReadInt32();

                        for (int j = 0; j < numMessages; j++)
                        {
                            IInputPipeline pipeline = m_pipelineManager.GetInputPipeline(messageIndex);
                            pipeline.PushMessage(stream, connection);
                        }
                    }
                    else if (task.IsCanceled || task.IsFaulted)
                    {
                        Debug.LogError("Receive message task cancelled for : " + connection.Socket.RemoteEndPoint);
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
        private EcsFilter<HiveConnection> m_connections;
        private Dictionary<HiveConnection, PipelineManager> m_managers;
        private Dictionary<HiveConnection, ReliableChannel> m_reliableChannels;
        
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
                ReliableChannel channel;
                
                bool found = m_managers.TryGetValue(connection, out manager);
                if (!found)
                {
                    Debug.LogError($"Failed to find PipelineManager for connection {connection.Socket.RemoteEndPoint}");
                    continue;
                }

                found = m_reliableChannels.TryGetValue(connection, out channel);
                if (!found)
                {
                    Debug.LogError($"Failed to find ReliableChannel for connection {connection.Socket.RemoteEndPoint}");
                    continue;
                }
                
                IReadOnlyList<IOutputPipeline> outputs = manager.GetOutputPipelines();
                
                channel.Send(outputs);
                Debug.Log("Sent Message to: " + connection.Socket.RemoteEndPoint);
            }
        }
        public void Destroy()
        {
        }
    }
}

