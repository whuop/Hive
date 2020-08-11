using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Google.Protobuf;
using Hive.TransportLayer.Shared;
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
                    if (task.IsCompleted || task.IsCanceled || task.IsFaulted)
                    {
                        m_entityToTaskMap.Remove(entity);
                        m_taskToEntityMap.Remove(task);
                        
                        //    Read the message itself
                        int messageSize = task.Result;

                        StateObject state = m_states[entity];
                        CodedInputStream stream = new CodedInputStream(state.Buffer, 0, messageSize);

                        int messageIndex = stream.ReadInt32();
                        int numMessages = stream.ReadInt32();

                        for (int j = 0; j < numMessages; j++)
                        {
                            IInputPipeline pipeline = m_pipelineManager.GetInputPipeline(messageIndex);
                            pipeline.PushMessage(stream, state.WorkSocket);
                        }
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
}

