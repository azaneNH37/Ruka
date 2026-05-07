using System;
using UnityEngine;

namespace Ruka.Utils.Core
{
    public static class ComponentValidatorExtensions
    {
        public static bool HasComponent<T>(this GameObject gameObject) where T : Component
        {
            if (gameObject == null)
            {
                throw new ArgumentNullException(nameof(gameObject));
            }

            return gameObject.TryGetComponent<T>(out _);
        }

        public static T RequireComponent<T>(this GameObject gameObject, string context = null)
            where T : Component
        {
            if (gameObject == null)
            {
                throw new ArgumentNullException(nameof(gameObject));
            }

            if (gameObject.TryGetComponent<T>(out var component))
            {
                return component;
            }

            var label = string.IsNullOrEmpty(context) ? gameObject.name : context;
            throw new InvalidOperationException($"Missing required component {typeof(T).Name} on {label}.");
        }
    }
}
