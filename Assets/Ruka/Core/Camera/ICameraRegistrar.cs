using Ruka.Core.Symbols;

namespace Ruka.Core.Camera
{
    public interface ICameraRegistrar
    {
        void SetCamera(Symbol<CameraRole> role, UnityEngine.Camera camera);
        void ClearCamera(Symbol<CameraRole> role, UnityEngine.Camera camera);
    }
}
