using VContainer;

namespace Ruka.Core.DI
{
    /// <summary>
    /// Ruka's primary registration contract for modular feature installation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Not a replacement for</b> <see cref="IInstaller"/>.
    /// VContainer's <c>IInstaller</c> is used for <b>transient extra registrations</b>
    /// during cross-scene loading (e.g., <c>LifetimeScope.Enqueue(installer)</c>).
    /// <c>IFeatureInstaller</c> serves a different purpose: it is the <b>persistent,
    /// group-based main registration path</b> — each <c>Install()</c> call runs
    /// every time its parent scope is built.
    /// </para>
    /// <para>
    /// This interface is intentionally <b>not</b> a subtype of <c>VContainer.IInstaller</c>
    /// to avoid implicit API-level confusion between the two lifecycle semantics.
    /// </para>
    /// </remarks>
    public interface IFeatureInstaller
    {
        /// <summary>
        /// Register services and configurations into the container builder.
        /// This method is called once per scope build and should only perform
        /// registration — it must not access runtime state.
        /// </summary>
        void Install(IContainerBuilder builder);
    }
}
