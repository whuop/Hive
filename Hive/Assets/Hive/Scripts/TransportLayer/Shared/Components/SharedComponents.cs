using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using UnityEngine;

namespace Hive.TransportLayer.Shared.Components
{
    public struct HiveConnection
    {
        public Socket Socket;
    }
    
    public class SystemInformation
    {
        private SystemType m_systemType;
        
        public SystemInformation(SystemType systemType)
        {
            m_systemType = systemType;
        }

        public string GetTag()
        {
            return $"[{m_systemType.ToString()}] ";
        }
    }
    
    public enum SystemType : int
    {
        Server = 0,
        Client = 1
    }
}


