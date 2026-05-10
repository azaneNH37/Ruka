using Cysharp.Threading.Tasks;
using Ruka.Core.Symbols;

namespace Ruka.UI.Windows
{
    public interface IWindowHandle
    {
        Symbol<WindowId> WindowId { get; }
        WindowLayer Layer { get; }
        bool CloseOnBack { get; }

        UniTask ShowAsync();
        UniTask HideAsync();
    }
}
