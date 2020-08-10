using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using UnityEngine;

namespace Hive.TransportLayer.Pipelines
{
    public interface IPipeline
    {
        int TypeIndex { get; }

        void Initialize(MessageLookupTable lookupTable, MessageDescriptor descriptor, int typeIndex);

        void PushMessage(CodedInputStream stream, Socket sender);
        
        int MessageSize { get; }
    }

    public interface IPipeline<T> : IPipeline where T : IMessage
    {
    }

    public class Pipeline<T> : IPipeline<T> where T : IMessage
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
        
        public Pipeline()
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

    public interface IOutputPipeline
    {
        int TypeIndex { get; }

        int MessageCount
        {
            get;
        }
        
        void Initialize(MessageLookupTable lookupTable, MessageDescriptor descriptor, int typeIndex);
        void PackMessages(CodedOutputStream stream);
        
    }

    public interface IOutputPipeline<T> : IOutputPipeline where T : IMessage
    {
        void PushMessage(T message);
    }
    
    public class OutputPipeline<T> : IOutputPipeline<T> where T : IMessage
    {
        private int m_typeIndex;
        public int TypeIndex
        {
            get => m_typeIndex;
        }

        private MessageLookupTable m_lookupTable;
        private MessageDescriptor m_descriptor;
        private ConcurrentQueue<T> m_messageQueue;
        private CodedOutputStream m_outputStream;
        
        public int MessageCount
        {
            get { return m_messageQueue.Count; }
        }
        
        public void Initialize(MessageLookupTable lookupTable, MessageDescriptor descriptor, int typeIndex)
        {
            m_typeIndex = typeIndex;
            m_lookupTable = lookupTable;
            m_descriptor = descriptor;
            m_messageQueue = new ConcurrentQueue<T>();
        }

        public void PushMessage(T message)
        {
            m_messageQueue.Enqueue(message);
        }

        public void PackMessages(CodedOutputStream stream)
        {
            if (m_messageQueue.Count == 0)
                return;
            
            //    Pack Header 1 int for message index, 1 int for num messages
            stream.WriteInt32(m_typeIndex);
            stream.WriteInt32(m_messageQueue.Count);
            
            Debug.Log("Packing Message!!");
            while (m_messageQueue.Count > 0)
            {
                T message;
                bool found = m_messageQueue.TryDequeue(out message);

                if (!found)
                    continue;
                
                stream.WriteMessage(message);
            }
        }
    }
}