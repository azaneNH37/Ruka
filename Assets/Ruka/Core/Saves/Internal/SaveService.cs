using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using MessagePack;
using MessagePipe;
using R3;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Ruka.Core.Saves
{
    internal sealed class SaveService : ISaveService, IAsyncStartable, IDisposable
    {
        private const string GlobalFileName = "global.dat";

        private readonly List<ISaveable> _saveables;
        private readonly ISaveStorage _storage;
        private readonly SavesConfig _config;
        private readonly PersistenceEngine<SlotChannel> _engine;
        private readonly ReactiveProperty<int> _activeSlot = new(0);
        private GlobalMetadata _globalData = new();

        private readonly IPublisher<SlotSaveStartedSignal> _saveStartPublisher;
        private readonly IPublisher<SlotSaveFinishedSignal> _saveFinishPublisher;
        private readonly IPublisher<SlotLoadStartedSignal> _loadStartPublisher;
        private readonly IPublisher<SlotLoadFinishedSignal> _loadFinishPublisher;

        public ReadOnlyReactiveProperty<int> ActiveSlot { get; }
        public ReadOnlyReactiveProperty<bool> IsProcessing { get; }
        public int MaxSlots => _config.MaxSaveSlots;
        public bool HasActiveSlot => _activeSlot.Value != 0;

        [Inject]
        internal SaveService(
            IEnumerable<ISaveable> permanentSaveables,
            ISaveStorage storage,
            SavesConfig config,
            MigrationRunner<SlotChannel> migrationRunner,
            IPublisher<SlotSaveStartedSignal> saveStartPublisher,
            IPublisher<SlotSaveFinishedSignal> saveFinishPublisher,
            IPublisher<SlotLoadStartedSignal> loadStartPublisher,
            IPublisher<SlotLoadFinishedSignal> loadFinishPublisher)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _engine = new PersistenceEngine<SlotChannel>(storage, migrationRunner, config.Version, config.AutoResaveOnMigrate, "SaveService");
            _saveStartPublisher = saveStartPublisher;
            _saveFinishPublisher = saveFinishPublisher;
            _loadStartPublisher = loadStartPublisher;
            _loadFinishPublisher = loadFinishPublisher;

            ActiveSlot = _activeSlot.ToReadOnlyReactiveProperty();
            IsProcessing = _engine.IsProcessing;

            _saveables = BuildSaveableList(permanentSaveables);
        }

        public async UniTask StartAsync(CancellationToken ct)
        {
            await LoadGlobalDataAsync();
        }

        public async UniTask SetActiveSlotAsync(int slot)
        {
            CheckValidSlot(slot);

            var previousSlot = _activeSlot.Value;
            SetupDefaultOnAll();

            await SaveGlobalDataAsync(pendingActiveSlot: slot);

            try
            {
                _activeSlot.Value = slot;
                var loaded = await TryLoadMetaAsync(slot);
                if (!loaded)
                    await SaveSlotAsync(slot);

                await SaveGlobalDataAsync(lastActiveSlot: slot, pendingActiveSlot: 0);
            }
            catch
            {
                _activeSlot.Value = previousSlot;
                await SaveGlobalDataAsync(lastActiveSlot: previousSlot, pendingActiveSlot: 0);
                throw;
            }
        }

        public async UniTask CreateNewGameFromSlot(int slot)
        {
            CheckValidSlot(slot);

            var previousSlot = _activeSlot.Value;
            SetupDefaultOnAll();

            await SaveGlobalDataAsync(pendingActiveSlot: slot);

            try
            {
                _activeSlot.Value = slot;
                await SaveSlotAsync(slot);
                await SaveGlobalDataAsync(lastActiveSlot: slot, pendingActiveSlot: 0);
            }
            catch
            {
                _activeSlot.Value = previousSlot;
                await SaveGlobalDataAsync(lastActiveSlot: previousSlot, pendingActiveSlot: 0);
                throw;
            }
        }

        public async UniTask SaveCurrentSlotAsync()
        {
            if (!HasActiveSlot)
                throw new InvalidOperationException("No active slot to save.");

            var slot = _activeSlot.Value;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            int totalBytes = 0;
            string errorMessage = null;

            _saveStartPublisher.Publish(new SlotSaveStartedSignal { Slot = slot });

            try
            {
                await _engine.ExecuteAsync(async () =>
                {
                    totalBytes += await WriteFullSaveAsync(slot);
                    totalBytes += await WriteMetaSaveAsync(slot);
                });
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                Debug.LogError($"[SaveService] Save failed for slot {slot}: {ex}");
            }
            finally
            {
                sw.Stop();
            }

            _saveFinishPublisher.Publish(new SlotSaveFinishedSignal
            {
                Slot = slot,
                Success = errorMessage == null,
                DurationMs = sw.ElapsedMilliseconds,
                BytesWritten = totalBytes
            });
        }

        public async UniTask<bool> LoadCurrentSlotFullAsync()
        {
            if (!HasActiveSlot)
                return false;

            var slot = _activeSlot.Value;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            bool success = false;
            bool migrated = false;
            int bytesRead = 0;
            string errorMessage = null;

            _loadStartPublisher.Publish(new SlotLoadStartedSignal { Slot = slot });

            try
            {
                await _engine.ExecuteAsync(async () =>
                {
                    var key = GetSlotKey(slot, ".sav");
                    var (container, didMigrate, _) = await _engine.ReadAsync(key);
                    if (container == null)
                        throw new InvalidOperationException($"Save file not found for slot {slot}.");

                    migrated = didMigrate;
                    bytesRead = GetContainerBytes(container);
                    RestoreAllFrom(container);
                });
                success = true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                Debug.LogError($"[SaveService] Load failed for slot {slot}: {ex}");
            }
            finally
            {
                sw.Stop();
            }

            _loadFinishPublisher.Publish(new SlotLoadFinishedSignal
            {
                Slot = slot,
                Success = success,
                DurationMs = sw.ElapsedMilliseconds,
                BytesRead = bytesRead,
                Migrated = migrated
            });

            return success;
        }

        public async UniTask<SaveSlotSnapshot> LoadSnapshotAsync(int slot)
        {
            CheckValidSlot(slot);

            SaveSlotSnapshot result = null;
            await _engine.ExecuteAsync(async () =>
            {
                var key = GetSlotKey(slot, ".meta");
                var (container, _, _) = await _engine.ReadAsync(key);
                if (container != null)
                    result = new SaveSlotSnapshot(slot, container);
            });

            return result;
        }

        public void Dispose()
        {
            _saveables.Clear();
            _activeSlot.Dispose();
            _engine.Dispose();
        }

        // ── Helpers ──────────────────────────────────────────────────────

        private static List<ISaveable> BuildSaveableList(IEnumerable<ISaveable> source)
        {
            var list = new List<ISaveable>();
            var seenKeys = new HashSet<string>();

            foreach (var s in source ?? Enumerable.Empty<ISaveable>())
            {
                if (s == null)
                    continue;

                if (string.IsNullOrEmpty(s.SaveKey))
                {
                    Debug.LogWarning("[SaveService] ISaveable with empty SaveKey skipped.");
                    continue;
                }

                if (!seenKeys.Add(s.SaveKey))
                {
                    Debug.LogError($"[SaveService] Duplicate SaveKey detected: {s.SaveKey}. Skipping.");
                    continue;
                }

                list.Add(s);
            }

            return list;
        }

        private static int GetContainerBytes(SaveContainer container)
        {
            if (container?.Chunks == null)
                return 0;
            var total = 0;
            foreach (var chunk in container.Chunks.Values)
            {
                if (chunk != null)
                    total += chunk.Length;
            }
            return total;
        }

        private string GetSlotKey(int slot, string extension)
        {
            return $"{_config.SaveFilePrefix}{slot:D3}{extension}";
        }

        private static Dictionary<string, byte[]> BuildSnapshot(IEnumerable<ISaveable> saveables)
        {
            var snapshot = new Dictionary<string, byte[]>();
            foreach (var s in saveables)
            {
                if (s == null)
                    continue;

                if (snapshot.ContainsKey(s.SaveKey))
                {
                    UnityEngine.                    Debug.LogError($"[SaveService] Duplicate SaveKey during capture: {s.SaveKey}. Skipping.");
                    continue;
                }

                snapshot[s.SaveKey] = s.CaptureState();
            }
            return snapshot;
        }

        private Dictionary<string, byte[]> BuildFullSnapshot()
        {
            return BuildSnapshot(_saveables);
        }

        private Dictionary<string, byte[]> BuildMetaSnapshot()
        {
            return BuildSnapshot(_saveables.Where(s => s.IsMeta));
        }

        private static void RestoreFrom(IEnumerable<ISaveable> saveables, SaveContainer container)
        {
            foreach (var s in saveables)
            {
                if (container.Chunks.TryGetValue(s.SaveKey, out var chunk))
                    s.RestoreState(chunk);
            }
        }

        private void RestoreAllFrom(SaveContainer container)
        {
            RestoreFrom(_saveables, container);
        }

        private void RestoreMetaFrom(SaveContainer container)
        {
            RestoreFrom(_saveables.Where(s => s.IsMeta), container);
        }

        private void SetupDefaultOnAll()
        {
            foreach (var s in _saveables)
                s.SetupDefaultState();
        }

        private async UniTask<int> WriteFullSaveAsync(int slot)
        {
            var container = new SaveContainer
            {
                Version = _config.Version,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Chunks = BuildFullSnapshot()
            };
            return await _engine.WriteAsync(GetSlotKey(slot, ".sav"), container);
        }

        private async UniTask<int> WriteMetaSaveAsync(int slot)
        {
            var container = new SaveContainer
            {
                Version = _config.Version,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Chunks = BuildMetaSnapshot()
            };
            return await _engine.WriteAsync(GetSlotKey(slot, ".meta"), container);
        }

        private async UniTask SaveSlotAsync(int slot)
        {
            await _engine.ExecuteAsync(async () =>
            {
                await WriteFullSaveAsync(slot);
                await WriteMetaSaveAsync(slot);
            });
        }

        private async UniTask<bool> TryLoadMetaAsync(int slot)
        {
            bool loaded = false;
            await _engine.ExecuteAsync(async () =>
            {
                var key = GetSlotKey(slot, ".meta");
                var (container, _, _) = await _engine.ReadAsync(key);
                if (container != null)
                {
                    RestoreMetaFrom(container);
                    loaded = true;
                }
            });
            return loaded;
        }

        private void CheckValidSlot(int slot)
        {
            if (slot <= 0 || slot > _config.MaxSaveSlots)
                throw new ArgumentOutOfRangeException(nameof(slot),
                    $"Slot must be between 1 and {_config.MaxSaveSlots}.");
        }

        // ── Global Metadata ──────────────────────────────────────────────

        private async UniTask LoadGlobalDataAsync()
        {
            var raw = await _storage.LoadAsync(GlobalFileName);
            if (raw != null)
            {
                try
                {
                    _globalData = MessagePackSerializer.Deserialize<GlobalMetadata>(raw);
                    await RecoverPendingSlotIfNeeded();
                    return;
                }
                catch (Exception)
                {
                    // Corrupt global.dat, reset
                }
            }

            InitGlobalData();
        }

        private void InitGlobalData()
        {
            _activeSlot.Value = 0;
            _globalData = new GlobalMetadata { LastActiveSlot = 0, PendingActiveSlot = 0 };
        }

        private async UniTask SaveGlobalDataAsync(int? lastActiveSlot = null, int? pendingActiveSlot = null)
        {
            if (lastActiveSlot.HasValue)
                _globalData.LastActiveSlot = lastActiveSlot.Value;
            if (pendingActiveSlot.HasValue)
                _globalData.PendingActiveSlot = pendingActiveSlot.Value;

            var raw = MessagePackSerializer.Serialize(_globalData);
            await _storage.SaveAsync(GlobalFileName, raw);
        }

        private async UniTask RecoverPendingSlotIfNeeded()
        {
            var recoveredSlot = _globalData.LastActiveSlot;

            if (_globalData.PendingActiveSlot > 0)
            {
                var pendingSlot = _globalData.PendingActiveSlot;
                var hasPendingData = _storage.Exists(GetSlotKey(pendingSlot, ".meta"))
                                  || _storage.Exists(GetSlotKey(pendingSlot, ".sav"));
                recoveredSlot = hasPendingData ? pendingSlot : _globalData.LastActiveSlot;

                _globalData.LastActiveSlot = recoveredSlot;
                _globalData.PendingActiveSlot = 0;
                await SaveGlobalDataAsync();
            }

            _activeSlot.Value = recoveredSlot;
        }
    }
}
