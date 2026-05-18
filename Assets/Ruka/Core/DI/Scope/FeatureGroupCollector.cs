using System;
using System.Collections.Generic;
using UnityEngine;
using Ruka.Utils.Core;

namespace Ruka.Core.DI
{
    /// <summary>
    /// ScriptableObject that maps an installer group to its discovered <see cref="IFeatureInstaller"/> types.
    /// Populated automatically by <c>FeatureInstallerManifestProcessor</c> on each domain reload.
    /// </summary>
    [CreateAssetMenu(menuName = "Ruka/DI/Feature Group Collector", fileName = "FeatureGroupCollector")]
    public sealed class FeatureGroupCollector : ScriptableObject
    {
        [SerializeField, TypeFilter(typeof(InstallerGroupMarker))] private SerializableType targetGroup;
        [SerializeField, HideInInspector] private List<SerializableType> qualifiedTypes = new();

        /// <summary>The installer group this collector serves. Must be a concrete <see cref="InstallerGroupMarker"/> subclass.</summary>
        public Type TargetGroup => targetGroup?.Type;

        /// <summary>Installer types discovered for this group, sorted by order then full type name. Do not modify directly.</summary>
        public IReadOnlyList<SerializableType> QualifiedTypes => qualifiedTypes;

#if UNITY_EDITOR
        /// <summary>Replaces the stored type list. Returns <c>true</c> if the list changed.</summary>
        /// <remarks>Editor-only. Called exclusively by <c>FeatureInstallerManifestProcessor</c>; do not call from gameplay code.</remarks>
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
