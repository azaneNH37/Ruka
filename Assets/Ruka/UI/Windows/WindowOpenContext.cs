using System;
using System.Threading;
using Ruka.Core.Resources;

namespace Ruka.UI.Windows
{
    public readonly struct WindowOpenContext
    {
        public IAssetScope AssetScope { get; }
        public CancellationToken Lifetime { get; }

        public WindowOpenContext(IAssetScope assetScope, CancellationToken lifetime)
        {
            AssetScope = assetScope ?? throw new ArgumentNullException(nameof(assetScope));
            Lifetime = lifetime;
        }

        public static WindowOpenContext From(IAssetScope assetScope, CancellationToken lifetime)
        {
            return new WindowOpenContext(assetScope, lifetime);
        }
    }
}
