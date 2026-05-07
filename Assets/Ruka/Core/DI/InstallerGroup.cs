using Ruka.Core.Symbols;

namespace Ruka.Core.DI
{
    public struct InstallerGroup { }

    [SymbolProvider(typeof(InstallerGroup), "Core")]
    public static class InstallerGroups
    {
        public static readonly Symbol<InstallerGroup> Project = new("PROJECT");
        public static readonly Symbol<InstallerGroup> Scene = new("SCENE");
    }
}
