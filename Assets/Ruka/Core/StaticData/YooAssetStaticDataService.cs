using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Ruka.Core.Resources;
using Ruka.Core.Symbols;
using UnityEngine;

namespace Ruka.Core.StaticData
{
    public sealed class YooAssetStaticDataService : IStaticDataService
    {
        private readonly IAssetScope _assetScope;
        private readonly Dictionary<Symbol<AssetTag>, UniTask<IStaticDataGroup>> _pendingTasks = new();
        private readonly Dictionary<Symbol<AssetTag>, IStaticDataGroup> _loadedGroups = new();

        public YooAssetStaticDataService(IAssetScope assetScope)
        {
            _assetScope = assetScope;
        }

        public async UniTask<IStaticDataGroup> LoadGroupAsync(
            Symbol<AssetTag> tag, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            if (_loadedGroups.TryGetValue(tag, out var loaded))
                return loaded;

            if (_pendingTasks.TryGetValue(tag, out var pending))
                return await pending;

            var task = LoadGroupInternalAsync(tag, ct);
            _pendingTasks[tag] = task;

            try
            {
                var group = await task;
                _loadedGroups[tag] = group;
                return group;
            }
            finally
            {
                _pendingTasks.Remove(tag);
            }
        }

        private async UniTask<IStaticDataGroup> LoadGroupInternalAsync(
            Symbol<AssetTag> tag, CancellationToken ct)
        {
            using var scope = _assetScope.CreateScope();
            var assets = await scope.LoadAllByTagAsync<TextAsset>(tag);

            var files = new Dictionary<string, string>(assets.Count);
            var fileNames = new List<string>(assets.Count);

            for (var i = 0; i < assets.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var asset = assets[i];
                var name = asset.name;
                files[name] = asset.text;
                fileNames.Add(name);
            }

            return new StaticDataGroup(tag, files, fileNames);
        }
    }
}
