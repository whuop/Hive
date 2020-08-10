using System.Collections;
using System.Collections.Generic;
using Google.Protobuf;
using UnityEngine;

namespace Hive.TransportLayer.Shared.Pipelines
{
    public interface IPipelineManager
    {
        byte[] MessageBuffer { get; }
        
        InputPipeline<T> GetInputPipeline<T>() where T : IMessage;
        IInputPipeline GetInputPipeline(int typeIndex);
        IReadOnlyList<IInputPipeline> GetInputPipelines();
        
        
        OutputPipeline<T> GetOutputPipeline<T>() where T : IMessage;
        IOutputPipeline GetOutputPipeline(int typeIndex);
        IReadOnlyList<IOutputPipeline> GetOutputPipelines();
    }
}


