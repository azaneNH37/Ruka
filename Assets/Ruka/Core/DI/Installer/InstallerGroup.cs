using Ruka.Core.Symbols;

namespace Ruka.Core.DI
{
    public struct InstallerGroup { }

    [SymbolProvider(typeof(InstallerGroup), "RUKA_CORE")]
    public static class InstallerGroups
    {
        public const string ProjectGroup = "PROJECT";
        public const string SceneGroup = "SCENE";

        public static readonly Symbol<InstallerGroup> Project = new(ProjectGroup);
        public static readonly Symbol<InstallerGroup> Scene = new(SceneGroup);
    }
}
