namespace Ruka.Core.Scenes
{
    internal interface ICurtainRegistry
    {
        void Push(ISceneTransitionCurtain curtain);
        void Pop(ISceneTransitionCurtain curtain);
    }
}
