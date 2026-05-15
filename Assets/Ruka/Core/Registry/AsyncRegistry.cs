using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Ruka.Core.Registry
{
    public abstract class AsyncRegistry<TKey, TValue> : IRegistry<TKey, TValue>
        where TValue : IKeyed<TKey>
    {
        private readonly UniTaskCompletionSource _readySource = new();
        private Dictionary<TKey, TValue> _items;

        public bool IsReady { get; private set; }

        public UniTask WaitUntilReady => _readySource.Task;

        public IReadOnlyDictionary<TKey, TValue> All => _items;

        public TValue Get(TKey key)
        {
            if (!IsReady)
                throw new InvalidOperationException(
                    $"Registry of type {GetType().Name} is not ready.");

            if (_items.TryGetValue(key, out var value))
                return value;

            throw new KeyNotFoundException($"Key '{key}' not found in {GetType().Name}.");
        }

        public bool TryGet(TKey key, out TValue value)
        {
            if (!IsReady)
                throw new InvalidOperationException(
                    $"Registry of type {GetType().Name} is not ready.");

            return _items.TryGetValue(key, out value);
        }

        protected UniTask InitializeAsync(IEnumerable<TValue> items, CancellationToken ct = default)
        {
            if (_readySource.Task.Status.IsCompleted())
                return UniTask.CompletedTask;

            try
            {
                ct.ThrowIfCancellationRequested();

                var dict = new Dictionary<TKey, TValue>();
                foreach (var item in items)
                {
                    ct.ThrowIfCancellationRequested();
                    if (!dict.TryAdd(item.Key, item))
                    {
                        Debug.LogError(
                            $"[{GetType().Name}] Duplicate key '{item.Key}' detected, skipping.");
                    }
                }

                _items = dict;
                IsReady = true;
                _readySource.TrySetResult();
            }
            catch (OperationCanceledException)
            {
                _readySource.TrySetCanceled(ct);
                throw;
            }
            catch (Exception ex)
            {
                _readySource.TrySetException(ex);
                throw;
            }

            return UniTask.CompletedTask;
        }
    }
}
