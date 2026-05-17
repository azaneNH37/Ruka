using UnityEngine;

namespace Ruka.Core.DI
{
    internal interface IConfigOverrideApply
    {
        object Apply(object baseline);
    }

    public abstract class FeatureConfigOverride<T> : ScriptableObject, IConfigOverrideApply
        where T : IFeatureConfig
    {
        public abstract T Apply(T baseline);
        object IConfigOverrideApply.Apply(object baseline) => Apply((T)baseline);
    }
}
