namespace Ruka.UI.MVVM
{
    /// <summary>MonoBehaviour contract for a view that binds to a typed ViewModel instance.</summary>
    public interface IView<TViewModel> where TViewModel : IViewModel
    {
        /// <summary>Wires up bindings between this view and viewModel.</summary>
        /// <remarks>Called by ViewPresenterBase after ViewModel creation and optional initialization; do not call directly.</remarks>
        void Bind(TViewModel viewModel);
    }
}
