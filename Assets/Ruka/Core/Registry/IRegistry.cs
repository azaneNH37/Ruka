using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Ruka.Core.Registry
{
    public interface IRegistry<TKey, TValue>
        where TValue : IKeyed<TKey>
    {
        bool IsReady { get; }
        UniTask WaitUntilReady { get; }
        IReadOnlyDictionary<TKey, TValue> All { get; }
        TValue Get(TKey key);
        bool TryGet(TKey key, out TValue value);
    }
}
