using System;
using System.Collections;
using System.Collections.Generic;
using Google.Protobuf.Reflection;
using UnityEngine;

namespace Services
{
    [CreateAssetMenu(fileName = "ServiceLocator", menuName = "Services/ServiceLocator", order = 0)]
    public class ServiceLocator : ScriptableObject
    {
        [SerializeField]
        private List<Service> m_services = new List<Service>();
        
        private IDictionary<Type, Service> m_loadedServices = new Dictionary<Type, Service>();

        private static ServiceLocator m_instance;

        public static ServiceLocator Instance
        {
            get
            {
                if (m_instance == null)
                {
                    m_instance = Resources.Load<ServiceLocator>("ServiceLocator");
                    m_instance.Initialize();
                    m_instance.LoadServices();
                }

                return m_instance;
            }
        }

        private void Initialize()
        {
            
        }

        private void LoadServices()
        {
            for (int i = 0; i < m_services.Count; i++)
            {
                var service = m_services[i];
                service.Initialize();
                
                Debug.Log($"-- Initialized Service {service.name}");
            }
        }

        public static T GetService<T>() where T : Service
        {
            return Instance.GetServiceInternal<T>();
        }

        public T GetServiceInternal<T>() where T : Service
        {
            Type wantedType = typeof(T);
            for (int i = 0; i < m_services.Count; i++)
            {
                if (m_services[i].GetType() == wantedType)
                    return (T) m_services[i];
            }

            return null;
        }
    }

    public class Service : ScriptableObject
    {
        void Awake()
        {
        }

        private void OnDestroy()
        {
        }

        public virtual void Initialize()
        {
            
        }

        public virtual void Destroy()
        {
            
        }

        public virtual void Update()
        {
            
        }
    }
}


