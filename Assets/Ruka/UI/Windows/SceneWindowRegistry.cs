using Ruka.Core.Symbols;
using UnityEngine;

namespace Ruka.UI.Windows
{
    public sealed class SceneWindowRegistry : MonoBehaviour
    {
        [SerializeField] private Symbol<WindowId> windowId;
        [SerializeField] private bool isRoot;
        [SerializeField] private bool attachToRoot;

        public Symbol<WindowId> WindowId => windowId;
        public bool IsRoot => isRoot;
        public bool AttachToRoot => attachToRoot;
    }
}
