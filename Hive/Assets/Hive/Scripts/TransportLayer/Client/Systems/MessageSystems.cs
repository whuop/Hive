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

namespace Hive.TransportLayer.Client.Systems
{
    public class ConnectSystem : IEcsInitSystem, IEcsRunSystem, IEcsDestroySystem
    {
        private SystemInformation m_info;
        private EcsWorld m_world;
        private ReliableChannel m_reliableChannel;

        private NetworkClient.EventBoard m_events;
        private Task m_task = null;
        
        public void Init()
        {   
        }

        public void Connect(IPEndPoint address, string username, string password)
        {
            m_task = m_reliableChannel.Socket.ConnectAsync(address);
        }

        public void Run()
        {
            if (m_task == null)
                return;

            if (m_task.IsCompleted)
            {
                var entity = m_world.NewEntity();
                ref HiveConnection connection = ref entity.Get<HiveConnection>();
                connection.Socket = m_reliableChannel.Socket;
                m_events.OnConnectCallback?.Invoke();
            }
            else if (m_task.IsCanceled)
            {
                m_events.OnDisconnectCallback?.Invoke();
            }
            else if (m_task.IsFaulted)
            {
                m_events.OnDisconnectCallback?.Invoke();
            }
            m_task = null;
        }

        public void Destroy()
        {
        }
    }
    
    public class ReceiveReliableMessageSystem : IEcsInitSystem, IEcsRunSystem, IEcsDestroySystem
    {
        private SystemInformation m_systemInfo;
        private IPipelineManager m_pipelineManager;
        private ReliableChannel m_reliableChannel;
        
        private List<Task<int>> m_receiveMessageTasks = new List<Task<int>>();
        private List<EcsEntity> m_queuedEntities = new List<EcsEntity>();

        private EcsFilter<HiveConnection> m_filter;
        
        private Task<int> m_task;
        private StateObject m_state;
        
        public void Init()
        {
            m_state = new StateObject()
            {
                WorkSocket = m_reliableChannel.Socket,
                Buffer = new byte[1024]
            };
            m_state.Segment = new ArraySegment<byte>(m_state.Buffer);
            
            m_task = m_state.WorkSocket.ReceiveAsync(m_state.Segment, SocketFlags.None);
        }

        public void Run()
        {
            if (m_task.IsCompleted || m_task.IsCanceled || m_task.IsFaulted)
            {
                //    Read the message itself
                int messageSize = m_task.Result;

                if (messageSize == 0)
                {
                    m_reliableChannel.IsStale = true;
                    return;
                }
                
                CodedInputStream stream = new CodedInputStream(m_state.Buffer, 0, messageSize);

                int messageIndex = stream.ReadInt32();
                int numMessages = stream.ReadInt32();

                for (int j = 0; j < numMessages; j++)
                {
                    IInputPipeline pipeline = m_pipelineManager.GetInputPipeline(messageIndex);
                    pipeline.PushMessage(stream, m_filter.Get1(0));
                }
                m_task = m_state.WorkSocket.ReceiveAsync(m_state.Segment, SocketFlags.None);
            }
        }

        public void Destroy()
        {
        }
    }

    public class SendPipelineMessages : IEcsInitSystem, IEcsRunSystem, IEcsDestroySystem
    {
        private ReliableChannel m_reliableChannel;
        private IPipelineManager m_pipelineManager;

        private IReadOnlyList<IInputPipeline> m_inputPipelines;
        private IReadOnlyList<IOutputPipeline> m_outputPipelines;

        public void Init()
        {
            m_inputPipelines = m_pipelineManager.GetInputPipelines();
            m_outputPipelines = m_pipelineManager.GetOutputPipelines();
        }

        public void Run()
        {
            m_reliableChannel.Send(m_outputPipelines);
        }

        public void Destroy()
        {
        }
    }
}
