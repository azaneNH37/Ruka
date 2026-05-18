using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ruka.Core.DI
{
    /// <summary>
    /// Applies a set of <see cref="FeatureConfigOverride{T}"/> assets to config baselines during scope build.
    /// Constructed by <see cref="GroupedLifetimeScope"/>; do not instantiate directly.
    /// </summary>
    public sealed class ConfigOverrideApplier
    {
        private readonly Dictionary<Type, List<IConfigOverrideApply>> applyByType = new();

        public ConfigOverrideApplier(IReadOnlyList<ScriptableObject> overrides)
        {
            if (overrides == null)
            {
                throw new ArgumentNullException(nameof(overrides));
            }

            for (var i = 0; i < overrides.Count; i++)
            {
                var entry = overrides[i];
                if (entry is not IConfigOverrideApply apply)
                {
                    continue;
                }

                var configType = ResolveOverrideConfigType(entry.GetType());
                if (configType == null)
                {
                    throw new InvalidOperationException(
                        $"Config override '{entry.GetType().FullName}' must derive from FeatureConfigOverride<T>.");
                }

                if (!applyByType.TryGetValue(configType, out var applies))
                {
                    applies = new List<IConfigOverrideApply>();
                    applyByType[configType] = applies;
                }

                applies.Add(apply);
            }
        }

        public T Apply<T>(T baseline) where T : IFeatureConfig
        {
            if (!applyByType.TryGetValue(typeof(T), out var applies))
            {
                return baseline;
            }

            object result = baseline;
            foreach (var apply in applies)
            {
                result = apply.Apply(result);
            }

            return (T)result;
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
    }
}
