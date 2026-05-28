using System.Collections.Generic;
using Ruka.Core.Symbols;
using UnityEngine;
using UnityEngine.Serialization;
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
        private bool _ownGoMarked;

        protected override void Awake()
        {
            if (!scopeId.IsEmpty)
            {
                ScopeRegistry.Instance.Register(scopeId, this);
            }

            ScanSelfInjectMarkers();
            resolvedParent = ResolveParentScope();

            if (resolvedParent != null)
            {
                parentReference.Object = resolvedParent;
                if (resolvedParent.Container == null)
                    resolvedParent.Build();
            }

            if (logParentResolution)
            {
                Debug.Log(resolvedParent != null
                    ? $"[NestedScope] {name} -> parent: {resolvedParent.name}"
                    : $"[NestedScope] {name} -> no parent resolved; falling back to VContainer root");
            }

            base.Awake();

            if (_ownGoMarked && Container != null)
                InjectOwnComponents();
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
                if (go == gameObject)
                {
                    // Handled after base.Awake() via InjectOwnComponents — not added to autoInjectGameObjects
                    // to avoid recursive injection of children that may already have their own markers.
                    _ownGoMarked = true;
                    continue;
                }
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
