using System;
using Ruka.Core.Symbols;
using UnityEngine;
using VContainer;

namespace Ruka.UI.Windows
{
    public sealed class SceneWindowRegistry : MonoBehaviour
    {
        [SerializeField, SymbolSelector(AllowManualInput = true)] private Symbol<WindowId> windowId;

        private IDisposable _registration;

        public Symbol<WindowId> WindowId => windowId;

        [Inject]
        private void Construct(IWindowRegistry registry)
        {
            if (windowId.IsEmpty) return;
            if (!TryGetComponent<WindowBase>(out var window)) return;

            _registration = registry.Register(windowId, window);
        }

        private void OnDestroy()
        {
            _registration?.Dispose();
            _registration = null;
        }
    }
}
