namespace Ruka.UI.MVVM
{
    public interface IView<TViewModel> where TViewModel : IViewModel
    {
        void Bind(TViewModel viewModel);
    }
}
