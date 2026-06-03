using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace Ruka.Core.Resources
{
    public static class SceneLoadServiceExtensions
    {
        public static async UniTask<SceneLoadHandle> LoadAndReportAsync(
            this ISceneLoadService service,
            string address,
            LoadSceneMode mode,
            IProgress<float> progress = null,
            CancellationToken ct = default)
        {
            var handle = service.Load(address, mode, suspendLoad: true);

            while (handle.Progress < 0.9f)
            {
                ct.ThrowIfCancellationRequested();
                progress?.Report(handle.Progress);
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            progress?.Report(1f);
            handle.Activate();

            return handle;
        }
    }
}
