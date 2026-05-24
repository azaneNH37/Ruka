using System;

namespace Ruka.Core.MVVM
{
    /// <summary>
    /// Marker contract for all ViewModels. Lives in Ruka.Core so business assemblies (Framework, Gameplay)
    /// can implement it without taking a dependency on Ruka.UI.
    /// ViewModels are plain C# classes that own observable presentation state; Views are the MonoBehaviours that render it.
    /// </summary>
    public interface IViewModel : IDisposable { }
}
