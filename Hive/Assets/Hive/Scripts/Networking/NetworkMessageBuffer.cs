using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;

namespace GameData.Networking
{
    public class NetworkMessageBuffer
    {
        private ConcurrentQueue<object> m_messageQueue = new ConcurrentQueue<object>();


        public static NativeArray<byte> ToMessage<T>(T messageObject) where T : struct
        {
            return GetBytes(messageObject);
        }

        public static T FromMessage<T>(NativeArray<byte> serializedMessage) where T : struct
        {
            return FromBytes<T>(serializedMessage);
        }

        /*public void PushMessage<T>(T message) where T : struct
        {
            m_messageQueue.Enqueue(message);
        }*/

        /*public byte[] CompressMessage()
        {
            byte[] message;
            object result;
            while(m_messageQueue.TryDequeue(out result))
            {

            }
        }*/

        private static NativeArray<byte> GetBytes<T>(T str)
        {
            int size = Marshal.SizeOf(str);

            NativeArray<byte> arr = new NativeArray<byte>(size, Allocator.Temp);

            GCHandle h = default(GCHandle);

            try
            {
                h = GCHandle.Alloc(arr, GCHandleType.Pinned);

                Marshal.StructureToPtr<T>(str, h.AddrOfPinnedObject(), false);
            }
            finally
            {
                if (h.IsAllocated)
                {
                    h.Free();
                }
            }

            return arr;
        }

        private static T FromBytes<T>(NativeArray<byte> arr) where T : struct
        {
            T str = default(T);

            GCHandle h = default(GCHandle);

            try
            {
                h = GCHandle.Alloc(arr, GCHandleType.Pinned);

                str = Marshal.PtrToStructure<T>(h.AddrOfPinnedObject());

            }
            finally
            {
                if (h.IsAllocated)
                {
                    h.Free();
                }
            }

            return str;
        }
    }
}


