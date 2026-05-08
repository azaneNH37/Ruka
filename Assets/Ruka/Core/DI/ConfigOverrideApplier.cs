using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Ruka.Core.DI
{
    public sealed class ConfigOverrideApplier
    {
        private readonly Dictionary<Type, Func<object, object>> applyByType = new();
        private readonly HashSet<Type> overrideTypes = new();

        public ConfigOverrideApplier(IReadOnlyList<ScriptableObject> overrides)
        {
            if (overrides == null)
            {
                throw new ArgumentNullException(nameof(overrides));
            }

            for (var i = 0; i < overrides.Count; i++)
            {
                var entry = overrides[i];
                if (entry == null)
                {
                    continue;
                }

                var overrideType = entry.GetType();
                var configType = ResolveOverrideConfigType(overrideType);
                if (configType == null)
                {
                    throw new InvalidOperationException(
                        $"Config override '{overrideType.FullName}' must derive from FeatureConfigOverride<T>.");
                }

                if (!overrideTypes.Add(configType))
                {
                    throw new InvalidOperationException(
                        $"Multiple overrides found for config type '{configType.FullName}'.");
                }

                applyByType[configType] = CreateApplyDelegate(configType, entry);
            }
        }

        public T Apply<T>(T baseline) where T : notnull
        {
            var configType = typeof(T);
            if (!applyByType.TryGetValue(configType, out var apply))
            {
                return baseline;
            }

            return (T)apply(baseline);
        }

        private static Type ResolveOverrideConfigType(Type overrideType)
        {
            while (overrideType != null)
            {
                if (overrideType.IsGenericType && overrideType.GetGenericTypeDefinition() == typeof(FeatureConfigOverride<>))
                {
                    return overrideType.GetGenericArguments()[0];
                }

                overrideType = overrideType.BaseType;
            }

            return null;
        }

        private static Func<object, object> CreateApplyDelegate(Type configType, ScriptableObject instance)
        {
            var method = typeof(ConfigOverrideApplier).GetMethod(
                nameof(CreateApplyDelegate),
                BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new[] { typeof(ScriptableObject) },
                null);

            if (method == null)
            {
                throw new InvalidOperationException("Failed to locate CreateApplyDelegate.");
            }

            var generic = method.MakeGenericMethod(configType);
            return (Func<object, object>)generic.Invoke(null, new object[] { instance });
        }

        private static Func<object, object> CreateApplyDelegate<T>(ScriptableObject instance) where T : notnull
        {
            if (instance is not FeatureConfigOverride<T> typed)
            {
                throw new InvalidOperationException(
                    $"Config override '{instance.GetType().FullName}' must derive from FeatureConfigOverride<{typeof(T).Name}>.");
            }

            return baseline => typed.Apply((T)baseline);
        }
    }
}
