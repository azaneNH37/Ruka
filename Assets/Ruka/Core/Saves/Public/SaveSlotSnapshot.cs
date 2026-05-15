using System;
using MessagePack;

namespace Ruka.Core.Saves
{
    public sealed class SaveSlotSnapshot
    {
        private readonly SaveContainer _container;

        public int Slot { get; }
        public int Version => _container.Version;
        public DateTimeOffset Timestamp => DateTimeOffset.FromUnixTimeSeconds(_container.Timestamp);

        internal SaveSlotSnapshot(int slot, SaveContainer container)
        {
            Slot = slot;
            _container = container ?? throw new ArgumentNullException(nameof(container));
        }

        public bool TryGet<T>(SaveKey<T> key, out T data)
        {
            if (_container.Chunks.TryGetValue(key.Value, out var chunk))
            {
                try
                {
                    data = MessagePackSerializer.Deserialize<T>(chunk);
                    return true;
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError($"[SaveSlotSnapshot] Failed to deserialize '{key.Value}': {e}");
                }
            }
            data = default;
            return false;
        }

        public bool Contains<T>(SaveKey<T> key)
        {
            return _container.Chunks.ContainsKey(key.Value);
        }
    }
}
