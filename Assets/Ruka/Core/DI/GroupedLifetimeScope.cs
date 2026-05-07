using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace Ruka.Core.DI
{
    public abstract class GroupedLifetimeScope : NestedLifetimeScope
    {
        [SerializeField] protected List<FeatureGroupCollector> collectors;
        [SerializeField] protected List<FeatureConfigBase> configs;

        protected override void Configure(IContainerBuilder builder)
        {
            RegisterConfigs(builder);
            InstallGroups(builder);
        }

        protected virtual void RegisterConfigs(IContainerBuilder builder)
        {
            if (configs == null)
            {
                return;
            }

            foreach (var config in configs)
            {
                if (config == null)
                {
                    continue;
                }

                builder.RegisterInstance(config, config.GetType());
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
                    var qualifiedType = types[i];
                    if (string.IsNullOrWhiteSpace(qualifiedType))
                    {
                        Debug.LogError($"[GroupedScope] Missing type at index {i} in {collector.name}.", collector);
                        continue;
                    }

                    var installerType = Type.GetType(qualifiedType, throwOnError: false);
                    if (installerType == null)
                    {
                        Debug.LogError($"[GroupedScope] Cannot resolve type {qualifiedType}.", collector);
                        continue;
                    }

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
