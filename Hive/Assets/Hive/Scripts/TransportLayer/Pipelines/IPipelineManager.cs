using System.Collections;
using System.Collections.Generic;
using Google.Protobuf;
using UnityEngine;

namespace Hive.TransportLayer.Pipelines
{
    public interface IPipelineManager
    {
        byte[] MessageBuffer { get; }
        
        Pipeline<T> GetPipeline<T>() where T : IMessage;
        IPipeline GetPipeline(int typeIndex);
        IReadOnlyList<IPipeline> GetInputPipelines();
        
        
        OutputPipeline<T> GetOutputPipeline<T>() where T : IMessage;
        IOutputPipeline GetOutputPipeline(int typeIndex);
        IReadOnlyList<IOutputPipeline> GetOutputPipelines();
    }
}


