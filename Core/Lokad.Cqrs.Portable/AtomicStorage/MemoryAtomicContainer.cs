﻿using System;
using System.Collections.Concurrent;
using System.IO;

namespace Lokad.Cqrs.AtomicStorage
{
    public sealed class MemoryAtomicContainer<TKey,TEntity> : IAtomicReader<TKey,TEntity>, IAtomicWriter<TKey,TEntity>
    {
        readonly IAtomicStorageStrategy _strategy;
        readonly string _folder;
        readonly ConcurrentDictionary<string, byte[]> _store;

        public MemoryAtomicContainer(ConcurrentDictionary<string, byte[]> store, IAtomicStorageStrategy strategy)
        {
            _store = store;
            _strategy = strategy;
            _folder = _strategy.GetFolderForEntity(typeof(TEntity),typeof(TKey));
        }

        string GetName(TKey key)
        {
            return Path.Combine(_folder, _strategy.GetNameForEntity(typeof(TEntity), key));
        }

        public bool TryGet(TKey key, out TEntity entity)
        {
            byte[] bytes;
            if(_store.TryGetValue(GetName(key), out bytes))
            {
               using (var mem = new MemoryStream(bytes))
               {
                   entity = _strategy.Deserialize<TEntity>(mem);
                   return true;
               }
            }
            entity = default(TEntity);
            return false;
        }


        public TEntity AddOrUpdate(TKey key, Func<TEntity> addFactory, Func<TEntity, TEntity> update, AddOrUpdateHint hint)
        {
            var result = default(TEntity);
            _store.AddOrUpdate(GetName(key), s =>
                {
                    result = addFactory();
                    using (var memory = new MemoryStream())
                    {
                        _strategy.Serialize(result, memory);
                        return memory.ToArray();
                    }
                }, (s2, bytes) =>
                    {
                        TEntity entity;
                        using (var memory = new MemoryStream(bytes))
                        {
                            entity = _strategy.Deserialize<TEntity>(memory);
                        }
                        result = update(entity);
                        using (var memory = new MemoryStream())
                        {
                            _strategy.Serialize(result, memory);
                            return memory.ToArray();
                        }
                    });
            return result;
        }
     

        public bool TryDelete(TKey key)
        {
            byte[] bytes;
            return _store.TryRemove(GetName(key), out bytes);
        }
    }
}