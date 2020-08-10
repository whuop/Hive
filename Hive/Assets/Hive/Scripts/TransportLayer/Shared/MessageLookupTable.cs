
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Unity.Networking.Transport;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR;

namespace Hive.TransportLayer.Shared
{
    [CreateAssetMenu(fileName = "MessageLookupTable", menuName = "Hive/MessageLookupTable", order = 1)]
    public class MessageLookupTable : ScriptableObject
    {
        [SerializeField]
        private List<string> m_messages = new List<string>();

        private Dictionary<Type, MessageDescriptor> m_typeDescriptors;
        public Dictionary<Type, MessageDescriptor> TypeDescriptors
        {
            get { return m_typeDescriptors; }
        }
        private Dictionary<int, MessageDescriptor> m_intDescriptors;
        public Dictionary<int, MessageDescriptor> IntDescriptors
        {
            get { return m_intDescriptors; }
        }
        private Dictionary<Type, int> m_typeToIntMap;
        public Dictionary<Type, int> TypeToIntMap
        {
            get { return m_typeToIntMap; }
        }
        
        private bool m_isWarmedUp = false;
        
        public void Warmup()
        {
            m_typeDescriptors = new Dictionary<Type, MessageDescriptor>();
            m_intDescriptors = new Dictionary<int, MessageDescriptor>();
            m_typeToIntMap = new Dictionary<Type, int>();
            var type = typeof(IMessage);
            var types = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(s => s.GetTypes())
                .Where(p => type.IsAssignableFrom(p));

            foreach (var targetType in types)
            {
                int elementIndex = m_messages.IndexOf(targetType.Name);
                if (elementIndex < 0)
                    continue;
                    
                var prop = targetType.GetProperty("Descriptor", BindingFlags.Public | BindingFlags.Static);
                if (prop == null)
                    continue;
                var value = prop.GetValue(null, null); // get the static property Descriptor
                if (prop == null)
                    continue;
                var descriptor = (MessageDescriptor) value;
                m_typeDescriptors.Add(targetType, descriptor);
                m_intDescriptors.Add(elementIndex, descriptor);
                m_typeToIntMap.Add(targetType, elementIndex);
            }

            m_isWarmedUp = true;
        }

        public byte[] Serialize(IMessage message)
        {
            int messageIndex = m_typeToIntMap[message.GetType()];
            byte[] indexBytes = BitConverter.GetBytes(messageIndex);
            byte[] data = new byte[4 + message.CalculateSize()];
            CodedOutputStream output = new CodedOutputStream(data);
            
            output.WriteInt32(messageIndex);
            output.WriteMessage(message);
            output.Dispose();
            return data;
        }
        
        [ContextMenu("GenerateMessageLookup")]
        public void GenerateMessageLookup()
        {
            var ext = new List<string> { ".proto" };
            var myFiles = Directory
                .EnumerateFiles(Application.dataPath, "*.*", SearchOption.AllDirectories)
                .Where(s => ext.Contains(Path.GetExtension(s).ToLowerInvariant()));

            foreach (var file in myFiles)
            {
                Debug.LogError("Found File: " + file);
                var lines = File.ReadLines(file);
                foreach (var line in lines)
                {;
                    if (line.TrimStart().StartsWith("message"))
                    {
                        int messageIndex = line.IndexOf("m");
                        int messageLength = 7;
                        int curlIndex = line.IndexOf("{");
                        int curlLength = 1;
                        string message = line;
                        if (messageIndex >= 0)
                        {
                            message = message.Remove(messageIndex, messageLength);
                            curlIndex = message.IndexOf("{");
                        }
                        if (curlIndex >= 0)
                            message = message.Remove(curlIndex, curlLength);
                        message = message.Trim();
                        m_messages.Add(message);
                        Debug.Log("Message Name: " + message);
                    }
                }
            }
            
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        
        
    }
}
