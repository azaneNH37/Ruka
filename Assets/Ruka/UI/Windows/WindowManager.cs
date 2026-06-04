using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using Ruka.Core.Prefabs;
using Ruka.Core.Resources;
using Ruka.Core.Symbols;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;

namespace Ruka.UI.Windows
{
    public sealed class WindowManager : IWindowRegistry, IAsyncStartable, IDisposable
    {
        private bool _isReady;
        private readonly UniTaskCompletionSource _ready = new();
        private readonly Dictionary<Symbol<WindowId>, WindowEntry> _windows = new();
        private readonly List<Symbol<WindowId>> _escStack = new();
        private readonly Dictionary<Symbol<WindowId>, List<Symbol<WindowId>>> _children = new();
        private readonly Dictionary<Symbol<WindowId>, UniTaskCompletionSource> _closeTcs = new();
        private readonly Dictionary<WindowLayer, int> _layerCounts = new();
        private readonly Subject<Unit> _onEmptyBackStack = new();

        private readonly IPrefabFactory _prefabFactory;
        private readonly ISceneLoadService _sceneLoadService;
        private readonly WindowConfig _config;
        private Transform _rootTransform;
        private SceneLoadHandle _uiSceneHandle;
        private PrefabInstanceHandle _rootCanvasHandle;

        public Observable<Unit> OnEmptyBackStack => _onEmptyBackStack;

        [Inject]
        internal WindowManager(IPrefabFactory prefabFactory, ISceneLoadService sceneLoadService, WindowConfig config)
        {
            _prefabFactory = prefabFactory;
            _sceneLoadService = sceneLoadService;
            _config = config;
        }

        public async UniTask StartAsync(CancellationToken cancellation)
        {
            await ResolveRootAsync(cancellation);

            _isReady = true;
            _ready.TrySetResult();
        }

        #region Internal API for WindowService

        internal UniTask WaitUntilReady() => _ready.Task;

        internal Transform GetParentTransform(Symbol<WindowId> parent)
        {
            if (!parent.IsEmpty && _windows.TryGetValue(parent, out var parentEntry) && parentEntry.Window != null)
                return parentEntry.Window.transform;
            return _rootTransform;
        }

        internal void TrackWindow(Symbol<WindowId> windowId, WindowBase window, Symbol<WindowId> parent)
        {
            if (_windows.ContainsKey(windowId))
                throw new InvalidOperationException($"Window with ID '{windowId}' is already open.");

            TrackWindowCore(windowId, window, parent);
        }

        internal UniTask WaitForCloseAsync(Symbol<WindowId> windowId)
        {
            if (!_windows.ContainsKey(windowId))
                return UniTask.CompletedTask;

            if (_closeTcs.TryGetValue(windowId, out var existing))
                return existing.Task;

            var tcs = new UniTaskCompletionSource();
            _closeTcs[windowId] = tcs;
            return tcs.Task;
        }

        #endregion

        #region IWindowRegistry (self-registration from SceneWindowRegistry)

        IDisposable IWindowRegistry.Register(Symbol<WindowId> windowId, WindowBase window)
        {
            if (windowId.IsEmpty || _windows.ContainsKey(windowId))
                return Disposable.Empty;

            TrackWindowCore(windowId, window, default);
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
                tcs.TrySetResult();
                _closeTcs.Remove(windowId);
            }
        }

        #endregion

        #region Window closing

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
                tcs.TrySetResult();
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
            _onEmptyBackStack.OnCompleted();
            _onEmptyBackStack.Dispose();

            _rootCanvasHandle?.Dispose();
            _rootCanvasHandle = null;

            if (_uiSceneHandle != null)
            {
                _uiSceneHandle.UnloadAsync().Forget();
                _uiSceneHandle = null;
            }
        }

        private void TrackWindowCore(Symbol<WindowId> windowId, WindowBase window, Symbol<WindowId> parent)
        {
            window.WindowId = windowId;
            _windows[windowId] = new WindowEntry(window);

            EnsureCanvas(window.gameObject);
            ApplySorting(window);

            if (!parent.IsEmpty && _windows.ContainsKey(parent))
            {
                if (!_children.ContainsKey(parent))
                    _children[parent] = new List<Symbol<WindowId>>();
                _children[parent].Add(windowId);
            }

            if (window.CloseOnBack)
                _escStack.Add(windowId);
        }

        private async UniTask ResolveRootAsync(CancellationToken ct)
        {
            if (_config.CanvasPrefabKey is { } prefabKey)
            {
                _rootCanvasHandle = await _prefabFactory.InstantiateAsync(prefabKey, ct: ct);
                var go = _rootCanvasHandle.GameObject;
                UnityEngine.Object.DontDestroyOnLoad(go);
                EnsureCanvas(go);
                _rootTransform = go.transform;
                return;
            }

            if (_config.UISceneKey is { } sceneKey)
            {
                _uiSceneHandle = await _sceneLoadService.LoadAndReportAsync(sceneKey, LoadSceneMode.Additive, null, ct);

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
                if (go.TryGetComponent<Canvas>(out var canvas))
                    return canvas.transform;
            }

            throw new InvalidOperationException(
                $"UI Scene '{scene.name}' does not contain a root Canvas. " +
                "Add a Canvas to the root of the scene.");
        }

        private void ApplySorting(WindowBase window)
        {
            if (!window.TryGetComponent<Canvas>(out var canvas)) return;

            canvas.overrideSorting = true;
            if (!_layerCounts.TryGetValue(window.Layer, out var count))
                _layerCounts[window.Layer] = 0;
            canvas.sortingOrder = (int)window.Layer * _config.LayerSpacing + _layerCounts[window.Layer];
            _layerCounts[window.Layer]++;
        }

        private static void EnsureCanvas(GameObject instance)
        {
            if (!instance.TryGetComponent<Canvas>(out _))
                instance.AddComponent<Canvas>();
        }

        private readonly struct WindowEntry
        {
            public readonly WindowBase Window;

            public WindowEntry(WindowBase window)
            {
                Window = window;
            }
        }
    }
}
