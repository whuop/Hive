using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Networking.Transport;
using UnityEngine;

namespace GameData.Networking.Server.Components
{
    public struct NetConnection : IComponentData
    {
        public NetworkConnection Connection;
    }

}

