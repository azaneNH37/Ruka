using System.Collections.Generic;
using MessagePack;

namespace Ruka.Core.Saves
{
    [MessagePackObject]
    public sealed class SaveContainer
    {
        [Key(0)] public int Version { get; set; }
        [Key(1)] public long Timestamp { get; set; }
        [Key(2)] public Dictionary<string, byte[]> Chunks { get; set; } = new();
    }
}
