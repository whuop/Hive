using System;
using System.Collections.Generic;
using Google.Protobuf;
using Hive.TransportLayer.Shared;

namespace Hive.TransportLayer.Shared.Pipelines
{
    public class PipelineManager : IPipelineManager
    {
        //    Input Pipelines
        private Dictionary<Type, IInputPipeline> m_pipelinesTypeMap;
        private Dictionary<int, IInputPipeline> m_pipelinesIntMap;
        private List<IInputPipeline> m_inputPipelines;

        //    Output Pipelines
        private Dictionary<Type, IOutputPipeline> m_outputPipelinesTypeMap;
        private Dictionary<int, IOutputPipeline> m_outputPipelinesIntMap;
        private List<IOutputPipeline> m_outputPipelines;

        private CodedOutputStream m_outputStream;
        public CodedOutputStream OutputStream
        {
            get { return m_outputStream; }
        }
        
        private MessageLookupTable m_lookupTable;

        private byte[] m_messageBuffer;

        public byte[] MessageBuffer => m_messageBuffer;

        public const int BYTES_PER_PIPELINE = 1024;
        
        public PipelineManager(MessageLookupTable lookupTable)
        {
            m_lookupTable = lookupTable;
            m_lookupTable.Warmup();
            InitializePipelines();
            
            m_outputStream = new CodedOutputStream(m_messageBuffer);
        }

        private void InitializePipelines()
        {
            m_pipelinesTypeMap = new Dictionary<Type,IInputPipeline>();
            m_pipelinesIntMap = new Dictionary<int, IInputPipeline>();
            m_inputPipelines = new List<IInputPipeline>();
            
            m_outputPipelinesTypeMap = new Dictionary<Type, IOutputPipeline>();
            m_outputPipelinesIntMap = new Dictionary<int, IOutputPipeline>();
            m_outputPipelines = new List<IOutputPipeline>();

            int pipelineCount = m_lookupTable.TypeDescriptors.Count;
            
            //    Initialize buffer size based on number of pipelines
            m_messageBuffer = new byte[BYTES_PER_PIPELINE * pipelineCount];

            int i = 0;
            foreach (var kvp in m_lookupTable.TypeDescriptors)
            {
                var pipelineType = typeof(InputPipeline<>).MakeGenericType(kvp.Key);
                var pipeline = (IInputPipeline)Activator.CreateInstance(pipelineType);

                var outputPipelineType = typeof(OutputPipeline<>).MakeGenericType(kvp.Key);
                var outputPipeline = (IOutputPipeline) Activator.CreateInstance(outputPipelineType);
                
                pipeline.Initialize(m_lookupTable, kvp.Value, m_lookupTable.TypeToIntMap[kvp.Key]);
                m_pipelinesTypeMap.Add(kvp.Key, pipeline);
                m_pipelinesIntMap.Add(pipeline.TypeIndex,  pipeline);
                m_inputPipelines.Add(pipeline);

                var bufferSegment = new ArraySegment<byte>(m_messageBuffer, BYTES_PER_PIPELINE * i, BYTES_PER_PIPELINE);
                outputPipeline.Initialize(m_lookupTable, kvp.Value, m_lookupTable.TypeToIntMap[kvp.Key]);
                m_outputPipelinesTypeMap.Add(kvp.Key, outputPipeline);
                m_outputPipelinesIntMap.Add(pipeline.TypeIndex, outputPipeline);
                m_outputPipelines.Add(outputPipeline);
                i++;
            }
        }
        
        public InputPipeline<T> GetInputPipeline<T>() where T : IMessage
        {
            Type type = typeof(T);
            if (!m_pipelinesTypeMap.ContainsKey(type))
                return null;
            return (InputPipeline<T>) m_pipelinesTypeMap[type];
        }

        public IInputPipeline GetInputPipeline(int typeIndex)
        {
            if (!m_pipelinesIntMap.ContainsKey(typeIndex))
                return null;
            return m_pipelinesIntMap[typeIndex];
        }

        public IReadOnlyList<IInputPipeline> GetInputPipelines()
        {
            return m_inputPipelines;
        }

        public OutputPipeline<T> GetOutputPipeline<T>() where T : IMessage
        {
            Type type = typeof(T);
            if (!m_outputPipelinesTypeMap.ContainsKey(type))
                return null;
            return (OutputPipeline<T>)m_outputPipelinesTypeMap[type];
        }

        public IOutputPipeline GetOutputPipeline(int typeIndex)
        {
            if (!m_outputPipelinesIntMap.ContainsKey(typeIndex))
                return null;
            return m_outputPipelinesIntMap[typeIndex];
        }

        public IReadOnlyList<IOutputPipeline> GetOutputPipelines()
        {
            return m_outputPipelines;
        }
    }
}


