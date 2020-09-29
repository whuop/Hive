using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using UnityEngine;

namespace Hive.TransportLayer.Shared.Components
{
    public struct HiveConnection
    {
        public Socket Socket;
        public string Username;
        public uint SessionID;

        public bool IsStale;

        public override bool Equals(object obj)
        {
            Debug.LogError($"Comparing {this.SessionID} || {((HiveConnection)obj).SessionID}");
            return this.SessionID == ((HiveConnection) obj).SessionID;
        }
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


