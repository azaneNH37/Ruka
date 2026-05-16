using System.Collections.Generic;
using System.Diagnostics;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer.Unity;
using Debug = UnityEngine.Debug;

namespace Ruka.Core.DI
{
    public abstract class NestedLifetimeScope : LifetimeScope
    {
        [SerializeField] protected bool autoParent = true;
        [SerializeField] protected LifetimeScope parentScope;
        [SerializeField] protected bool logParentRelation;
        [SerializeField] protected bool autoInjectSelf;
        [SerializeField] protected int maxWaitMs = 5000;

        private LifetimeScope resolvedParent;

        protected override void Awake()
        {
            if (autoInjectSelf)
            {
                autoInjectGameObjects ??= new List<GameObject>();
                if (!autoInjectGameObjects.Contains(gameObject))
                {
                    autoInjectGameObjects.Add(gameObject);
                }
            }

            resolvedParent = ResolveParentScope();

            if (resolvedParent != null && resolvedParent.Container == null)
            {
                if (logParentRelation)
                {
                    Debug.Log($"[NestedScope] {name} -> Waiting for parent {resolvedParent.name} to settle...");
                }

                var shouldRun = autoRun;
                autoRun = false;
                base.Awake();
                WaitForParentAndBuild(resolvedParent, shouldRun).Forget();
                return;
            }

            base.Awake();
        }

        protected override LifetimeScope FindParent()
        {
            return resolvedParent != null ? resolvedParent : base.FindParent();
        }

        protected virtual LifetimeScope ResolveParentScope()
        {
            if (autoParent)
            {
                return transform.parent != null ? transform.parent.GetComponentInParent<LifetimeScope>() : null;
            }

            return parentScope;
        }

        private async UniTask WaitForParentAndBuild(LifetimeScope parent, bool shouldRun)
        {
            var stopwatch = Stopwatch.StartNew();
            var cancellationToken = this.GetCancellationTokenOnDestroy();

            while (parent != null && parent.Container == null)
            {
                if (stopwatch.ElapsedMilliseconds > maxWaitMs)
                {
                    Debug.LogError($"[NestedScope] Parent not ready within {maxWaitMs}ms for {name}.", this);
                    return;
                }

                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            stopwatch.Stop();

            if (logParentRelation)
            {
                Debug.Log($"[NestedScope] {name} -> Parent settled. Wait time {stopwatch.ElapsedMilliseconds} ms");
            }

            if (shouldRun)
            {
                Build();
            }
        }
    }
}
