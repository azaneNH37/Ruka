using System.Collections.Generic;
using Ruka.Core.Symbols;
using UnityEngine;
using UnityEngine.Serialization;
using VContainer.Unity;
using Debug = UnityEngine.Debug;

namespace Ruka.Core.DI
{
    public class NestedLifetimeScope : LifetimeScope
    {
        [Header("Scope Identity")]
        [SerializeField, SymbolSelector]
        protected Symbol<ScopeIdentifier> scopeId;

        [Header("Parent Resolution")]
        [SerializeField] protected bool autoParent = true;
        [SerializeField] protected LifetimeScope parentScope;
        [SerializeField, SymbolSelector]
        protected Symbol<ScopeIdentifier> parentScopeId;

        [Header("Debug")]
        [SerializeField]
        protected bool logParentResolution;

        private LifetimeScope resolvedParent;

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
            }

            if (logParentResolution)
            {
                Debug.Log(resolvedParent != null
                    ? $"[NestedScope] {name} -> parent: {resolvedParent.name}"
                    : $"[NestedScope] {name} -> no parent resolved; falling back to VContainer root");
            }

            base.Awake();
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

        // Checks whether 'target' has an intervening LifetimeScope between itself and this scope's
        // transform. Stops traversal at this scope's own transform to avoid climbing into ancestors.
        private bool IsOwnedByNestedScope(GameObject target)
        {
            var current = target.transform.parent;
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
