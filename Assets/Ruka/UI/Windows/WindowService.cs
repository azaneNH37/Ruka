using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using Ruka.Core.Prefabs;
using Ruka.Core.Resources;
using Ruka.Core.Symbols;
using VContainer;
using VContainer.Unity;

namespace Ruka.UI.Windows
{
    public sealed class WindowService : IWindowService
    {
        private readonly WindowManager _manager;
        private readonly IPrefabFactory _prefabFactory;
        private readonly LifetimeScope _ownerScope;

        public Observable<Unit> OnEmptyBackStack => _manager.OnEmptyBackStack;

        [Inject]
        internal WindowService(WindowManager manager, IPrefabFactory prefabFactory, LifetimeScope ownerScope)
        {
            _manager = manager;
            _prefabFactory = prefabFactory;
            _ownerScope = ownerScope;
        }

        public async UniTask<TResult> OpenWindowAsync<TResult>(
            Symbol<WindowId> windowId,
            Symbol<AssetRef> prefabAsset,
            Symbol<WindowId> parent = default,
            CancellationToken ct = default)
        {
            return await OpenWindowInternal<TResult>(windowId, prefabAsset, parent, payload: null, ct);
        }

        public async UniTask<TResult> OpenWindowAsync<TResult, TPayload>(
            Symbol<WindowId> windowId,
            Symbol<AssetRef> prefabAsset,
            TPayload payload,
            Symbol<WindowId> parent = default,
            CancellationToken ct = default)
        {
            return await OpenWindowInternal<TResult>(windowId, prefabAsset, parent, payload, ct);
        }

        private async UniTask<TResult> OpenWindowInternal<TResult>(
            Symbol<WindowId> windowId,
            Symbol<AssetRef> prefabAsset,
            Symbol<WindowId> parent,
            object payload,
            CancellationToken ct)
        {
            await _manager.WaitUntilReady();

            var ownerLifetime = _ownerScope.GetCancellationTokenOnDestroy();
            var effectiveCt = ct.CanBeCanceled
                ? CancellationTokenSource.CreateLinkedTokenSource(ct, ownerLifetime).Token
                : ownerLifetime;

            effectiveCt.ThrowIfCancellationRequested();

            if (_manager.IsOpen(windowId))
                throw new InvalidOperationException($"Window with ID '{windowId}' is already open.");

            var parentTransform = _manager.GetParentTransform(parent);

            var handle = await _prefabFactory.InstantiateAsync<WindowBase>(
                prefabAsset,
                o => o.Under(parentTransform).WithDiParent(_ownerScope),
                effectiveCt);
            var window = handle.Component;

            if (payload != null)
                window.SetPayload(payload);

            _manager.TrackWindow(windowId, window, parent);

            CancellationTokenRegistration ctRegistration = default;
            if (effectiveCt.CanBeCanceled)
            {
                ctRegistration = effectiveCt.Register(() =>
                {
                    if (_manager.IsOpen(windowId))
                        _manager.CloseWindow(windowId).Forget();
                });
            }

            try
            {
                await window.ShowAsync();

                if (!_manager.IsOpen(windowId))
                    return default;

                if (window is IWindowResult<TResult> resultWindow)
                    return await resultWindow.GetResultAsync();

                if (typeof(TResult) == typeof(Unit))
                {
                    await _manager.WaitForCloseAsync(windowId);
                    return (TResult)(object)Unit.Default;
                }

                await _manager.CloseWindow(windowId);
                throw new InvalidOperationException(
                    $"Window '{windowId}' does not implement IWindowResult<{typeof(TResult).Name}>.");
            }
            finally
            {
                ctRegistration.Dispose();
            }
        }

        public UniTask CloseWindow(Symbol<WindowId> windowId)
        {
            return _manager.CloseWindow(windowId);
        }

        public bool IsOpen(Symbol<WindowId> windowId)
        {
            return _manager.IsOpen(windowId);
        }

        public UniTask<bool> CloseTopmost()
        {
            return _manager.CloseTopmost();
        }
    }
}
