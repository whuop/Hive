using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using UnityEngine;

namespace Hive.TransportLayer.Shared.Pipelines
{
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
