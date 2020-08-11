using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Google.Protobuf;
using Hive.TransportLayer.Shared;
using Hive.TransportLayer.Shared.Components;
using Hive.TransportLayer.Shared.Pipelines;
using Leopotam.Ecs;
using NetMessage;
using UnityEngine;

namespace Hive.TransportLayer.Client.Systems
{
    public class ConnectSystem : IEcsInitSystem, IEcsRunSystem, IEcsDestroySystem
    {
        private SystemInformation m_info;
        private EcsWorld m_world;
        private TCPSocket m_socket;

        private NetworkClient.EventBoard m_events;
        private Task m_task = null;
        
        public void Init()
        {   
        }

        public void Connect(IPEndPoint address, string username, string password)
        {
            m_task = m_socket.Socket.ConnectAsync(address);
        }

        public void Run()
        {
            if (m_task == null)
                return;

            if (m_task.IsCompleted)
            {
                var entity = m_world.NewEntity();
                ref HiveConnection connection = ref entity.Get<HiveConnection>();
                connection.Socket = m_socket.Socket;
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
    
    public class AuthenticationSystem : IEcsInitSystem, IEcsRunSystem, IEcsDestroySystem
    {
        private IPipelineManager m_pipelines;

        private OutputPipeline<HandshakeRequest> m_handshakeRequests;
        private InputPipeline<HandshakeResponse> m_handshakeResponses;

        private NetworkClient.EventBoard m_events;
        
        private string m_username = string.Empty;
        private string m_password = string.Empty;
        
        public void Init()
        {
            m_handshakeRequests = m_pipelines.GetOutputPipeline<HandshakeRequest>();
            m_handshakeResponses = m_pipelines.GetInputPipeline<HandshakeResponse>();
            
            m_handshakeRequests.PushMessage(new HandshakeRequest()
            {
                Username = m_username,
                Password = m_password
            });
        }

        public void SetCredentials(string username, string password)
        {
            m_username = username;
            m_password = password;
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
                m_events.OnAuthenticatedCallback?.Invoke(response.Message.State);
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
            
            m_task = m_state.WorkSocket.ReceiveAsync(m_state.Segment, SocketFlags.None);
        }

        public void Run()
        {
            if (m_task.IsCompleted || m_task.IsCanceled || m_task.IsFaulted)
            {
                //    Read the message itself
                int messageSize = m_task.Result;
                
                CodedInputStream stream = new CodedInputStream(m_state.Buffer, 0, messageSize);

                int messageIndex = stream.ReadInt32();
                int numMessages = stream.ReadInt32();

                for (int j = 0; j < numMessages; j++)
                {
                    IInputPipeline pipeline = m_pipelineManager.GetInputPipeline(messageIndex);
                    pipeline.PushMessage(stream, m_state.WorkSocket);
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
        private TCPSocket m_tcpSocket;
        private IPipelineManager m_pipelineManager;

        private IReadOnlyList<IInputPipeline> m_inputPipelines;
        private IReadOnlyList<IOutputPipeline> m_outputPipelines;

        private CodedOutputStream m_outputStream;
        
        public void Init()
        {
            m_inputPipelines = m_pipelineManager.GetInputPipelines();
            m_outputPipelines = m_pipelineManager.GetOutputPipelines();
            m_outputStream = new CodedOutputStream(m_pipelineManager.MessageBuffer);
        }

        public void Run()
        {
            m_outputStream.Flush();
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
