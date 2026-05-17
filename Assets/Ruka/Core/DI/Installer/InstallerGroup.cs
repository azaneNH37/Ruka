using Ruka.Core.Symbols;

namespace Ruka.Core.DI
{
    public struct InstallerGroup { }

    [SymbolProvider(typeof(InstallerGroup), "RUKA_CORE")]
    public static class InstallerGroups
    {
        public const string ProjectGroup = "PROJECT";
        public const string SceneGroup = "SCENE";
        public const string SessionGroup = "SESSION";

        public static readonly Symbol<InstallerGroup> Project = new(ProjectGroup);
        public static readonly Symbol<InstallerGroup> Scene = new(SceneGroup);
        public static readonly Symbol<InstallerGroup> Session = new(SessionGroup);
    }

    public struct ScopeIdentifier { }

    [SymbolProvider(typeof(ScopeIdentifier), "RUKA_CORE")]
    public static class ScopeIdentifiers
    {
        public static readonly Symbol<ScopeIdentifier> Project = new("PROJECT");
        public static readonly Symbol<ScopeIdentifier> Scene = new("SCENE");
        public static readonly Symbol<ScopeIdentifier> Session = new("SESSION");
    }
}
