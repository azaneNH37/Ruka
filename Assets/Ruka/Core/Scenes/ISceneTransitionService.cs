using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using Ruka.Core.Resources;
using Ruka.Core.Symbols;

namespace Ruka.Core.Scenes
{
    public interface ISceneTransitionService
    {
        ReadOnlyReactiveProperty<bool> IsTransitioning { get; }
        ReadOnlyReactiveProperty<float> Progress { get; }

        UniTask TransitionAsync(Symbol<AssetRef> sceneKey, CancellationToken ct = default);
    }
}
