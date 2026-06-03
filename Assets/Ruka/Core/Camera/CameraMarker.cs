using Ruka.Core.Symbols;
using UnityEngine;
using VContainer;

namespace Ruka.Core.Camera
{
    [RequireComponent(typeof(UnityEngine.Camera))]
    public sealed class CameraMarker : MonoBehaviour
    {
        [SerializeField, SymbolSelector] private Symbol<CameraRole> role = CameraRoles.Main;

        private ICameraRegistrar _registrar;
        private UnityEngine.Camera _camera;

        [Inject]
        private void Construct(ICameraRegistrar registrar)
        {
            _registrar = registrar;
            _camera = GetComponent<UnityEngine.Camera>();
        }

        private void OnEnable()
        {
            if (_registrar != null && _camera != null)
                _registrar.SetCamera(role, _camera);
        }

        private void OnDisable()
        {
            if (_registrar != null && _camera != null)
                _registrar.ClearCamera(role, _camera);
        }
    }
}
