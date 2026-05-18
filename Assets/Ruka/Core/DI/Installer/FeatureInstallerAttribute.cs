using System;
using System.ComponentModel;

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
        public FeatureInstallerAttribute(Type group, int order = 0)
        {
            if (group == null)
            {
                throw new ArgumentNullException(nameof(group));
            }

            if (!typeof(InstallerGroupMarker).IsAssignableFrom(group))
            {
                throw new ArgumentException(
                    $"Group type '{group.FullName}' must inherit from {nameof(InstallerGroupMarker)}.",
                    nameof(group));
            }

            Group = group;
            Order = order;
        }

        /// <summary>The group marker type this installer belongs to. Must inherit from <see cref="InstallerGroupMarker"/>.</summary>
        public Type Group { get; }

        /// <summary>Execution order within the group. Keep at <c>0</c> unless resolving a last-wins registration conflict.</summary>
        public int Order { get; }
    }
}
