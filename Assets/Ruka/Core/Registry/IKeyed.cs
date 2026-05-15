namespace Ruka.Core.Registry
{
    public interface IKeyed<out TKey>
    {
        TKey Key { get; }
    }
}
