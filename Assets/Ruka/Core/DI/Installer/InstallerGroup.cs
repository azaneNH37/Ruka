using Ruka.Core.Symbols;

namespace Ruka.Core.DI
{
    /// <summary>Base class for installer group markers. Inherit to declare a custom group.</summary>
    public abstract class InstallerGroupMarker { }

    /// <summary>Built-in installer group for project-lifetime registrations.</summary>
    public sealed class ProjectGroup : InstallerGroupMarker { }

    /// <summary>Built-in installer group for scene-lifetime registrations.</summary>
    public sealed class SceneGroup : InstallerGroupMarker { }

    /// <summary>Built-in installer group for session-lifetime registrations.</summary>
    public sealed class SessionGroup : InstallerGroupMarker { }

    /// <summary>Phantom type for <see cref="Symbol{T}"/> scope name identifiers.</summary>
    public struct ScopeIdentifier { }

    /// <summary>Framework-defined named scope identifiers.</summary>
    [SymbolProvider(typeof(ScopeIdentifier), "RUKA_CORE")]
    public static class ScopeIdentifiers
    {
        public static readonly Symbol<ScopeIdentifier> Session = new("SESSION");
    }
}
