namespace Ruka.Core.Pool
{
    /// <summary>
    /// Marker interface for objects that can reset their own intrinsic state.
    /// </summary>
    public interface IResettable
    {
        /// <summary>
        /// Resets intrinsic state to a clean baseline.
        /// </summary>
        void ResetState();
    }
}
