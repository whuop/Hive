using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Hive.TransportLayer.Shared;
using UnityEngine;

namespace Hive.TransportLayer.Shared.Pipelines
{
    public interface IInputPipeline
    {
        int TypeIndex { get; }

        void Initialize(MessageLookupTable lookupTable, MessageDescriptor descriptor, int typeIndex);

        void PushMessage(CodedInputStream stream, Socket sender);
        
        int MessageSize { get; }
    }

    public interface IInputPipeline<T> : IInputPipeline where T : IMessage
    {
    }

    public class InputPipeline<T> : IInputPipeline<T> where T : IMessage
    {
        private ConcurrentQueue<MessageObject<T>> m_messageQueue;
        private MessageLookupTable m_messageLookupTable;
        private MessageDescriptor m_descriptor;

        private ConcurrentQueue<MessageObject<T>> m_messagePool;

        public class MessageObject<T> where T : IMessage
        {
            public Socket Sender;
            public T Message;
        }
        
        private int m_typeIndex = -1;
        public int TypeIndex
        {
            get { return m_typeIndex; }
        }
        
        public int Count
        {
            get { return m_messageQueue.Count; }
        }
        
        public int MessageSize { get; private set; }
        
        public InputPipeline()
        {
            var dummyMessage = Activator.CreateInstance<T>();
            MessageSize = dummyMessage.CalculateSize();
        }

        public void PushMessage(CodedInputStream stream, Socket sender)
        {
            var pooledMessage = GetNextPooledMessage();
            stream.ReadMessage(pooledMessage.Message);
            pooledMessage.Sender = sender;
            Debug.LogError("PUSHED MESSAGE!!! " + pooledMessage.Message);
            m_messageQueue.Enqueue(pooledMessage);
        }

        public MessageObject<T> GetNextPooledMessage()
        {
            if (m_messagePool.Count == 0)
            {
                var message = new MessageObject<T>();
                message.Message = Activator.CreateInstance<T>();
                m_messagePool.Enqueue(message);
            }

            MessageObject<T> result;
            m_messagePool.TryDequeue(out result);
            return result;
        }

        public void Release(MessageObject<T> pooledInstance)
        {
            m_messagePool.Enqueue(pooledInstance);
        }
        
        public MessageObject<T> PopMessageTyped()
        {
            MessageObject<T> result;
            m_messageQueue.TryDequeue(out result);
            return result;
        }

        public void Initialize(MessageLookupTable lookupTable, MessageDescriptor descriptor, int typeIndex)
        {
            m_messageQueue = new ConcurrentQueue<MessageObject<T>>();
            m_messagePool = new ConcurrentQueue<MessageObject<T>>();
            m_messageLookupTable = lookupTable;
            m_descriptor = descriptor;
            m_typeIndex = typeIndex;
        }
    }

    
}