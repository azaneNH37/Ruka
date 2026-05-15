using Cysharp.Threading.Tasks;
using R3;

namespace Ruka.Core.Saves
{
    public interface ISaveService
    {
        ReadOnlyReactiveProperty<int> ActiveSlot { get; }
        ReadOnlyReactiveProperty<bool> IsProcessing { get; }
        int MaxSlots { get; }
        bool HasActiveSlot { get; }

        UniTask SetActiveSlotAsync(int slot);
        UniTask CreateNewGameFromSlot(int slot);
        UniTask SaveCurrentSlotAsync();
        UniTask<bool> LoadCurrentSlotFullAsync();
        UniTask<SaveSlotSnapshot> LoadSnapshotAsync(int slot);
    }
}
