namespace Ruka.UI.MVVM
{
    public interface IInitializableViewModel<in TParam> : IViewModel
    {
        void Initialize(TParam param);
    }
}
