using Ruka.Core.Symbols;

namespace Ruka.Core.DI
{
    public abstract class InstallerGroupMarker { }

    public sealed class ProjectGroup : InstallerGroupMarker { }
    public sealed class SceneGroup : InstallerGroupMarker { }
    public sealed class SessionGroup : InstallerGroupMarker { }

    public struct ScopeIdentifier { }

    [SymbolProvider(typeof(ScopeIdentifier), "RUKA_CORE")]
    public static class ScopeIdentifiers
    {
        public static readonly Symbol<ScopeIdentifier> Session = new("SESSION");
    }
}
