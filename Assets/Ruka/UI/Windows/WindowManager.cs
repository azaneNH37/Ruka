using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using Ruka.Core.Resources;
using Ruka.Core.Symbols;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;

namespace Ruka.UI.Windows
{
    public sealed class WindowManager : IWindowService, IWindowRegistry, IAsyncStartable, IDisposable
    {
        private bool _isReady;
        private readonly UniTaskCompletionSource _ready = new();
        private readonly CompositeDisposable _disposables = new();
        private readonly Dictionary<Symbol<WindowId>, WindowEntry> _windows = new();
        private readonly List<Symbol<WindowId>> _escStack = new();
        private readonly Dictionary<Symbol<WindowId>, List<Symbol<WindowId>>> _children = new();
        private readonly Dictionary<Symbol<WindowId>, UniTaskCompletionSource<Unit>> _closeTcs = new();
        private readonly Dictionary<WindowLayer, int> _layerCounts = new();
        private readonly Subject<Unit> _onEmptyBackStack = new();

        private readonly IAssetScope _assetScope;
        private readonly ISceneLoadService _sceneLoadService;
        private readonly WindowConfig _config;
        private Transform _rootTransform;
        private SceneLoadHandle _uiSceneHandle;

        public Observable<Unit> OnEmptyBackStack => _onEmptyBackStack;

        [Inject]
        internal WindowManager(IAssetScope assetScope, ISceneLoadService sceneLoadService, WindowConfig config)
        {
            _assetScope = assetScope;
            _sceneLoadService = sceneLoadService;
            _config = config;
        }

        public async UniTask StartAsync(CancellationToken cancellation)
        {
            await ResolveRootAsync(cancellation);

            _isReady = true;
            _ready.TrySetResult();
        }

        #region IWindowRegistry (self-registration from SceneWindowRegistry)

        IDisposable IWindowRegistry.Register(Symbol<WindowId> windowId, WindowBase window)
        {
            if (windowId.IsEmpty || _windows.ContainsKey(windowId))
                return Disposable.Empty;

            window.WindowId = windowId;
            _windows[windowId] = new WindowEntry(window, ct: default);
            ApplySorting(window);

            if (window.CloseOnBack)
                _escStack.Add(windowId);

            return Disposable.Create(() => UnregisterWindow(windowId));
        }

        private void UnregisterWindow(Symbol<WindowId> windowId)
        {
            if (!_windows.TryGetValue(windowId, out var entry)) return;

            _escStack.Remove(windowId);
            _windows.Remove(windowId);
            _children.Remove(windowId);

            if (_layerCounts.ContainsKey(entry.Window.Layer))
                _layerCounts[entry.Window.Layer]--;

            if (_closeTcs.TryGetValue(windowId, out var tcs))
            {
                tcs.TrySetResult(Unit.Default);
                _closeTcs.Remove(windowId);
            }
        }

        #endregion

        #region IWindowService

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
            await _ready.Task;

            if (_windows.ContainsKey(windowId))
                throw new InvalidOperationException($"Window with ID '{windowId}' is already open.");

            var parentTransform = ResolveParentTransform(parent);

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
            _windows[windowId] = new WindowEntry(window, ct);

            if (!parent.IsEmpty && _windows.TryGetValue(parent, out var parentEntry) && parentEntry.Window != null)
            {
                if (!_children.ContainsKey(parent))
                    _children[parent] = new List<Symbol<WindowId>>();
                _children[parent].Add(windowId);
            }

            if (payload != null)
                window.SetPayload(payload);

            ApplySorting(window);

            if (window.CloseOnBack)
                _escStack.Add(windowId);

            if (ct.CanBeCanceled)
            {
                ct.Register(() =>
                {
                    if (_windows.ContainsKey(windowId))
                        CloseWindowInternal(windowId).Forget();
                }).AddTo(_disposables);
            }

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

            if (!_windows.TryGetValue(windowId, out var entry))
                return;

            if (_children.TryGetValue(windowId, out var childList))
            {
                var childrenCopy = new List<Symbol<WindowId>>(childList);
                foreach (var childId in childrenCopy)
                    await CloseWindowInternal(childId);
                _children.Remove(windowId);
            }

            var window = entry.Window;
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
            if (!_isReady) return false;
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

        #endregion

        public void Dispose()
        {
            _disposables.Dispose();
            _onEmptyBackStack.OnCompleted();
            _onEmptyBackStack.Dispose();

            if (_uiSceneHandle != null)
            {
                _uiSceneHandle.UnloadAsync().Forget();
                _uiSceneHandle = null;
            }
        }

        private async UniTask ResolveRootAsync(CancellationToken ct)
        {
            if (_config.CanvasPrefabKey is { } prefabKey)
            {
                var go = await _assetScope.InstantiateAsync(prefabKey);
                UnityEngine.Object.DontDestroyOnLoad(go);
                EnsureCanvas(go);
                _rootTransform = go.transform;
                return;
            }

            if (_config.UISceneKey is { } sceneKey)
            {
                _uiSceneHandle = _sceneLoadService.Load(sceneKey.Value, LoadSceneMode.Additive, suspendLoad: true);

                while (_uiSceneHandle.Progress < 0.9f)
                {
                    ct.ThrowIfCancellationRequested();
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }

                _uiSceneHandle.Activate();

                var scene = _uiSceneHandle.SceneObject;
                _rootTransform = FindRootCanvasInScene(scene);
                return;
            }

            CreateDefaultRoot();
        }

        private void CreateDefaultRoot()
        {
            var rootCanvas = new GameObject("WindowRoot");
            UnityEngine.Object.DontDestroyOnLoad(rootCanvas);

            var canvas = rootCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = rootCanvas.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = _config.ReferenceResolution;
            scaler.matchWidthOrHeight = _config.MatchWidthOrHeight;

            rootCanvas.AddComponent<GraphicRaycaster>();
            _rootTransform = rootCanvas.transform;
        }

        private static Transform FindRootCanvasInScene(Scene scene)
        {
            var rootObjects = scene.GetRootGameObjects();
            foreach (var go in rootObjects)
            {
                var canvas = go.GetComponent<Canvas>();
                if (canvas != null)
                    return canvas.transform;
            }

            throw new InvalidOperationException(
                $"UI Scene '{scene.name}' does not contain a root Canvas. " +
                "Add a Canvas to the root of the scene.");
        }

        private Transform ResolveParentTransform(Symbol<WindowId> parent)
        {
            if (!parent.IsEmpty && _windows.TryGetValue(parent, out var parentEntry) && parentEntry.Window != null)
                return parentEntry.Window.transform;
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

        private static void EnsureCanvas(GameObject instance)
        {
            if (instance.GetComponent<Canvas>() == null)
                instance.AddComponent<Canvas>();
        }

        private readonly struct WindowEntry
        {
            public readonly WindowBase Window;
            public readonly CancellationToken Ct;

            public WindowEntry(WindowBase window, CancellationToken ct)
            {
                Window = window;
                Ct = ct;
            }
        }
    }
}
