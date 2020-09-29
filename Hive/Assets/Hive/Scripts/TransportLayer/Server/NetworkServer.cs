using System;
using System.Collections.Generic;
using System.Net.Sockets;
using Hive.TransportLayer.Server.Components;
using Hive.TransportLayer.Server.Systems;
using Hive.TransportLayer.Shared;
using Hive.TransportLayer.Shared.Channels;
using Hive.TransportLayer.Shared.Components;
using Hive.TransportLayer.Shared.Pipelines;
using Leopotam.Ecs;
using UnityEditor.MemoryProfiler;
using UnityEngine;

namespace Hive.TransportLayer.Server
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

        private ConnectionHandlerSystem m_connectionHandlerSystem;

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

            m_connectionHandlerSystem = new ConnectionHandlerSystem();
            
            m_world = new EcsWorld();
            m_updateSystems = new EcsSystems(m_world);
            m_updateSystems.Add(new CleanupConnectionsSystem());
            m_updateSystems.Add(new AcceptConnectionsSystem());
            m_updateSystems.Add(new MultiReceiveReliableMessageSystem());
            m_updateSystems.Add(new MultiSendPipelineMessages());
            m_updateSystems.Add(m_connectionHandlerSystem);
            m_updateSystems.Inject(m_world);
            m_updateSystems.Inject(m_tcpSocket);
            m_updateSystems.Inject(m_state);
            m_updateSystems.Inject(m_pipelineManager);
            m_updateSystems.Inject(m_info);
            m_updateSystems.Inject(m_lookupTable);
            m_updateSystems.Inject(new Dictionary<HiveConnection, PipelineManager>());
            m_updateSystems.Inject(new Dictionary<HiveConnection, ReliableChannel>());
            
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

        public void KickUser(string userName)
        {
            ref var connection = ref m_connectionHandlerSystem.FindConnectionByUsername(userName);
            if (connection.Username == null || connection.Username == string.Empty)
            {
                Debug.LogError($"{m_info.GetTag()} Could not kick user {userName}. User could not be found!");
                return;
            }
            
            connection.Socket.Shutdown(SocketShutdown.Both);
            connection.Socket.Close();
            connection.IsStale = true;
            Debug.Log($"{m_info.GetTag()} Succesfully kicked user {connection.Username}.");
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
    }

    public class ConnectionHandlerSystem : IEcsInitSystem, IEcsRunSystem, IEcsDestroySystem
    {
        private EcsFilter<HiveConnection> m_filter;

        public HiveConnection NullConnection = default(HiveConnection);
        
        public void Init()
        {
            
        }

        public EcsFilter<HiveConnection> GetAllConnectionsFilter()
        {
            return m_filter;
        }

        public ref HiveConnection FindConnectionByUsername(string username)
        {
            for (int i = 0; i < m_filter.GetEntitiesCount(); i++)
            {
                var connection = m_filter.Get1(i);
                if (username == connection.Username)
                    return ref m_filter.Get1Ref(i).Unref();
            }
            return ref NullConnection;
        }

        public void Run()
        {
        }

        public void Destroy()
        {
        }
    }
}