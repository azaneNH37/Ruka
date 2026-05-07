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
    }
}
