using R3;
using Ruka.Core.Symbols;
using UnityEngine;

namespace Ruka.Core.Camera
{
    public interface ICameraProvider
    {
        ReadOnlyReactiveProperty<UnityEngine.Camera> GetCamera(Symbol<CameraRole> role);
    }
}
