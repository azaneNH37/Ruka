using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using MessagePipe;
using R3;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Ruka.Core.Saves
{
    internal sealed class CrossSaveService : ICrossSaveService, IAsyncStartable, IDisposable
    {
        private readonly List<ICrossSaveable> _saveables;
        private readonly ISaveStorage _storage;
        private readonly CrossSaveConfig _config;
        private readonly PersistenceEngine<CrossChannel> _engine;
        private readonly string _dataKey;

        private readonly IPublisher<CrossSaveStartedSignal> _saveStartPublisher;
        private readonly IPublisher<CrossSaveFinishedSignal> _saveFinishPublisher;
        private readonly IPublisher<CrossLoadStartedSignal> _loadStartPublisher;
        private readonly IPublisher<CrossLoadFinishedSignal> _loadFinishPublisher;

        public ReadOnlyReactiveProperty<bool> IsProcessing { get; }

        [Inject]
        internal CrossSaveService(
            IEnumerable<ICrossSaveable> permanentSaveables,
            ISaveStorage storage,
            CrossSaveConfig config,
            MigrationRunner<CrossChannel> migrationRunner,
            IPublisher<CrossSaveStartedSignal> saveStartPublisher,
            IPublisher<CrossSaveFinishedSignal> saveFinishPublisher,
            IPublisher<CrossLoadStartedSignal> loadStartPublisher,
            IPublisher<CrossLoadFinishedSignal> loadFinishPublisher)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _dataKey = config.FileName;
            _engine = new PersistenceEngine<CrossChannel>(storage, migrationRunner, config.Version, config.AutoResaveOnMigrate, "CrossSaveService");
            _saveStartPublisher = saveStartPublisher;
            _saveFinishPublisher = saveFinishPublisher;
            _loadStartPublisher = loadStartPublisher;
            _loadFinishPublisher = loadFinishPublisher;
            IsProcessing = _engine.IsProcessing;

            _saveables = BuildSaveableList(permanentSaveables);
        }

        public async UniTask StartAsync(CancellationToken ct)
        {
            var (_, loaded) = await LoadCrossInternalAsync();
            if (!loaded)
            {
                SetupDefaultOnAll();
                await SaveCrossInternalAsync();
            }
        }

        public async UniTask SaveCrossAsync()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            int bytesWritten = 0;
            string errorMessage = null;

            _saveStartPublisher.Publish(new CrossSaveStartedSignal());

            try
            {
                bytesWritten = await SaveCrossInternalAsync();
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                Debug.LogError($"[CrossSaveService] Save failed: {ex}");
            }
            finally
            {
                sw.Stop();
            }

            _saveFinishPublisher.Publish(new CrossSaveFinishedSignal
            {
                Success = errorMessage == null,
                DurationMs = sw.ElapsedMilliseconds,
                BytesWritten = bytesWritten
            });
        }

        public async UniTask<bool> LoadCrossAsync()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            bool success = false;
            bool migrated = false;
            string errorMessage = null;

            _loadStartPublisher.Publish(new CrossLoadStartedSignal());

            try
            {
                (migrated, success) = await LoadCrossInternalAsync();
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                Debug.LogError($"[CrossSaveService] Load failed: {ex}");
            }
            finally
            {
                sw.Stop();
            }

            _loadFinishPublisher.Publish(new CrossLoadFinishedSignal
            {
                Success = success,
                DurationMs = sw.ElapsedMilliseconds,
                BytesRead = 0,
                Migrated = migrated
            });

            return success;
        }

        public async UniTask ResetCrossAsync()
        {
            SetupDefaultOnAll();
            if (_storage.Exists(_dataKey))
                _storage.Delete(_dataKey);
            await SaveCrossAsync();
        }

        public void Dispose()
        {
            _saveables.Clear();
            _engine.Dispose();
        }

        // ── Internals ────────────────────────────────────────────────────

        private async UniTask<int> SaveCrossInternalAsync()
        {
            var bytes = 0;
            await _engine.ExecuteAsync(async () =>
            {
                bytes = await WriteCrossAsync();
            });
            return bytes;
        }

        private async UniTask<int> WriteCrossAsync()
        {
            var container = new SaveContainer
            {
                Version = _config.Version,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Chunks = BuildSnapshot()
            };
            return await _engine.WriteAsync(_dataKey, container);
        }

        private async UniTask<(bool Migrated, bool Success)> LoadCrossInternalAsync()
        {
            var migrated = false;
            var success = false;
            await _engine.ExecuteAsync(async () =>
            {
                var (container, didMigrate, _) = await _engine.ReadAsync(_dataKey);
                if (container != null)
                {
                    RestoreAllFrom(container);
                    migrated = didMigrate;
                    success = true;
                }
            });
            return (migrated, success);
        }

        private static List<ICrossSaveable> BuildSaveableList(IEnumerable<ICrossSaveable> source)
        {
            var list = new List<ICrossSaveable>();
            var seenKeys = new HashSet<string>();

            foreach (var s in source ?? Enumerable.Empty<ICrossSaveable>())
            {
                if (s == null)
                    continue;

                if (string.IsNullOrEmpty(s.SaveKey))
                {
                    Debug.LogWarning("[CrossSaveService] ICrossSaveable with empty SaveKey skipped.");
                    continue;
                }

                if (!seenKeys.Add(s.SaveKey))
                {
                    Debug.LogError($"[CrossSaveService] Duplicate SaveKey detected: {s.SaveKey}. Skipping.");
                    continue;
                }

                list.Add(s);
            }

            return list;
        }

        private Dictionary<string, byte[]> BuildSnapshot()
        {
            var snapshot = new Dictionary<string, byte[]>();
            foreach (var s in _saveables)
            {
                if (snapshot.ContainsKey(s.SaveKey))
                {
                    Debug.LogError($"[CrossSaveService] Duplicate SaveKey during capture: {s.SaveKey}. Skipping.");
                    continue;
                }
                snapshot[s.SaveKey] = s.CaptureState();
            }
            return snapshot;
        }

        private void SetupDefaultOnAll()
        {
            foreach (var s in _saveables)
                s.SetupDefaultState();
        }

        private void RestoreAllFrom(SaveContainer container)
        {
            foreach (var s in _saveables)
            {
                if (container.Chunks.TryGetValue(s.SaveKey, out var chunk))
                    s.RestoreState(chunk);
            }
        }
    }
}
