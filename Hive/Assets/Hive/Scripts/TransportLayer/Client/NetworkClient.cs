using System.Net;
using System.Net.Sockets;
using Google.Protobuf;
using Hive.TransportLayer.Client.Systems;
using Hive.TransportLayer.Shared;
using Hive.TransportLayer.Shared.Components;
using Hive.TransportLayer.Shared.Pipelines;
using Leopotam.Ecs;
using NetMessage;
using UnityEngine;

namespace Hive.TransportLayer.Client
{
    
    public class NetworkClient
    {   
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

        public EcsSystems UpdateSystem => m_updateSystems;

        private EventBoard m_events;
        public EventBoard Events
        {
            get { return m_events; }
        }
        
        /// <summary>
        /// These might be nice to implement
        /// </summary>
        public delegate void OnFailedToConnectDelegate();

        public delegate void OnRetryingConnectionDelegate();

        public ConnectionState State
        {
            get { return m_tcpSocket.State; }
        }

        private bool m_initedECS = false;
        public NetworkClient(MessageLookupTable lookupTable)
        {
            m_lookupTable = lookupTable;
            m_pipelineManager = new PipelineManager(lookupTable);
            m_tcpSocket = new TCPSocket();
            m_events = new EventBoard();
            
            m_events.OnConnectCallback += () =>
            {
                Debug.LogError("ON CONNECTED!!");
            };

            m_events.OnDisconnectCallback += () =>
            {
                Debug.LogError("ON DISCONNECTED!!");
            };

            m_events.OnAuthenticatedCallback += (ConnectionState state) =>
            {
                Debug.LogError("AUTHENTICATED WITH STATE: " + state.ToString());
            };
        }

        private void InitEcs()
        {
            
        }

        public void Connect(IPEndPoint address, string username, string password)
        {
            if (State == ConnectionState.Connected)
                return;
            
            m_tcpSocket.Socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            
            var connectSystem = new ConnectSystem();
            var authSystem = new AuthenticationSystem();
            authSystem.SetCredentials(username, password);

            m_world = new EcsWorld();
            m_updateSystems = new EcsSystems(m_world);
            m_updateSystems.Add(connectSystem);
            m_updateSystems.Add(authSystem);
            m_updateSystems.Add(new ReceiveReliableMessageSystem());
            m_updateSystems.Add(new SendPipelineMessages());
            m_updateSystems.Inject(m_tcpSocket);
            m_updateSystems.Inject(m_state);
            m_updateSystems.Inject(m_pipelineManager);
            m_updateSystems.Inject(m_world);
            m_updateSystems.Inject(m_info);
            m_updateSystems.Inject(m_events);
            
            m_updateSystems.Init();
            m_initedECS = true;
            connectSystem.Connect(address, username, password);
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
            if (m_initedECS)
                m_updateSystems.Run();
        }

        public void FixedUpdate()
        {
        }

        public void LateUpdate()
        {
        }

        public void Send(byte[] data)
        {
            m_tcpSocket.Socket.BeginSend(data, 0, data.Length, SocketFlags.None, (ar) =>
            {
                StateObject so = (StateObject)ar.AsyncState;
                int bytes = m_tcpSocket.Socket.EndSend(ar);
            }, m_state);
        }

        public class EventBoard
        {
            public delegate void OnConnectDelegate();
            public OnConnectDelegate OnConnectCallback { get; set; }

            public delegate void OnDisconnectDelegate();
            public OnDisconnectDelegate OnDisconnectCallback { get; set; }
        
            public delegate void OnAuthenticatedDelegate(ConnectionState state);

            public OnAuthenticatedDelegate OnAuthenticatedCallback { get; set; }
        }
    }
}


