using UnityEngine;

namespace Ruka.Core.Resources
{
    [DisallowMultipleComponent]
    internal class AssetReleaseHook : MonoBehaviour
    {
        private ReleaseToken _token;
        private AssetScope _ownerScope;
        private bool _released;

        internal void Init(ReleaseToken token, AssetScope owner)
        {
            _token = token;
            _ownerScope = owner;
        }

        private void OnDestroy()
        {
            if (_released) return;
            _released = true;

            _ownerScope?.ReleaseToken(_token);
        }
    }
}
