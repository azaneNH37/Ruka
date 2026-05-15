using MessagePack;

namespace Ruka.Core.Saves
{
    [MessagePackObject(AllowPrivate = true)]
    internal sealed class GlobalMetadata
    {
        [Key(0)] public int LastActiveSlot { get; set; } = 0;
        [Key(1)] public int PendingActiveSlot { get; set; } = 0;
    }
}
