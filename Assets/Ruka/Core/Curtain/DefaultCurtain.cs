using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Ruka.Core.Curtain
{
    internal sealed class DefaultCurtain : ICurtain
    {
        private readonly CanvasGroup _canvasGroup;

        internal DefaultCurtain()
        {
            var root = new GameObject("DefaultCurtain", typeof(RectTransform));
            Object.DontDestroyOnLoad(root);

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;

            var imageGo = new GameObject("BlackScreen", typeof(RectTransform));
            imageGo.transform.SetParent(root.transform, false);

            var image = imageGo.AddComponent<Image>();
            image.color = Color.black;
            image.raycastTarget = true;

            var rect = imageGo.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            _canvasGroup = root.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f;
        }

        public UniTask ShowAsync(CancellationToken ct)
        {
            _canvasGroup.alpha = 1f;
            return UniTask.CompletedTask;
        }

        public void OnProgressUpdated(float progress) { }

        public UniTask OnBeforeRevealAsync(CancellationToken ct) => UniTask.CompletedTask;

        public UniTask HideAsync(CancellationToken ct)
        {
            _canvasGroup.alpha = 0f;
            return UniTask.CompletedTask;
        }
    }
}
