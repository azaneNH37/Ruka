namespace Ruka.Core.Saves
{
    public interface ISaveable
    {
        string SaveKey { get; }
        bool IsMeta { get; }
        byte[] CaptureState();
        void RestoreState(byte[] data);
        void SetupDefaultState();
    }
}
