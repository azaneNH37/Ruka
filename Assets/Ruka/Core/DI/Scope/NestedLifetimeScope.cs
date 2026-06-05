using System.Collections.Generic;
using System.Reflection;
using Ruka.Core.Symbols;
using UnityEngine;
using UnityEngine.Serialization;
using VContainer;
using VContainer.Unity;
using Debug = UnityEngine.Debug;

namespace Ruka.Core.DI
{
    /// <summary>
    /// A <see cref="LifetimeScope"/> with Symbol-based scope naming, flexible parent resolution,
    /// and automatic <see cref="SelfInjectMarker"/> scanning.
    /// </summary>
    public class NestedLifetimeScope : LifetimeScope
    {
        /// <summary>Unique name registered in <see cref="ScopeRegistry"/>. Leave empty to skip registration.</summary>
        [Header("Scope Identity")]
        [SerializeField, SymbolSelector]
        protected Symbol<ScopeIdentifier> scopeId;
        
        /// <summary>
        /// When true, walks up the transform hierarchy to find the nearest ancestor <see cref="LifetimeScope"/> as parent.
        /// Disable when specifying <see cref="parentScope"/> or <see cref="parentScopeId"/> explicitly.
        /// </summary>
        [Header("Parent Resolution")]
        
        [SerializeField] protected bool autoParent = true;

        /// <summary>Explicit parent scope reference. Evaluated only when <see cref="autoParent"/> is false and <see cref="parentScopeId"/> is empty.</summary>
        [SerializeField] protected LifetimeScope parentScope;

        /// <summary>
        /// Resolves the parent by Symbol lookup in <see cref="ScopeRegistry"/>.
        /// Evaluated only when <see cref="autoParent"/> is false and <see cref="parentScope"/> is null.
        /// The target scope must have registered before this scope's Awake runs.
        /// </summary>
        [SerializeField, SymbolSelector]
        protected Symbol<ScopeIdentifier> parentScopeId;
        
        /// <summary>Logs the resolved parent scope name on Awake. Enable to diagnose parent resolution failures.</summary>
        [Header("Debug")]
        [SerializeField]
        protected bool logParentResolution;

        private LifetimeScope resolvedParent;
        private bool _preBuilt;

        protected override void Awake()
        {
            EnsurePreBuilt();

            if (logParentResolution)
            {
                Debug.Log(resolvedParent != null
                    ? $"[NestedScope] {name} -> parent: {resolvedParent.name}"
                    : $"[NestedScope] {name} -> no parent resolved; falling back to VContainer root");
            }

            // Guard: if this scope was already built by a child that force-built its parent before
            // this Awake ran, skip base.Awake to prevent a second Build() call that would create a
            // duplicate container and re-dispatch IAsyncStartable entry points.
            if (Container == null)
                base.Awake();

            if (Container != null)
                InjectOwnComponents();

#if UNITY_EDITOR || DEBUG
            ValidateUninjectedComponents();
#endif
        }

        /// <summary>
        /// Runs all pre-build initialization (ScopeRegistry registration, marker scanning,
        /// parent resolution) exactly once. Called from <see cref="Awake"/> and recursively
        /// when a child scope force-builds this scope before Unity delivers Awake.
        /// </summary>
        private void EnsurePreBuilt()
        {
            if (_preBuilt) return;
            _preBuilt = true;

            if (!scopeId.IsEmpty)
            {
                ScopeRegistry.Instance.Register(scopeId, this);
            }

            ScanSelfInjectMarkers();

            // Respect externally-set parent (e.g. PrefabFactory.WithDiParent)
            if (parentReference.Object != null)
            {
                resolvedParent = parentReference.Object as LifetimeScope;
            }
            else
            {
                resolvedParent = ResolveParentScope();
                if (resolvedParent != null)
                    parentReference.Object = resolvedParent;
            }

            if (resolvedParent != null && resolvedParent.Container == null)
            {
                if (resolvedParent is NestedLifetimeScope nested)
                    nested.EnsurePreBuilt();
                resolvedParent.Build();
            }
        }

        protected override void OnDestroy()
        {
            if (!scopeId.IsEmpty)
            {
                ScopeRegistry.Instance.Unregister(scopeId, this);
            }

            base.OnDestroy();
        }

        protected override LifetimeScope FindParent()
        {
            return resolvedParent != null ? resolvedParent : base.FindParent();
        }

        protected virtual LifetimeScope ResolveParentScope()
        {
            if (autoParent && transform.parent != null)
            {
                return transform.parent.GetComponentInParent<LifetimeScope>();
            }

            if (parentScope != null)
            {
                return parentScope;
            }

            if (!parentScopeId.IsEmpty && ScopeRegistry.Instance.TryFind(parentScopeId, out var found) && found != this)
            {
                return found;
            }

            return null;
        }

        private void ScanSelfInjectMarkers()
        {
            var markers = GetComponentsInChildren<SelfInjectMarker>(includeInactive: true);
            if (markers == null || markers.Length == 0)
            {
                return;
            }

            autoInjectGameObjects ??= new List<GameObject>();

            for (var i = 0; i < markers.Length; i++)
            {
                var marker = markers[i];
                if (marker == null) continue;

                var go = marker.gameObject;
                if (go == gameObject) continue;
                if (IsOwnedByNestedScope(go)) continue;

                if (!autoInjectGameObjects.Contains(go))
                {
                    autoInjectGameObjects.Add(go);
                }
            }
        }

        private void InjectOwnComponents()
        {
            var components = GetComponents<MonoBehaviour>();
            foreach (var mb in components)
                if (mb != null) Container.Inject(mb);
        }


#if UNITY_EDITOR || DEBUG
        private void ValidateUninjectedComponents()
        {
            var injectedGOs = new HashSet<GameObject> { gameObject };
            if (autoInjectGameObjects != null)
                foreach (var go in autoInjectGameObjects)
                    if (go != null) injectedGOs.Add(go);

            WarnUninjectedRecursive(transform, injectedGOs);
        }

        private void WarnUninjectedRecursive(Transform current, HashSet<GameObject> injectedGOs)
        {
            if (current != transform && current.TryGetComponent<LifetimeScope>(out _))
                return;

            if (!injectedGOs.Contains(current.gameObject))
            {
                foreach (var mb in current.GetComponents<MonoBehaviour>())
                {
                    if (mb == null) continue;
                    if (HasInjectMembers(mb.GetType()))
                    {
                        Debug.LogWarning(
                            $"[NestedScope] {mb.GetType().Name} on '{current.gameObject.name}' has [Inject] members " +
                            $"but is not in scope '{name}' injection path. Add SelfInjectMarker to the GameObject.",
                            current.gameObject);
                        break;
                    }
                }
            }

            for (var i = 0; i < current.childCount; i++)
                WarnUninjectedRecursive(current.GetChild(i), injectedGOs);
        }

        private static bool HasInjectMembers(System.Type type)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var member in type.GetMembers(flags))
            {
                foreach (var attr in member.GetCustomAttributes(inherit: true))
                {
                    var t = attr.GetType();
                    if (t.Name == "InjectAttribute" && t.Namespace == "VContainer")
                        return true;
                }
            }
            return false;
        }
#endif

        // Checks whether 'target' has an intervening LifetimeScope between itself and this scope's
        // transform. Stops traversal at this scope's own transform to avoid climbing into ancestors.
        private bool IsOwnedByNestedScope(GameObject target)
        {
            var current = target.transform;
            while (current != null && current != transform)
            {
                var scope = current.GetComponent<LifetimeScope>();
                if (scope != null && scope != this)
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }
    }
}
