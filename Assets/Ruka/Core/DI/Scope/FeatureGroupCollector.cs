using System;
using System.Collections.Generic;
using UnityEngine;
using Ruka.Core.Symbols;
using Ruka.Utils.Core;

namespace Ruka.Core.DI
{
    [CreateAssetMenu(menuName = "Ruka/DI/Feature Group Collector", fileName = "FeatureGroupCollector")]
    public sealed class FeatureGroupCollector : ScriptableObject
    {
        [SerializeField, SymbolSelector] private Symbol<InstallerGroup> targetGroup;
        [SerializeField, HideInInspector] private List<SerializableType> qualifiedTypes = new();

        public Symbol<InstallerGroup> TargetGroup => targetGroup;
        public IReadOnlyList<SerializableType> QualifiedTypes => qualifiedTypes;

#if UNITY_EDITOR
        public bool UpdateQualifiedTypes(IReadOnlyList<Type> types)
        {
            if (types == null)
            {
                throw new ArgumentNullException(nameof(types));
            }

            if (AreQualifiedTypesEqual(types))
            {
                return false;
            }

            qualifiedTypes.Clear();
            for (var i = 0; i < types.Count; i++)
            {
                var st = new SerializableType();
                st.Set(types[i]);
                qualifiedTypes.Add(st);
            }

            return true;
        }

        private bool AreQualifiedTypesEqual(IReadOnlyList<Type> types)
        {
            if (qualifiedTypes.Count != types.Count)
            {
                return false;
            }

            for (var i = 0; i < types.Count; i++)
            {
                if (qualifiedTypes[i].Type != types[i])
                {
                    return false;
                }
            }

            return true;
        }
#endif
    }
}
