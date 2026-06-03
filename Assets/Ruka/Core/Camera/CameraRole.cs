using Ruka.Core.Symbols;

namespace Ruka.Core.Camera
{
    public struct CameraRole { }

    [SymbolProvider(typeof(CameraRole),"RUKA_CORE")]
    public static class CameraRoles
    {
        public static readonly Symbol<CameraRole> Main = new("Main");
    }
}
