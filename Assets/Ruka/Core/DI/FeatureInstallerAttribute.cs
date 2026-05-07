using System;

namespace Ruka.Core.DI
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class FeatureInstallerAttribute : Attribute
    {
        public FeatureInstallerAttribute(string group, int order = 0)
        {
            if (string.IsNullOrWhiteSpace(group))
            {
                throw new ArgumentException("Group cannot be null or whitespace.", nameof(group));
            }

            Group = group.Trim();
            Order = order;
        }

        public string Group { get; }
        public int Order { get; }
    }
}
