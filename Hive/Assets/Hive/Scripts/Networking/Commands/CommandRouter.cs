using System.Collections;
using System.Collections.Generic;
using GameData.Networking.Attributes;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Google.Protobuf.WellKnownTypes;
using NetMessage;
using UnityEngine;
/*
namespace GameData.Networking.Commands
{
    public class CommandRouter
    {
        private Dictionary<MessageDescriptor, INetworkCommand
        
        public CommandRouter()
        {
        }
    }
    
    public interface INetworkCommand<T> where T : IMessage
    {
        MessageDescriptor MessageDescriptor { get;}
        void Execute(Vault.Vault vault, T message);
    }
    
    public class HandshakeCommand : INetworkCommand<HandshakeRequest>
    {
        public MessageDescriptor MessageDescriptor {
            get
            {
                return HandshakeRequest.Descriptor;
            }
        }
        
        public void Execute(Vault.Vault vault, HandshakeRequest message)
        {
        }
    }
}
*/