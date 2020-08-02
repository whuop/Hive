using GameData.Networking.Server.Components;
using Google.Protobuf.WellKnownTypes;
using NetMessage;
using System.Collections;
using Unity.Assertions;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Networking.Transport;
using UnityEngine;

namespace GameData.Networking.Server.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public class ServerMessageSystem : JobComponentSystem
    {
        private NetworkDriver m_driver;
        public NetworkDriver NetworkDriver { get { return m_driver; } }
        private EntityQuery m_query;

        private EntityCommandBufferSystem m_cmd;
        
        private EntityCommandBuffer m_buffer;
        
        private JobHandle m_handle;

        private NativeArray<Entity> m_entities;
        private NativeArray<NetConnection> m_connections;

        private MonoBehaviour m_sceneContext;

        private bool m_listenForConnections = true;

        protected override void OnCreate()
        {
            m_driver = NetworkDriver.Create();
            var endpoint = NetworkEndPoint.AnyIpv4;
            endpoint.Port = 9000;
            if (m_driver.Bind(endpoint) != 0)
            {
                Debug.LogError("Failed to bind to port 9000");
            }
            else
            {
                m_driver.Listen();
            }

            m_cmd = this.World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();

            var queryDesc = new EntityQueryDesc
            {
                All = new ComponentType[] { typeof(NetConnection) }
            };
            m_query = GetEntityQuery(queryDesc);
            FetchData();
        }

        public void StartListening(MonoBehaviour sceneContext)
        {
            m_sceneContext = sceneContext;
            m_sceneContext.StartCoroutine(ListenForNewConnections());
        }

        private IEnumerator ListenForNewConnections()
        {;
            yield return null;
            while(m_listenForConnections)
            {
                m_driver.ScheduleUpdate().Complete();
                //  Accept new connections
                NetworkConnection c;
                while ((c = m_driver.Accept()) != default(NetworkConnection) && m_listenForConnections)
                {
                    var cmd = m_cmd.CreateCommandBuffer();
                    var entity = cmd.CreateEntity();
                    cmd.AddComponent(entity, new NetConnection { Connection = c });
                    m_listenForConnections = false;
                }
                yield return null;
            }
        }

        protected override void OnDestroy()
        {
            m_driver.Dispose();
        }

        private void FetchData()
        {
            if (m_entities.IsCreated)
            {
                m_entities.Dispose();
                m_connections.Dispose();
            }

            m_entities = m_query.ToEntityArray(Allocator.TempJob);
            m_connections = m_query.ToComponentDataArray<NetConnection>(Allocator.TempJob);
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            //Debug.Log("Running Network Jobs");
            FetchData();
            m_buffer = m_cmd.CreateCommandBuffer();
            
            //  Accept new connections job
            var connectionJob = new AcceptConnectionsJob
            {
                Driver = m_driver,
                Cmd = m_buffer.ToConcurrent()
            };

            //  Cleanup old connections job
            var cleanupJob = new CleanupConnectionsJob
            {
                Cmd = m_buffer,
                Entities = m_entities,
                Connections = m_connections
            };

            //  Process new messages job
            var messageJob = new UpdateConnectionsJob
            {
                Driver = m_driver.ToConcurrent(),
                Connections = m_connections
            };

            var driverHandle = m_driver.ScheduleUpdate(inputDeps);
            var connectionHandle = connectionJob.Schedule(driverHandle);
            var cleanupHandle = cleanupJob.Schedule(connectionHandle);
            var messageHandle = messageJob.Schedule(m_entities.Length, 16,cleanupHandle);
            m_cmd.AddJobHandleForProducer(messageHandle);

            return messageHandle;
        }

        [BurstCompile]
        struct AcceptConnectionsJob : IJob
        {
            public NetworkDriver Driver;
            public EntityCommandBuffer.Concurrent Cmd;

            public void Execute()
            {
                //  Accept new connections
                NetworkConnection c;
                while ((c = Driver.Accept()) != default(NetworkConnection))
                {
                    var entity = Cmd.CreateEntity(0);
                    Cmd.AddComponent(0, entity, new NetConnection { Connection = c });
                    Debug.Log("Accepted Connection");
                }
            }
        }

        [BurstCompile]
        struct CleanupConnectionsJob : IJob
        {
            public EntityCommandBuffer Cmd;
            public NativeArray<Entity> Entities;
            public NativeArray<NetConnection> Connections;

            public void Execute()
            {
                //  Clean up connections
                for (int i = 0; i < Entities.Length; i++)
                {
                    var entity = Entities[i];
                    var connection = Connections[i];

                    //  If the connection is no longer valid and created, remove it the whole entity.
                    if (!connection.Connection.IsCreated)
                    {
                        Debug.Log("Cleaned Up Connection");
                        Cmd.DestroyEntity(entity);
                    }
                }
            }
        }

        [BurstCompile]
        private struct UpdateConnectionsJob : IJobParallelFor
        {
            public NetworkDriver.Concurrent Driver;
            public NativeArray<NetConnection> Connections;

            public void Execute(int index)
            {
                DataStreamReader stream;
                Assert.IsTrue(Connections[index].Connection.IsCreated);

                NetworkEvent.Type cmd;
                while ((cmd = Driver.PopEventForConnection(Connections[index].Connection, out stream)) != NetworkEvent.Type.Empty)
                {
                    if (cmd == NetworkEvent.Type.Connect)
                    {
                        Debug.Log("[Server] - Client connected!");
                    }
                    else if (cmd == NetworkEvent.Type.Data)
                    {
                        Debug.Log("[Server] - Receiving Handshake");
                        NativeArray<byte> bytes = new NativeArray<byte>(stream.Length, Allocator.Temp);
                        stream.ReadBytes(bytes);
                        Any any = Any.Parser.ParseFrom(bytes.ToArray());
                        
                        
                        if (any.Is(HandshakeRequest.Descriptor))
                        {
                            HandshakeRequest request = any.Unpack<HandshakeRequest>();
                            Debug.Log("Handshake: " + request.Message);
                        }
                    }
                    else if (cmd == NetworkEvent.Type.Disconnect)
                    {
                        Debug.Log("Client disconnected from server");
                    }
                }
            }
        }
    }
}


