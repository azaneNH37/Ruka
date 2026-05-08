using System.Collections.Generic;
using UnityEngine;
using Ruka.Core.Symbols;

namespace Ruka.Core.DI
{
    public sealed class FeatureGroupCollector : ScriptableObject
    {
        [SerializeField] private Symbol<InstallerGroup> targetGroup;
        [SerializeField] private List<string> qualifiedTypes = new();

        public Symbol<InstallerGroup> TargetGroup => targetGroup;
        public IReadOnlyList<string> QualifiedTypes => qualifiedTypes;

#if UNITY_EDITOR
        public bool UpdateQualifiedTypes(IReadOnlyList<string> types)
        {
            if (types == null)
            {
                throw new System.ArgumentNullException(nameof(types));
            }

            if (AreQualifiedTypesEqual(types))
            {
                return false;
            }

            qualifiedTypes.Clear();
            for (var i = 0; i < types.Count; i++)
            {
                qualifiedTypes.Add(types[i]);
            }

            return true;
        }

        private bool AreQualifiedTypesEqual(IReadOnlyList<string> types)
        {
            if (qualifiedTypes.Count != types.Count)
            {
                return false;
            }

            for (var i = 0; i < types.Count; i++)
            {
                if (!string.Equals(qualifiedTypes[i], types[i], System.StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }
#endif
    }
}
