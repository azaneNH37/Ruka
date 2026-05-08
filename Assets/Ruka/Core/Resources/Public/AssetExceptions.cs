using System;

namespace Ruka.Core.Resources
{
    public sealed class AssetLoadException : Exception
    {
        public string Address { get; }

        public AssetLoadException(string address, string message)
            : base($"[Resources] Load failure for '{address}': {message}")
        {
            Address = address;
        }
    }

    public sealed class AssetInitializationException : Exception
    {
        public string PackageName { get; }

        public AssetInitializationException(string packageName, string message)
            : base($"[Resources] Package '{packageName}' initialization failure: {message}")
        {
            PackageName = packageName;
        }
    }
}
