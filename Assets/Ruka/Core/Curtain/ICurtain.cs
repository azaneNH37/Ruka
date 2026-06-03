using System.Threading;
using Cysharp.Threading.Tasks;

namespace Ruka.Core.Curtain
{
    public interface ICurtain
    {
        UniTask ShowAsync(CancellationToken ct);

        void OnProgressUpdated(float progress);

        UniTask OnBeforeRevealAsync(CancellationToken ct);

        UniTask HideAsync(CancellationToken ct);
    }
}
