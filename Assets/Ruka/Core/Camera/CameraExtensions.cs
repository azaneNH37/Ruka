using R3;

namespace Ruka.Core.Camera
{
    public static class CameraExtensions
    {
        public static ReadOnlyReactiveProperty<UnityEngine.Camera> MainCamera(this ICameraProvider provider)
            => provider.GetCamera(CameraRoles.Main);

        public static void SetMainCamera(this ICameraRegistrar registrar, UnityEngine.Camera camera)
            => registrar.SetCamera(CameraRoles.Main, camera);

        public static void ClearMainCamera(this ICameraRegistrar registrar, UnityEngine.Camera camera)
            => registrar.ClearCamera(CameraRoles.Main, camera);
    }
}
