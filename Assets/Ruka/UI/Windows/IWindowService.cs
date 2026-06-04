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
            WindowOpenContext context,
            Symbol<WindowId> parent = default);

        UniTask<TResult> OpenWindowAsync<TResult, TPayload>(
            Symbol<WindowId> windowId,
            Symbol<AssetRef> prefabAsset,
            TPayload payload,
            WindowOpenContext context,
            Symbol<WindowId> parent = default);

        UniTask CloseWindow(Symbol<WindowId> windowId);
        bool IsOpen(Symbol<WindowId> windowId);

        UniTask<bool> CloseTopmost();

        Observable<Unit> OnEmptyBackStack { get; }
    }
}
