using System.IO;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Ruka.Core.Saves
{
    internal sealed class LocalFileStorage : ISaveStorage
    {
        private readonly string _basePath;

        public LocalFileStorage(SavesConfig config)
        {
            _basePath = Path.Combine(Application.persistentDataPath, config.SaveFolder);
            if (!Directory.Exists(_basePath))
                Directory.CreateDirectory(_basePath);
        }

        public async UniTask SaveAsync(string key, byte[] data)
        {
            var path = Path.Combine(_basePath, key);
            var tempPath = path + ".tmp";

            using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
            {
                await fs.WriteAsync(data, 0, data.Length);
                await fs.FlushAsync();
            }

            if (File.Exists(path))
            {
                File.Replace(tempPath, path, null);
            }
            else
            {
                File.Move(tempPath, path);
            }
        }

        public async UniTask<byte[]> LoadAsync(string key)
        {
            var path = Path.Combine(_basePath, key);
            if (!File.Exists(path))
                return null;
            return await File.ReadAllBytesAsync(path);
        }

        public bool Exists(string key)
        {
            return File.Exists(Path.Combine(_basePath, key));
        }

        public void Delete(string key)
        {
            var path = Path.Combine(_basePath, key);
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
