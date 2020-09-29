using System;
using System.Collections.Generic;
using Leopotam.Ecs;
using UnityEngine;

namespace Hive.TransportLayer.Shared.ECS
{
    public class EcsManager
    {
        private EcsWorld m_world;
        private EcsSystems m_updateSystems;
        
        private List<Type> m_systemsToAdd = new List<Type>();
        private Dictionary<Type, object> m_dataToInject = new Dictionary<Type, object>();
        private Dictionary<Type, IEcsSystem> m_runningSystems = new Dictionary<Type, IEcsSystem>();

        private bool m_initedEcs = false;
        public bool IsInitialized
        {
            get { return m_initedEcs; }
        }
        
        public EcsManager()
        {
            m_world = new EcsWorld();
            
            //    Expose Ecs World to all systems
            AddInject(m_world);
        }

        public void AddSystem<T>() where T : IEcsSystem
        {
            m_systemsToAdd.Add(typeof(T));
        }

        public T GetSystem<T>() where T : IEcsSystem
        {
            var type = typeof(T);
            if (!m_runningSystems.ContainsKey(type))
                return default(T);
            return (T) m_runningSystems[type];
        }

        public void RemoveSystem<T>()
        {
            m_systemsToAdd.Remove(typeof(T));
        }

        public void AddInject<T>(T data)
        {
            Type t = typeof(T);
            if (m_dataToInject.ContainsKey(t))
            {
                Debug.LogError($"[EcsManager] Can't add inject of type {t.Name}. An inject of the same type has already been injected!");
                return;
            }
            m_dataToInject.Add(t, data);
        }

        public void RemoveInject<T>()
        {
            Type t = typeof(T);
            if (m_dataToInject.ContainsKey(t))
            {
                m_dataToInject.Remove(t);
            }
        }

        public void Startup()
        {
            if (m_initedEcs)
                return;
            
            m_updateSystems = new EcsSystems(m_world);
            for (int i = 0; i < m_systemsToAdd.Count; i++)
            {
                var system = m_systemsToAdd[i];
                IEcsSystem systemInstance = (IEcsSystem) Activator.CreateInstance(system);
                m_updateSystems.Add(systemInstance);
                m_runningSystems.Add(system, systemInstance);
            }

            foreach (var kvp in m_dataToInject)
            {
                m_updateSystems.Inject(kvp.Value, kvp.Key);
            }
            
            m_updateSystems.Init();
            m_initedEcs = true;
        }

        public void Teardown()
        {
            if (!m_initedEcs)
                return;
            
            m_runningSystems.Clear();
            m_updateSystems.Destroy();
            m_updateSystems = null;
            
            m_initedEcs = false;
        }

        public void Update()
        {
            if (!m_initedEcs)
                return;
            m_updateSystems.Run();
        }
    }

}

