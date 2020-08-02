using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace GameData.Networking.Server.Components
{
    public struct NetEntityID : IComponentData
    {
        public uint ID;
    }
}


