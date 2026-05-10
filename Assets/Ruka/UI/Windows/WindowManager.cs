using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using Ruka.Core.Resources;
using Ruka.Core.Symbols;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;

namespace Ruka.UI.Windows
{
    public sealed class WindowManager : IWindowService, IAsyncStartable, IDisposable
    {
        private bool _isReady;
        private readonly UniTaskCompletionSource _ready = new();
        private readonly CompositeDisposable _disposables = new();
        private readonly Dictionary<Symbol<WindowId>, WindowBase> _windows = new();
        private readonly List<Symbol<WindowId>> _escStack = new();
        private readonly Dictionary<Symbol<WindowId>, List<Symbol<WindowId>>> _children = new();
        private readonly Dictionary<Symbol<WindowId>, UniTaskCompletionSource<Unit>> _closeTcs = new();
        private readonly Dictionary<WindowLayer, int> _layerCounts = new();
        private readonly Subject<Unit> _onEmptyBackStack = new();

        private readonly IAssetScope _assetScope;
        private readonly WindowConfig _config;
        private readonly IReadOnlyList<SceneWindowRegistry> _registries;
        private Transform _rootTransform;

        public Observable<Unit> OnEmptyBackStack => _onEmptyBackStack;

        [Inject]
        internal WindowManager(
            IAssetScope assetScope,
            WindowConfig config,
            IReadOnlyList<SceneWindowRegistry> registries)
        {
            _assetScope = assetScope;
            _config = config;
            _registries = registries;
        }

        public UniTask StartAsync(CancellationToken cancellation)
        {
            ResolveRoot();

            if (_registries != null)
            {
                foreach (var registry in _registries)
                {
                    if (registry == null) continue;
                    if (!registry.TryGetComponent<WindowBase>(out var window)) continue;

                    var id = registry.WindowId;
                    if (id.IsEmpty || _windows.ContainsKey(id)) continue;

                    if (!registry.IsRoot && registry.AttachToRoot)
                    {
                        window.transform.SetParent(_rootTransform, false);
                    }

                    window.WindowId = id;
                    _windows[id] = window;

                    if (window.CloseOnBack)
                        _escStack.Add(id);

                    ApplySorting(window);
                }
            }

            _isReady = true;
            _ready.TrySetResult();
            return UniTask.CompletedTask;
        }

        public async UniTask<TResult> OpenWindowAsync<TResult>(
            Symbol<WindowId> windowId,
            Symbol<AssetRef> prefabAsset,
            Symbol<WindowId> parent = default)
        {
            return await OpenWindowInternal<TResult>(windowId, prefabAsset, parent, payload: null);
        }

        public async UniTask<TResult> OpenWindowAsync<TResult, TPayload>(
            Symbol<WindowId> windowId,
            Symbol<AssetRef> prefabAsset,
            TPayload payload,
            Symbol<WindowId> parent = default)
        {
            return await OpenWindowInternal<TResult>(windowId, prefabAsset, parent, payload);
        }

        private async UniTask<TResult> OpenWindowInternal<TResult>(
            Symbol<WindowId> windowId,
            Symbol<AssetRef> prefabAsset,
            Symbol<WindowId> parent,
            object payload)
        {
            await _ready.Task;

            if (_windows.ContainsKey(windowId))
                throw new InvalidOperationException($"Window with ID '{windowId}' is already open.");

            Transform parentTransform = ResolveParentTransform(parent);

            var instance = await _assetScope.InstantiateAsync(prefabAsset);
            var window = instance.GetComponent<WindowBase>();
            if (window == null)
            {
                UnityEngine.Object.Destroy(instance);
                throw new InvalidOperationException($"Prefab '{prefabAsset}' has no WindowBase component.");
            }

            EnsureCanvas(instance);
            instance.transform.SetParent(parentTransform, false);

            window.WindowId = windowId;
            _windows[windowId] = window;

            if (!parent.IsEmpty && _windows.TryGetValue(parent, out var parentWindow) && parentWindow != null)
            {
                if (!_children.ContainsKey(parent))
                    _children[parent] = new List<Symbol<WindowId>>();
                _children[parent].Add(windowId);
            }

            if (payload != null)
            {
                SetPayload(window, payload);
            }

            ApplySorting(window);

            if (window.CloseOnBack)
                _escStack.Add(windowId);

            await window.ShowAsync();

            if (!_windows.ContainsKey(windowId))
                return default;

            if (window is IWindowResult<TResult> resultWindow)
                return await resultWindow.GetResultAsync();

            if (typeof(TResult) == typeof(Unit))
            {
                var tcs = new UniTaskCompletionSource<Unit>();
                _closeTcs[windowId] = tcs;
                return (TResult)(object)await tcs.Task;
            }

            await CloseWindowInternal(windowId);
            throw new InvalidOperationException(
                $"Window '{windowId}' does not implement IWindowResult<{typeof(TResult).Name}>.");
        }

        public async UniTask CloseWindow(Symbol<WindowId> windowId)
        {
            await _ready.Task;
            await CloseWindowInternal(windowId);
        }

        private async UniTask CloseWindowInternal(Symbol<WindowId> windowId)
        {
            _escStack.Remove(windowId);

            if (!_windows.TryGetValue(windowId, out var window))
                return;

            if (_children.TryGetValue(windowId, out var childList))
            {
                var childrenCopy = new List<Symbol<WindowId>>(childList);
                foreach (var childId in childrenCopy)
                    await CloseWindowInternal(childId);
                _children.Remove(windowId);
            }

            await window.HideAsync();

            if (window is IWindowResultInternal resultInternal)
                resultInternal.Cancel();

            _windows.Remove(windowId);

            if (_layerCounts.ContainsKey(window.Layer))
                _layerCounts[window.Layer]--;

            _children.Remove(windowId);

            if (_closeTcs.TryGetValue(windowId, out var tcs))
            {
                tcs.TrySetResult(Unit.Default);
                _closeTcs.Remove(windowId);
            }

            if (window != null && window.gameObject != null)
                UnityEngine.Object.Destroy(window.gameObject);
        }

        public bool IsOpen(Symbol<WindowId> windowId)
        {
            if (!_isReady)
                return false;
            return _windows.ContainsKey(windowId);
        }

        public async UniTask<bool> CloseTopmost()
        {
            await _ready.Task;

            if (_escStack.Count == 0)
            {
                _onEmptyBackStack.OnNext(Unit.Default);
                return false;
            }

            var topId = _escStack[^1];
            await CloseWindowInternal(topId);
            return true;
        }

        public void Dispose()
        {
            _disposables.Dispose();
            _onEmptyBackStack.OnCompleted();
            _onEmptyBackStack.Dispose();
        }

        private void ResolveRoot()
        {
            WindowBase rootWindow = null;
            var rootCount = 0;

            if (_registries != null)
            {
                foreach (var registry in _registries)
                {
                    if (registry == null) continue;
                    if (!registry.IsRoot) continue;

                    rootCount++;
                    rootWindow = registry.GetComponent<WindowBase>();
                }
            }

            if (rootCount > 1)
                throw new InvalidOperationException(
                    "Multiple root windows found in scene. Only one SceneWindowRegistry with isRoot=true is allowed.");

            if (rootCount == 1 && rootWindow != null)
            {
                _rootTransform = rootWindow.transform;
                EnsureCanvas(rootWindow.gameObject);
                return;
            }

            var rootCanvas = new GameObject("RootCanvas");

            var canvas = rootCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = rootCanvas.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = _config.ReferenceResolution;
            scaler.matchWidthOrHeight = _config.MatchWidthOrHeight;

            rootCanvas.AddComponent<GraphicRaycaster>();
            _rootTransform = rootCanvas.transform;
        }

        private Transform ResolveParentTransform(Symbol<WindowId> parent)
        {
            if (!parent.IsEmpty && _windows.TryGetValue(parent, out var parentWindow) && parentWindow != null)
                return parentWindow.transform;
            return _rootTransform;
        }

        private void ApplySorting(WindowBase window)
        {
            var canvas = window.GetComponent<Canvas>();
            if (canvas == null) return;

            canvas.overrideSorting = true;
            if (!_layerCounts.TryGetValue(window.Layer, out var count))
                _layerCounts[window.Layer] = 0;
            canvas.sortingOrder = (int)window.Layer * _config.LayerSpacing + _layerCounts[window.Layer];
            _layerCounts[window.Layer]++;
        }

        private static void SetPayload(WindowBase window, object payload)
        {
            var windowType = window.GetType();
            while (windowType != null && windowType != typeof(WindowBase))
            {
                if (windowType.IsGenericType && windowType.GetGenericTypeDefinition() == typeof(WindowBase<>))
                {
                    var payloadProp = windowType.GetProperty("Payload");
                    payloadProp?.SetValue(window, payload);
                    return;
                }
                if (windowType.IsGenericType && windowType.GetGenericTypeDefinition() == typeof(WindowBase<,>))
                {
                    var payloadProp = windowType.GetProperty("Payload");
                    payloadProp?.SetValue(window, payload);
                    return;
                }
                windowType = windowType.BaseType;
            }
        }

        private static void EnsureCanvas(GameObject instance)
        {
            if (instance.GetComponent<Canvas>() == null)
                instance.AddComponent<Canvas>();
        }
    }
}
