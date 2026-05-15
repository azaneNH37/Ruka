using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using VContainer.Unity;

namespace Ruka.Core.Registry
{
    public sealed class Registry<TKey, TValue> : AsyncRegistry<TKey, TValue>, IAsyncStartable
        where TValue : IKeyed<TKey>
    {
        private readonly IEnumerable<IRegistryProvider<TKey, TValue>> _providers;

        public Registry(IEnumerable<IRegistryProvider<TKey, TValue>> providers)
        {
            _providers = providers;
        }

        public async UniTask StartAsync(CancellationToken ct)
        {
            var items = new List<TValue>();
            foreach (var provider in _providers)
            {
                var provided = await provider.GetAsync(ct);
                items.AddRange(provided);
            }

            await InitializeAsync(items, ct);
        }
    }
}
