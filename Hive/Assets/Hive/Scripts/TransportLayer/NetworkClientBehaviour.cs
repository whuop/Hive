using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Runtime.Remoting.Contexts;
using Hive.TransportLayer.Pipelines;
using Leopotam.Ecs;
using NetMessage;
using UnityEngine;

namespace Hive.TransportLayer
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
            //m_socket = new UdpSocket(m_lookupTable);
            m_client = new NetworkClient(m_lookupTable);
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
            if (m_client.State != NetworkClient.ConnectionState.Connected)
                return;
            
            m_client.Update();
            /*if (Input.GetKeyUp(KeyCode.Space))
            {
                m_client.Send(new HandshakeRequest
                {
                    Username = "whuop",
                    Password = "bajs"
                });
            }*/
        }

        private void FixedUpdate()
        {
            if (m_client.State != NetworkClient.ConnectionState.Connected)
                return;
            m_client.FixedUpdate();
        }

        private void LateUpdate()
        {
            if (m_client.State != NetworkClient.ConnectionState.Connected)
                return;
            m_client.LateUpdate();
        }
    }

}

