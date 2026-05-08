using Cysharp.Threading.Tasks;
using Ruka.Core.Symbols;

namespace Ruka.Core.Resources
{
    public interface IAssetLoader
    {
        bool IsReady { get; }
        UniTask<SceneResHandle> LoadSceneSingleAsync(Symbol<AssetRef> sceneKey);
    }
}
