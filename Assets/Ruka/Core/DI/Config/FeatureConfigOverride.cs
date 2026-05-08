using UnityEngine;

namespace Ruka.Core.DI
{
    public abstract class FeatureConfigOverride<T> : ScriptableObject
        where T : notnull
    {
        public abstract T Apply(T baseline);
    }
}
