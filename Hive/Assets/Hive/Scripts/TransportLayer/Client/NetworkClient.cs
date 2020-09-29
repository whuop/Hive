using System.Net;
using System.Net.Sockets;
using Hive.TransportLayer.Client.Systems;
using Hive.TransportLayer.Shared;
using Hive.TransportLayer.Shared.Channels;
using Hive.TransportLayer.Shared.Components;
using Hive.TransportLayer.Shared.ECS;
using Hive.TransportLayer.Shared.Pipelines;
using UnityEngine;

namespace Hive.TransportLayer.Client
{
    public class NetworkClient
    {   
        private SystemInformation m_info = new SystemInformation(SystemType.Client);
        private StateObject m_state = new StateObject();
        private MessageLookupTable m_lookupTable;

        private ReliableChannel m_reliableChannel;

        private IPipelineManager m_pipelineManager;
        public IPipelineManager PipelineManager
        {
            get { return m_pipelineManager; }
        }

        private EcsManager m_ecsManager;
        public EcsManager Ecs
        {
            get { return m_ecsManager; }
        }
        
        private EventBoard m_events;
        public EventBoard Events
        {
            get { return m_events; }
        }

        private string m_tag = "[Client]";
        
        /// <summary>
        /// These might be nice to implement
        /// </summary>
        public delegate void OnFailedToConnectDelegate();

        public delegate void OnRetryingConnectionDelegate();

        public NetworkClient(MessageLookupTable lookupTable)
        {
            m_ecsManager = new EcsManager();
            m_lookupTable = lookupTable;
            m_pipelineManager = new PipelineManager(lookupTable);
            m_events = new EventBoard();
            
            m_events.OnConnectCallback += () =>
            {
                Debug.LogError("ON CONNECTED!!");
            };

            m_events.OnDisconnectCallback += () =>
            {
                Debug.LogError("ON DISCONNECTED!!");
            };

            m_events.OnAuthenticatedCallback += () =>
            {
                Debug.LogError("AUTHENTICATED WITH STATE: " /*+ state.ToString()*/);
            };
            
            m_reliableChannel = new ReliableChannel();
            
            InjectEcsData();
        }

        private void InjectEcsData()
        {
            m_ecsManager.AddSystem<ConnectSystem>();
            m_ecsManager.AddSystem<ReceiveReliableMessageSystem>();
            m_ecsManager.AddSystem<SendPipelineMessages>();
            
            m_ecsManager.AddInject(m_reliableChannel);
            m_ecsManager.AddInject(m_state);
            m_ecsManager.AddInject(m_pipelineManager);
            m_ecsManager.AddInject(m_info);
            m_ecsManager.AddInject(m_events);
        }

        public void Connect(IPEndPoint address, string username, string password)
        {
            m_reliableChannel.GenerateSocket(address.AddressFamily);
            
            Ecs.Startup();
            var connectSystem = Ecs.GetSystem<ConnectSystem>();
            connectSystem.Connect(address, username, password);
        }

        public void Disconnect()
        {
            var args = new SocketAsyncEventArgs()
            {
                DisconnectReuseSocket = false,
                RemoteEndPoint = m_reliableChannel.Socket.RemoteEndPoint,
                UserToken = m_reliableChannel
            };
            args.Completed += (sender, eventArgs) =>
            {
                Debug.Log($"{m_tag} Disconnected from the server!");
                m_events.OnDisconnectCallback?.Invoke();
                m_reliableChannel.Teardown();
            };

            m_reliableChannel.Socket.DisconnectAsync(args);
        }
        
        public void Update()
        {
            if (m_reliableChannel.Socket == null)
                return;
            
            if (!Ecs.IsInitialized)
                return;
            
            Ecs.Update();
            if (!IsActiveConnection(m_reliableChannel))
            {
                Disconnect();
            }
        }

        private bool IsActiveConnection(Hive.TransportLayer.Shared.Channels.IChannel channel)
        {
            try
            {
                if (!channel.Socket.Connected || channel.IsStale)
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

        public class EventBoard
        {
            public delegate void OnConnectDelegate();
            public OnConnectDelegate OnConnectCallback { get; set; }

            public delegate void OnDisconnectDelegate();
            public OnDisconnectDelegate OnDisconnectCallback { get; set; }
        
            public delegate void OnAuthenticatedDelegate();

            public OnAuthenticatedDelegate OnAuthenticatedCallback { get; set; }
        }
    }
}


