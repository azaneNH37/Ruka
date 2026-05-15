namespace Ruka.Core.Saves
{
    public interface ICrossSaveable
    {
        string SaveKey { get; }
        byte[] CaptureState();
        void RestoreState(byte[] data);
        void SetupDefaultState();
    }
}
