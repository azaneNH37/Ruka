using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;

namespace Ruka.Core.Curtain
{
    public interface ICurtainService
    {
        ReadOnlyReactiveProperty<bool> IsTransitioning { get; }

        UniTask TransitionAsync(
            Func<IProgress<float>, CancellationToken, UniTask> work,
            CancellationToken ct = default);
    }
}
