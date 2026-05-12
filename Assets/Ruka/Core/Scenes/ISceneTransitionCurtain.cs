using System.Threading;
using Cysharp.Threading.Tasks;

namespace Ruka.Core.Scenes
{
    public interface ISceneTransitionCurtain
    {
        UniTask ShowAsync(CancellationToken ct);

        void OnProgressUpdated(float progress);

        UniTask OnLoadedAsync(CancellationToken ct);

        UniTask HideAsync(CancellationToken ct);
    }
}
