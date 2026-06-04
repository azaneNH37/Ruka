using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Ruka.Core.Prefabs
{
    /// <summary>
    /// Fluent builder for prefab instantiation options. Passed via <c>Action&lt;PrefabOptions&gt;</c>
    /// callback; do not cache or reuse instances across calls.
    /// </summary>
    public class PrefabOptions
    {
        internal Transform VisualParent;
        internal LifetimeScope DiParentOverride;
        internal List<IInstaller> Installers;
        internal bool IsManualActivation;
        internal bool IsWorldPositionStays;
        internal Vector3? Position;
        internal Quaternion? Rotation;

        /// <summary>Sets the transform parent for the instantiated GO (visual hierarchy only, does not affect DI parent).</summary>
        public PrefabOptions Under(Transform parent)
        {
            VisualParent = parent;
            return this;
        }

        /// <summary>
        /// Overrides the DI parent scope. Defaults to the scope that owns the <see cref="IPrefabFactory"/>.
        /// Also shifts the default <c>CancellationToken</c> to this scope's <c>destroyCancellationToken</c>,
        /// so the instance lifetime follows the DI parent unless an explicit <c>ct</c> is passed.
        /// </summary>
        public PrefabOptions WithDiParent(LifetimeScope scope)
        {
            DiParentOverride = scope;
            return this;
        }

        /// <summary>Enqueues extra registrations into the prefab's <see cref="LifetimeScope"/> during activation.</summary>
        /// <remarks>Only effective for prefabs whose root has a <see cref="LifetimeScope"/>. Ignored for plain prefabs.</remarks>
        public PrefabOptions WithInstallation(Action<IContainerBuilder> installation)
        {
            Installers ??= new List<IInstaller>();
            Installers.Add(new ActionInstaller(installation));
            return this;
        }

        public PrefabOptions WithInstallation(IInstaller installer)
        {
            Installers ??= new List<IInstaller>();
            Installers.Add(installer);
            return this;
        }

        /// <summary>
        /// Prevents the factory from activating the GO. Caller must call <c>SetActive(true)</c> manually.
        /// </summary>
        /// <remarks>
        /// When combined with <see cref="WithInstallation"/> on a scope prefab, the caller is
        /// responsible for calling <c>LifetimeScope.Enqueue</c> before activating.
        /// </remarks>
        public PrefabOptions ManualActivation()
        {
            IsManualActivation = true;
            return this;
        }

        /// <summary>Preserves world position when parenting via <see cref="Under"/>. Mirrors <c>Transform.SetParent(parent, true)</c>.</summary>
        public PrefabOptions WorldPositionStays()
        {
            IsWorldPositionStays = true;
            return this;
        }

        /// <summary>Sets world position for the instance. Overrides the prefab's default position.</summary>
        public PrefabOptions At(Vector3 position)
        {
            Position = position;
            return this;
        }

        /// <summary>Sets world position and rotation for the instance.</summary>
        public PrefabOptions At(Vector3 position, Quaternion rotation)
        {
            Position = position;
            Rotation = rotation;
            return this;
        }
    }
}
