using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Ruka.Core.Registry
{
    public interface IRegistryProvider<TKey, TValue>
        where TValue : IKeyed<TKey>
    {
        UniTask<IEnumerable<TValue>> GetAsync(CancellationToken ct = default);
    }
}
