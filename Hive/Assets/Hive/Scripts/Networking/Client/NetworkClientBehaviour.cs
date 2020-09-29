using System.Net;
using Hive.TransportLayer.Client;
using Hive.TransportLayer.Shared;
using Hive.TransportLayer.Shared.Pipelines;
using Leopotam.Ecs;
using NetMessage;
using UnityEngine;

namespace Hive.Networking.Client
{
    public class NetworkClientBehaviour : MonoBehaviour
    {
        [SerializeField]
        private string m_ip = "127.0.0.1";
        [SerializeField]
        private int m_port = 27000;
        [SerializeField]
        private MessageLookupTable m_lookupTable;

        private NetworkClient m_client;

        // Start is called before the first frame update
        void Start()
        {
            m_client = new NetworkClient(m_lookupTable);
            
            m_client.Ecs.AddSystem<AuthenticationSystem>();
        }

        private void OnDestroy()
        {
        }
        
        [ContextMenu("Connect")]
        public void Connect()
        {
            m_client.Connect(
                new IPEndPoint(IPAddress.Parse("127.0.0.1"), 11000),
                "whuop",
                "bajs");
        }

        [ContextMenu("Disconnect")]
        public void Disconnect()
        {
            m_client.Disconnect();
        }

        // Update is called once per frame
        void Update()
        {
            m_client.Update();
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
                m_events.OnAuthenticatedCallback?.Invoke(/*response.Message.State*/);
                m_handshakeResponses.Release(response);
            }
        }

        public void Destroy()
        {
        }
    }

}

