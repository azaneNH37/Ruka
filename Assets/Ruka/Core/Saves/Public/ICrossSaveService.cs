using Cysharp.Threading.Tasks;
using R3;

namespace Ruka.Core.Saves
{
    public interface ICrossSaveService
    {
        ReadOnlyReactiveProperty<bool> IsProcessing { get; }

        UniTask SaveCrossAsync();
        UniTask<bool> LoadCrossAsync();
        UniTask ResetCrossAsync();
    }
}
