using System;
using System.Collections.Generic;
using Ruka.Core.Symbols;

namespace Ruka.Core.DI
{
    internal sealed class ScopeRegistry
    {
        internal static readonly ScopeRegistry Instance = new();

        private readonly Dictionary<Symbol<ScopeIdentifier>, NestedLifetimeScope> _scopes = new();

        internal bool TryFind(Symbol<ScopeIdentifier> id, out NestedLifetimeScope scope)
        {
            if (id.IsEmpty)
            {
                scope = null;
                return false;
            }

            return _scopes.TryGetValue(id, out scope);
        }

        internal void Register(Symbol<ScopeIdentifier> id, NestedLifetimeScope scope)
        {
            if (id.IsEmpty)
            {
                return;
            }

            if (_scopes.TryGetValue(id, out var existing) && existing != scope)
            {
                throw new InvalidOperationException(
                    $"[ScopeRegistry] Scope id '{id.Value}' is already registered by '{existing.name}'. " +
                    $"Scope IDs must be unique. Conflicting scope: '{scope.name}'.");
            }

            _scopes[id] = scope;
        }

        internal void Unregister(Symbol<ScopeIdentifier> id, NestedLifetimeScope scope)
        {
            if (_scopes.TryGetValue(id, out var registered) && registered == scope)
            {
                _scopes.Remove(id);
            }
        }
    }
}
