using UnityEngine.SceneManagement;

namespace Ruka.Core.Resources
{
    public interface ISceneLoadService
    {
        SceneLoadHandle Load(string address, LoadSceneMode mode, bool suspendLoad = true);
    }
}
