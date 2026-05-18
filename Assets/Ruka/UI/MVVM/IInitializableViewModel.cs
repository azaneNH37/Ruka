namespace Ruka.UI.MVVM
{
    /// <summary>
    /// Optional extension for ViewModels that require creation-time parameters. Not a replacement for constructor injection — use when parameters are data-driven and only available at the moment CreateView is called.
    /// </summary>
    public interface IInitializableViewModel<in TParam> : IViewModel
    {
        /// <summary>Applies creation-time parameters to the ViewModel.</summary>
        /// <remarks>Called by ViewPresenterBase before Bind; do not invoke from outside the presenter pipeline.</remarks>
        void Initialize(TParam param);
    }
}
