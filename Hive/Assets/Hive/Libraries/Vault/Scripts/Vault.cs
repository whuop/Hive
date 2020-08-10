using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using DataPacks;
using Google.Protobuf;
using JetBrains.Annotations;
using MongoDB.Driver;
using Services;
using UnityEngine;
// ReSharper disable InconsistentNaming

namespace Vault
{
    [CreateAssetMenu(fileName = "Vault", menuName = "Services/Vault", order = 1)]
    public class Vault : Service
    {
        private string m_passwordString = "m9x5EwkbKMs8VIRk";
        private MongoClient m_client;
        private IMongoDatabase m_database;
        private string m_databaseName = "gamedata";
        
        private Dictionary<Type, MongoCollectionWrapper> m_loadedCollections = new Dictionary<Type, MongoCollectionWrapper>();

        private string m_connectionString;
        
        public QueryBuilder Query { get; private set; }

        public override void Initialize()
        {
            base.Initialize();
            
            this.Query = new QueryBuilder(this);
            Connect(m_databaseName);
            
            InjectDummyData();
        }

        private void InjectDummyData()
        {
            DeleteAllData();
            InjectPlayerProfiles();
        }
        
        private int GetNextID<T>() where T : IMessage
        {
            var targetType = typeof(T);
            var counterName = targetType.Name;

            var collection = GetCollection<Counter>();

            FilterDefinition<Counter> findFilter = Queries.Equal<Counter, string>(x => x.Id, counterName);
            UpdateDefinition<Counter> updateFilter = Builders<Counter>.Update.Inc(x => x.Value, 1);
            var updateOptions = new FindOneAndUpdateOptions<Counter>
            {
                IsUpsert = true,
                ReturnDocument = ReturnDocument.After
            };

            var task = collection.FindOneAndUpdateAsync(findFilter, updateFilter, updateOptions);
            return task.GetAwaiter().GetResult().Value;
        }

        private void DeleteAllData()
        {
            Debug.Log("[Vault] Dropping all databases!");
            var databaseNames = m_client.ListDatabaseNames().ToList();
            foreach (var database in databaseNames)
            {
                if (database == "admin" || database == "local")
                {
                    Debug.Log($"[Vault] Skipping {database}");
                    continue;
                }
                    
                Debug.Log($"[Vault] Dropping database {database}!");
                m_client.DropDatabase(database);
                
            }
        }

        private void InjectPlayerProfiles()
        {
            Query.InsertDocument(new PlayerProfile
            {
                Name = "Kristian Brodal",
                Alias = "whuop",
                Email = "kristianbrodal@gmail.com",
                Password = "test",
                Id = GetNextID<PlayerProfile>()
            });
            
            Query.InsertDocument(new PlayerProfile
            {
                Name = "Rudolf Brodal",
                Alias = "whuoppo",
                Email = "kristianbrodal@gmail.comigen",
                Password = "test",
                Id = GetNextID<PlayerProfile>()
            });
            
            Debug.Log("Inserted documents!");
        }

        public void Connect(string database)
        {
            m_client = new MongoClient($"mongodb+srv://whuop:{m_passwordString}@cluster0.ckmtd.gcp.mongodb.net/{database}?retryWrites=true&w=majority");
            SelectDatabase(database);
        }

        private void SelectDatabase(string databaseName)
        {
            m_database = m_client
                .GetDatabase(databaseName);
        }

        private void LoadCollection<T>() where T : IMessage
        {
            Type collectionType = typeof(T);
            if (m_loadedCollections.ContainsKey(collectionType))
            {
                Debug.LogWarning($"[Vault] Collection {collectionType.Name} has already been loaded.");
                return;
            }

            var collectionWrapper = new MongoCollectionWrapper(m_database.GetCollection<T>(collectionType.Name));
            
            m_loadedCollections.Add(collectionType, collectionWrapper);
        }

        public IMongoCollection<T> GetCollection<T>() where T : IMessage
        {
            Type collectionType = typeof(T);
            if (!m_loadedCollections.ContainsKey(collectionType))
            {
                LoadCollection<T>();
            }
            return m_loadedCollections[collectionType].GetAs<IMongoCollection<T>>();
        }

        public override void Destroy()
        {
            base.Destroy();
        }

        public override void Update()
        {
            base.Update();
        }
    }

    public static class Queries
    {
        public static FilterDefinition<T> Equal<T, TField>(Expression<Func<T, TField>> expression, TField value) where T : IMessage
        {
            var builder = Builders<T>.Filter;
            var filter = builder.Eq(expression, value);
            return filter;
        }

        public static FilterDefinition<T> NotEqual<T, TField>(Expression<Func<T, TField>> expression, TField value)
        {
            var builder = Builders<T>.Filter;
            var filter = builder.Ne(expression, value);
            return filter;
        }
        
        public static FilterDefinition<T> GreaterThan<T, TField>(Expression<Func<T, TField>> expression, TField value)
        {
            var builder = Builders<T>.Filter;
            var filter = builder.Gt(expression, value);
            return filter;
        }
        
        public static FilterDefinition<T> LessThan<T, TField>(Expression<Func<T, TField>> expression, TField value)
        {
            var builder = Builders<T>.Filter;
            var filter = builder.Lt(expression, value);
            return filter;
        }
        
        public static FilterDefinition<T> GreaterThanEqual<T, TField>(Expression<Func<T, TField>> expression, TField value)
        {
            var builder = Builders<T>.Filter;
            var filter = builder.Gte(expression, value);
            return filter;
        }
        
        public static FilterDefinition<T> LessThanEqual<T, TField>(Expression<Func<T, TField>> expression, TField value)
        {
            var builder = Builders<T>.Filter;
            var filter = builder.Lte(expression, value);
            return filter;
        }
    }

    public class QueryBuilder
    {
        private Vault m_vault;
        public QueryBuilder(Vault vault)
        {
            m_vault = vault;
        }
        
        public delegate void OnQueryComplete<T>(T result) where T : IMessage;
        public delegate void OnInsertComplete();
        public delegate void OnRemoveComplete();
        public void FindByFilter<T>([NotNull] FilterDefinition<T> filter, OnQueryComplete<T> onQueryComplete) where T : IMessage
        {
            Debug.LogError("STARTING QUERY!");
            var collection = m_vault.GetCollection<T>();
            var task = collection.FindAsync(filter);
            task.ContinueWith((Task taskAgain) =>
            {
                onQueryComplete?.Invoke(task.GetAwaiter().GetResult().FirstOrDefault());
            });
        }

        public void FindEquals<T, TField>(Expression<Func<T, TField>> expression, TField value,
            OnQueryComplete<T> onQueryComplete) where T : IMessage
        {
            Debug.LogError("STARTING QUERY!");
            var collection = m_vault.GetCollection<T>();

            var filter = Builders<T>.Filter.Eq(expression, value);
            var task = collection.FindAsync(filter);
            task.ContinueWith((Task taskAgain) =>
            {
                onQueryComplete?.Invoke(task.GetAwaiter().GetResult().FirstOrDefault());
            });
        }
        
        public void FindNotEquals<T, TField>(Expression<Func<T, TField>> expression, TField value,
            OnQueryComplete<T> onQueryComplete) where T : IMessage
        {
            Debug.LogError("STARTING QUERY!");
            var collection = m_vault.GetCollection<T>();

            var filter = Builders<T>.Filter.Ne(expression, value);
            var task = collection.FindAsync(filter);
            task.ContinueWith((Task taskAgain) =>
            {
                onQueryComplete?.Invoke(task.GetAwaiter().GetResult().FirstOrDefault());
            });
        }
        
        public void FindGreaterThan<T, TField>(Expression<Func<T, TField>> expression, TField value,
            OnQueryComplete<T> onQueryComplete) where T : IMessage
        {
            var collection = m_vault.GetCollection<T>();

            var filter = Builders<T>.Filter.Ne(expression, value);
            var task = collection.FindAsync(filter);
            task.ContinueWith((Task taskAgain) =>
            {
                onQueryComplete?.Invoke(task.GetAwaiter().GetResult().FirstOrDefault());
            });
        }
        
        public void FindLessThan<T, TField>(Expression<Func<T, TField>> expression, TField value,
            OnQueryComplete<T> onQueryComplete) where T : IMessage
        {
            var collection = m_vault.GetCollection<T>();

            var filter = Builders<T>.Filter.Ne(expression, value);
            var task = collection.FindAsync(filter);
            task.ContinueWith((Task taskAgain) =>
            {
                onQueryComplete?.Invoke(task.GetAwaiter().GetResult().FirstOrDefault());
            });
        }
        
        public void FindGreaterThanEqual<T, TField>(Expression<Func<T, TField>> expression, TField value,
            OnQueryComplete<T> onQueryComplete) where T : IMessage
        {
            var collection = m_vault.GetCollection<T>();

            var filter = Builders<T>.Filter.Ne(expression, value);
            var task = collection.FindAsync(filter);
            task.ContinueWith((Task taskAgain) =>
            {
                onQueryComplete?.Invoke(task.GetAwaiter().GetResult().FirstOrDefault());
            });
        }
        
        public void FindLessThanEqual<T, TField>(Expression<Func<T, TField>> expression, TField value,
            OnQueryComplete<T> onQueryComplete) where T : IMessage
        {
            var collection = m_vault.GetCollection<T>();

            var filter = Builders<T>.Filter.Ne(expression, value);
            var task = collection.FindAsync(filter);
            task.ContinueWith((Task taskAgain) =>
            {
                onQueryComplete?.Invoke(task.GetAwaiter().GetResult().FirstOrDefault());
            });
        }
        
        public void InsertDocument<T>(T document, OnInsertComplete onInsertComplete = null) where T : IMessage
        {
            Type collectionType = typeof(T);
            var collection = m_vault.GetCollection<T>();
            var task = collection.InsertOneAsync(document);
            task.ContinueWith((Task nextTask) =>
            {
                onInsertComplete?.Invoke();
            });
        }
        
        public void RemoveDocument<T, TField>(Expression<Func<T, TField>> expression, TField value, OnRemoveComplete onRemoveComplete = null) where T : IMessage
        {
            Type collectionType = typeof(T);
            var collection = m_vault.GetCollection<T>();
            
            //    Contruct filter
            var filter = Builders<T>.Filter.Eq(expression, value);
            var task = collection.DeleteOneAsync(filter);
            task.ContinueWith((Task nextTask) =>
            {
                onRemoveComplete?.Invoke();
            });
        }
    }

    public class MongoCollectionWrapper
    {
        private object m_collectionObject;

        public MongoCollectionWrapper(object collectionObject)
        {
            m_collectionObject = collectionObject;
        }
        
        public T GetAs<T>()
        {
            return (T) m_collectionObject;
        }
    }
}


