using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Hive.TransportLayer.Server;
using Hive.TransportLayer.Shared;
using Hive.TransportLayer.Shared.Pipelines;
using Leopotam.Ecs;
using NetMessage;
using UnityEngine;

namespace Hive.Networking.Server
{
    public class NetworkServerBehaviour : MonoBehaviour
    {
        [SerializeField]
        private MessageLookupTable m_lookupTable;
        private NetworkServer m_server;
        void Awake()
        {
            m_server = new NetworkServer(m_lookupTable);
            m_server.UpdateSystem.Add(new AuthSystem());
            m_server.UpdateSystem.Inject(m_server);
            m_server.Start();
        }
        
        // Start is called before the first frame update
        void Start()
        {
        }

        private void OnDestroy()
        {
            m_server.Stop();
        }

        // Update is called once per frame
        void Update()
        {
            m_server.Update();
        }

        private void LateUpdate()
        {
            m_server.LateUpdate();
        }

        private void FixedUpdate()
        {
            m_server.FixedUpdate();
        }
    }

    public class AuthSystem : IEcsInitSystem, IEcsDestroySystem, IEcsRunSystem
    {
        [SerializeField]
        private EcsWorld m_world;
        [SerializeField]
        private NetworkServer m_server;

        private InputPipeline<HandshakeRequest> m_handshakes;
        
        private List<EndPoint> m_acceptedConnections = new List<EndPoint>();
        private Dictionary<Socket, PipelineManager> m_pipelineManagers;
        
        public void Init()
        {
            m_handshakes = m_server.PipelineManager.GetInputPipeline<HandshakeRequest>();
        }

        public void Destroy()
        {
        }

        public void Run()
        {
            if (m_handshakes == null)
                return;
            
            if (m_handshakes.Count > 0)
            {
                var message = m_handshakes.PopMessageTyped();
                var handshake = message.Message;
                var output = m_pipelineManagers[message.Sender].GetOutputPipeline<HandshakeResponse>();
                
                bool authSuccessful = AuthConnection(handshake.Username, handshake.Password, message.Sender);

                if (authSuccessful)
                {
                    Debug.Log(
                        $"User trying to log in with Username {handshake.Username} and Password {handshake.Password} from Address {message.Sender.RemoteEndPoint}");

                    output.PushMessage(new HandshakeResponse
                    {
                        State = ConnectionState.Connected
                    });
                }
                else
                {
                    output.PushMessage(new HandshakeResponse
                    {
                        State = ConnectionState.Disconnected
                    });
                }
                
                m_handshakes.Release(message);
            }
        }

        private bool AuthConnection(string username, string password, Socket sender)
        {
            if (m_acceptedConnections.Contains(sender.RemoteEndPoint))
            {
                Debug.LogError($"[Server] A connection from {sender.RemoteEndPoint} has already been established! Cannot accept connection again.");
                return false;
            }
        
            if (username == "whuop" && password == "bajs")
            {
                Debug.Log($"[Server] Authentication successful for {username}::{sender.RemoteEndPoint}");
                m_acceptedConnections.Add(sender.RemoteEndPoint);
                return true;
            }

            return false;
        }
    }
}