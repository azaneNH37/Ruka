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
        [ThreadStatic]
        private static ConfigOverrideApplier _current;

        internal static ConfigOverrideApplier Current => _current;

        /// <summary>
        /// Sets this applier as the active context for the current thread for the duration of the returned scope.
        /// Restores the previous applier on dispose, making nested scopes safe.
        /// </summary>
        internal IDisposable BeginScope()
        {
            var previous = _current;
            _current = this;
            return new ScopeHandle(previous);
        }

        private sealed class ScopeHandle : IDisposable
        {
            private readonly ConfigOverrideApplier _previous;
            public ScopeHandle(ConfigOverrideApplier previous) => _previous = previous;
            public void Dispose() => _current = _previous;
        }

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
