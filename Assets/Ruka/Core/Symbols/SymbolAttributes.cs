using System;
using UnityEngine;

namespace Ruka.Core.Symbols
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class SymbolProviderAttribute : Attribute
    {
        public SymbolProviderAttribute(Type symbolType, string groupName)
        {
            SymbolType = symbolType;
            GroupName = groupName;
        }

        public Type SymbolType { get; }
        public string GroupName { get; }
    }

    [AttributeUsage(AttributeTargets.Field, Inherited = true)]
    public sealed class SymbolSelectorAttribute : PropertyAttribute
    {
        public SymbolSelectorAttribute(params string[] targetGroups)
        {
            TargetGroups = targetGroups ?? Array.Empty<string>();
        }

        public string[] TargetGroups { get; }
        public bool AllowManualInput { get; set; }
    }
}
