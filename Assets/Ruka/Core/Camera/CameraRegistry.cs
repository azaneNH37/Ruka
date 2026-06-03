using System;
using System.Collections.Generic;
using R3;
using Ruka.Core.Symbols;

namespace Ruka.Core.Camera
{
    internal sealed class CameraRegistry : ICameraProvider, ICameraRegistrar, IDisposable
    {
        private readonly Dictionary<Symbol<CameraRole>, ReactiveProperty<UnityEngine.Camera>> _cameras = new();

        public ReadOnlyReactiveProperty<UnityEngine.Camera> GetCamera(Symbol<CameraRole> role)
        {
            return GetOrCreate(role);
        }

        public void SetCamera(Symbol<CameraRole> role, UnityEngine.Camera camera)
        {
            GetOrCreate(role).Value = camera;
        }

        public void ClearCamera(Symbol<CameraRole> role, UnityEngine.Camera camera)
        {
            if (!_cameras.TryGetValue(role, out var prop)) return;
            if (prop.Value == camera)
                prop.Value = null;
        }

        public void Dispose()
        {
            foreach (var prop in _cameras.Values)
                prop.Dispose();
            _cameras.Clear();
        }

        private ReactiveProperty<UnityEngine.Camera> GetOrCreate(Symbol<CameraRole> role)
        {
            if (!_cameras.TryGetValue(role, out var prop))
            {
                prop = new ReactiveProperty<UnityEngine.Camera>(null);
                _cameras[role] = prop;
            }

            return prop;
        }
    }
}
