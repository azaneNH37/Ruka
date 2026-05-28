using UnityEngine;

namespace Ruka.Core.DI
{
    /// <summary>
    /// Marks a GameObject for automatic VContainer injection by the nearest enclosing <see cref="NestedLifetimeScope"/>.
    /// Add to any MonoBehaviour-bearing GameObject that needs injection without being listed in the scope's <c>autoInjectGameObjects</c>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SelfInjectMarker : MonoBehaviour { }
}
