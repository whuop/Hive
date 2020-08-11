using System;
using System.Collections.Generic;
using System.Net.Sockets;
using Hive.TransportLayer.Server.Systems;
using Hive.TransportLayer.Shared;
using Hive.TransportLayer.Shared.Components;
using Hive.TransportLayer.Shared.Pipelines;
using Leopotam.Ecs;

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
    }
}