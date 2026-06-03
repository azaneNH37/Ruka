using System;
using Ruka.Core.Symbols;

namespace Ruka.UI.Windows
{
    public interface IWindowRegistry
    {
        IDisposable Register(Symbol<WindowId> windowId, WindowBase window);
    }
}
