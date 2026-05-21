using System;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Ruka.Core.Pool
{
    /// <summary>
    /// IContainerBuilder extensions for standard component pool registration.
    /// </summary>
    public static class PoolBuilderExtensions
    {
        public static void RegisterPool<T>(
            this IContainerBuilder builder,
            T prefab,
            Transform poolRoot,
            PoolSettings settings = default,
            Action<T> onReturn = null)
            where T : MonoBehaviour
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (prefab == null)
            {
                throw new ArgumentNullException(nameof(prefab));
            }

            builder.Register<IObjectPool<T>>(
                resolver => new ComponentPool<T>(
                    create: () => resolver.Instantiate(prefab, poolRoot),
                    onReturn: onReturn,
                    settings: settings),
                Lifetime.Singleton);
        }
    }
}
