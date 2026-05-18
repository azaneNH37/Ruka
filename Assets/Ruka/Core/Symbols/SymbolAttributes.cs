using System;
using UnityEngine;

namespace Ruka.Core.Symbols
{
    /// <summary>
    /// Marks a static class as a source of <see cref="Symbol{T}"/> constants for editor dropdown discovery.
    /// Apply once per domain-group pair; a single class may carry multiple instances for different types or groups.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class SymbolProviderAttribute : Attribute
    {
        /// <summary>Declares this class as a provider of constants for <paramref name="symbolType"/> within the named group.</summary>
        /// <param name="symbolType">The marker struct type (the T in Symbol{T}) that this class provides constants for.</param>
        /// <param name="groupName">Group label shown in the dropdown hierarchy. Use a consistent name across related providers.</param>
        public SymbolProviderAttribute(Type symbolType, string groupName)
        {
            SymbolType = symbolType;
            GroupName = groupName;
        }

        /// <summary>The marker struct type (the T in Symbol{T}) that this class provides constants for.</summary>
        public Type SymbolType { get; }

        /// <summary>Group label shown in the dropdown hierarchy.</summary>
        public string GroupName { get; }
    }

    /// <summary>
    /// Marks a <c>[SerializeField]</c> <see cref="Symbol{T}"/> field to render as a searchable dropdown populated by <see cref="SymbolProviderAttribute"/> classes.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true)]
    public sealed class SymbolSelectorAttribute : PropertyAttribute
    {
        /// <param name="targetGroups">Groups to include. Empty means all registered groups for the symbol type.</param>
        public SymbolSelectorAttribute(params string[] targetGroups)
        {
            TargetGroups = targetGroups ?? Array.Empty<string>();
        }

        /// <summary>Groups to include in the dropdown. Empty means all registered groups for the symbol type.</summary>
        public string[] TargetGroups { get; }

        /// <summary>Enables freeform text input alongside the dropdown. Disable when the valid value set is closed (all values come from providers).</summary>
        public bool AllowManualInput { get; set; }
    }
}
