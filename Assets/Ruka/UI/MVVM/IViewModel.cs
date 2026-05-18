using System;

namespace Ruka.UI.MVVM
{
    /// <summary>
    /// Marker contract for all ViewModels. Not a replacement for MonoBehaviour — ViewModels are plain C# classes that own observable state; Views are the MonoBehaviours that render it.
    /// </summary>
    public interface IViewModel : IDisposable { }
}
