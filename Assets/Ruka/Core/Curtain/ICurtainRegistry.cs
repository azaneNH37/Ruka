namespace Ruka.Core.Curtain
{
    public interface ICurtainRegistry
    {
        void Push(ICurtain curtain);
        void Pop(ICurtain curtain);
    }
}
