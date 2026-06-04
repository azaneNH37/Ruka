using UnityEngine;

namespace Ruka.Core.Prefabs
{
    [DisallowMultipleComponent]
    internal sealed class PrefabReleaseHook : MonoBehaviour
    {
        private PrefabInstanceHandle _handle;

        internal void Init(PrefabInstanceHandle handle)
        {
            _handle = handle;
        }

        private void OnDestroy()
        {
            _handle?.Dispose();
        }
    }
}
