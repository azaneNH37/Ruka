using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using MessagePack;
using R3;

namespace Ruka.Core.Saves
{
    internal sealed class PersistenceEngine<TChannel> : IDisposable
    {
        private readonly ISaveStorage _storage;
        private readonly MigrationRunner<TChannel> _migrationRunner;
        private readonly int _targetVersion;
        private readonly bool _autoResaveOnMigrate;
        private readonly string _logTag;

        private readonly SemaphoreSlim _lock = new(1, 1);
        private readonly ReactiveProperty<bool> _isProcessing = new(false);

        public ReadOnlyReactiveProperty<bool> IsProcessing { get; }

        public PersistenceEngine(
            ISaveStorage storage,
            MigrationRunner<TChannel> migrationRunner,
            int targetVersion,
            bool autoResaveOnMigrate,
            string logTag)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _migrationRunner = migrationRunner ?? throw new ArgumentNullException(nameof(migrationRunner));
            _targetVersion = targetVersion;
            _autoResaveOnMigrate = autoResaveOnMigrate;
            _logTag = logTag;
            IsProcessing = _isProcessing.ToReadOnlyReactiveProperty();
        }

        public async UniTask ExecuteAsync(Func<UniTask> operation)
        {
            await _lock.WaitAsync();
            try
            {
                _isProcessing.Value = true;
                await operation();
            }
            finally
            {
                _isProcessing.Value = false;
                _lock.Release();
            }
        }

        public async UniTask<int> WriteAsync(string key, SaveContainer container)
        {
            var data = MessagePackSerializer.Serialize(container);
            await _storage.SaveAsync(key, data);
            return data.Length;
        }

        public async UniTask<(SaveContainer Container, bool Migrated, string MigrationPath)> ReadAsync(string key)
        {
            var raw = await _storage.LoadAsync(key);
            if (raw == null)
                return (null, false, string.Empty);

            var container = MessagePackSerializer.Deserialize<SaveContainer>(raw);
            if (container == null)
                return (null, false, string.Empty);

            if (!_migrationRunner.TryMigrate(container, _targetVersion, out var path, out var error))
                throw new InvalidOperationException($"Save migration failed for {key}. {error}");

            var migrated = !string.IsNullOrEmpty(path);
            if (migrated)
            {
                UnityEngine.Debug.Log($"[{_logTag}] Save migrated. Key={key}, Path={path}");
                if (_autoResaveOnMigrate)
                    await WriteAsync(key, container);
            }

            return (container, migrated, path);
        }

        public void Dispose()
        {
            _lock.Dispose();
            _isProcessing.Dispose();
        }
    }
}
