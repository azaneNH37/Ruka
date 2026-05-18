using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace Ruka.Core.DI
{
    /// <summary>
    /// A <see cref="NestedLifetimeScope"/> that installs services by executing all <see cref="IFeatureInstaller"/> types
    /// collected from its assigned <see cref="FeatureGroupCollector"/> assets, with optional per-scope config substitution.
    /// </summary>
    public class GroupedLifetimeScope : NestedLifetimeScope
    {
        /// <summary>Collectors whose discovered installer types will be installed into this scope. Attach one per required installer group.</summary>
        [SerializeField] protected List<FeatureGroupCollector> collectors;

        /// <summary>
        /// <see cref="FeatureConfigOverride{T}"/> assets applied to any config registered via <see cref="ContainerBuilderExtensions.RegisterConfig{T}"/> during this scope's build.
        /// Overrides are applied in list order; the last override for a given config type wins.
        /// </summary>
        [SerializeField] protected List<ScriptableObject> configOverrides;

        protected override void Configure(IContainerBuilder builder)
        {
            try
            {
                var overrides = configOverrides ?? new List<ScriptableObject>();
                var applier = new ConfigOverrideApplier(overrides);
                ConfigOverrideBuildContext.Current = applier;
                try
                {
                    InstallGroups(builder);
                }
                finally
                {
                    ConfigOverrideBuildContext.Current = null;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GroupedScope] Exception during configuration for ScopeObject {gameObject.name}: {ex}", this);
            }
        }

        protected virtual void InstallGroups(IContainerBuilder builder)
        {
            if (collectors == null)
            {
                return;
            }

            foreach (var collector in collectors)
            {
                if (collector == null)
                {
                    continue;
                }

                var types = collector.QualifiedTypes;
                for (var i = 0; i < types.Count; i++)
                {
                    var st = types[i];
                    if (st == null || !st.IsValid)
                    {
                        Debug.LogError($"[GroupedScope] Invalid type at index {i} in {collector.name}.", collector);
                        continue;
                    }

                    var installerType = st.Type;
                    if (!typeof(IFeatureInstaller).IsAssignableFrom(installerType))
                    {
                        Debug.LogError($"[GroupedScope] {installerType.FullName} does not implement {nameof(IFeatureInstaller)}.", collector);
                        continue;
                    }

                    if (installerType.IsAbstract)
                    {
                        Debug.LogError($"[GroupedScope] {installerType.FullName} is abstract and cannot be instantiated.", collector);
                        continue;
                    }

                    try
                    {
                        if (Activator.CreateInstance(installerType) is not IFeatureInstaller featureInstaller)
                        {
                            Debug.LogError($"[GroupedScope] Failed to create instance for {installerType.FullName}.", collector);
                            continue;
                        }

                        featureInstaller.Install(builder);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[GroupedScope] Exception while installing {installerType.FullName}: {ex}", collector);
                    }
                }
            }
        }
    }
}
