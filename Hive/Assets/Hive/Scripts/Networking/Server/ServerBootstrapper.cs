using GameData.Networking.Server.Systems;
using DataPacks;
using Unity.Entities;
using UnityEngine;
using Services;
using Vault;

namespace GameData.Networking.Server
{
    public class ServerBootstrapper : MonoBehaviour
    {
        private async void Start()
        {
            var serviceLocator = ServiceLocator.Instance;
            
            var networkSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<ServerMessageSystem>();
            networkSystem.Enabled = true;
            networkSystem.StartListening(this);

            var vault = ServiceLocator.GetService<Vault.Vault>();
            
            var filter = Queries.Equal<PlayerProfile, string>(x => x.Alias, "whuop");
            vault.Query.FindByFilter(filter, playerProfile =>
            {
                Debug.Log($"Found profile with name: {playerProfile.Name}");
            });

            
            vault.Query.FindEquals<PlayerProfile, string>(x => x.Alias, "whuop", result => { });
        }
    }
}

