using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Networking.Transport;
using UnityEngine;
using UnityEngine.Networking;

namespace GameData.Networking
{
    struct ClientUpdateJob : IJob
    {
        public NetworkDriver Driver;
        public NativeArray<NetworkConnection> Connections;
        public NativeArray<byte> Done;

        public void Execute()
        {
            if (!Connections[0].IsCreated)
            {
                if (Done[0] != 1)
                {
                    Debug.LogError("Something went wrong during connection!");
                    return;
                }
            }

            DataStreamReader stream;
            NetworkEvent.Type cmd;

            while((cmd = Connections[0].PopEvent(Driver, out stream)) != NetworkEvent.Type.Empty)
            {
                if (cmd == NetworkEvent.Type.Connect)
                {
                    Debug.Log("We are now connected to the server");

                    var value = 1;
                    var writer = Driver.BeginSend(Connections[0]);
                    writer.WriteInt(value);
                    Driver.EndSend(writer);
                }
                else if (cmd == NetworkEvent.Type.Data)
                {
                    int value = stream.ReadInt();
                    Debug.Log("Got the value: " + value + " back from the server");

                    //  Change to 1, we've received the value and are done
                    Done[0] = 1;
                    Connections[0].Disconnect(Driver);
                    Connections[0] = default(NetworkConnection);
                }
                else if (cmd == NetworkEvent.Type.Disconnect)
                {
                    Debug.Log("Client got disconnected from server");
                    Connections[0] = default(NetworkConnection);
                }
            }
        }
    }
}