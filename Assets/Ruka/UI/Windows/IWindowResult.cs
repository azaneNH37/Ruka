using Cysharp.Threading.Tasks;

namespace Ruka.UI.Windows
{
    internal interface IWindowResultInternal
    {
        void Cancel();
    }

    internal interface IWindowResult<TResult> : IWindowResultInternal
    {
        UniTask<TResult> GetResultAsync();
    }
}
