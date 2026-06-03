using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using Ruka.Core.Resources;
using Ruka.Core.Symbols;

namespace Ruka.UI.Windows
{
    public interface IWindowService
    {
        UniTask<TResult> OpenWindowAsync<TResult>(
            Symbol<WindowId> windowId,
            Symbol<AssetRef> prefabAsset,
            Symbol<WindowId> parent = default,
            CancellationToken ct = default);

        UniTask<TResult> OpenWindowAsync<TResult, TPayload>(
            Symbol<WindowId> windowId,
            Symbol<AssetRef> prefabAsset,
            TPayload payload,
            Symbol<WindowId> parent = default,
            CancellationToken ct = default);

        UniTask CloseWindow(Symbol<WindowId> windowId);
        bool IsOpen(Symbol<WindowId> windowId);

        UniTask<bool> CloseTopmost();

        Observable<Unit> OnEmptyBackStack { get; }
    }
}
