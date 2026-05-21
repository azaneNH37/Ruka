using System;
using UnityEngine;

namespace Ruka.Core.Pool
{
    /// <summary>
    /// ObjectPool variant for MonoBehaviour components.
    /// </summary>
    public class ComponentPool<T> : ObjectPool<T> where T : MonoBehaviour
    {
        public ComponentPool(
            Func<T> create,
            Action<T> onReturn = null,
            Action<T> onDestroy = null,
            PoolSettings settings = default)
            : base(
                create,
                Deactivate,
                WrapReturn(onReturn),
                onDestroy ?? DestroyGameObject,
                settings)
        {
        }

        public override T Get()
        {
            var instance = base.Get();
            instance.gameObject.SetActive(true);
            return instance;
        }

        private static void Deactivate(T instance)
        {
            instance.gameObject.SetActive(false);
        }

        private static Action<T> WrapReturn(Action<T> onReturn)
        {
            return instance =>
            {
                onReturn?.Invoke(instance);
                instance.gameObject.SetActive(false);
            };
        }

        private static void DestroyGameObject(T instance)
        {
            if (instance != null)
            {
                UnityEngine.Object.Destroy(instance.gameObject);
            }
        }
    }
}
