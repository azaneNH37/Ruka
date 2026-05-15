using Cysharp.Threading.Tasks;

namespace Ruka.Core.Saves
{
    public interface ISaveStorage
    {
        UniTask SaveAsync(string key, byte[] data);
        UniTask<byte[]> LoadAsync(string key);
        bool Exists(string key);
        void Delete(string key);
    }
}
