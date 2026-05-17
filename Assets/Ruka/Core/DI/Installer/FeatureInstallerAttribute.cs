using System;

namespace Ruka.Core.DI
{
    /// <summary>
    /// Marks an <see cref="IFeatureInstaller"/> class for automatic discovery by group.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Order parameter:</b> Controls execution sequence among installers within the same group.
    /// The default value is <c>0</c>, and the vast majority of installers should keep this default.
    /// </para>
    /// <para>
    /// The only legitimate use for a non-zero <c>order</c> is when two installers in the same group
    /// register the same interface type (VContainer's last-wins behavior), and you need a predictable
    /// winner. This is a design smell — prefer resolving such conflicts at the architecture level
    /// rather than relying on integer ordering.
    /// </para>
    /// <para>
    /// <b>Do NOT use <c>order</c> to control <c>IInitializable.Initialize()</c> execution order.</b>
    /// If ServiceA must initialize before ServiceB, inject A into B's constructor instead.
    /// VContainer guarantees A will be resolved (and thus initialized) before B.
    /// Using <c>order</c> for initialization ordering turns implicit dependencies into
    /// unverifiable integer magic.
    /// </para>
    /// </remarks>
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
