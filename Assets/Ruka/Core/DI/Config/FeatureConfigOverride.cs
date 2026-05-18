using UnityEngine;

namespace Ruka.Core.DI
{
    internal interface IConfigOverrideApply
    {
        object Apply(object baseline);
    }

    /// <summary>
    /// ScriptableObject that substitutes or patches a single <typeparamref name="T"/> config during scope build.
    /// Not a service — attach to <see cref="GroupedLifetimeScope"/> <c>configOverrides</c> to activate.
    /// </summary>
    public abstract class FeatureConfigOverride<T> : ScriptableObject, IConfigOverrideApply
        where T : IFeatureConfig
    {
        /// <summary>Produces the final config value from <paramref name="baseline"/>. Called once per scope build; do not read runtime state here.</summary>
        public abstract T Apply(T baseline);
        object IConfigOverrideApply.Apply(object baseline) => Apply((T)baseline);
    }
}
