using R3;

namespace Ruka.UI.Windows
{
    public interface IWindowBackInputProvider
    {
        Observable<Unit> OnBack { get; }
    }
}
