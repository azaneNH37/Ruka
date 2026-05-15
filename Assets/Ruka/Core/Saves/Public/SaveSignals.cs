namespace Ruka.Core.Saves
{
    public struct SlotSaveStartedSignal { public int Slot; }
    public struct SlotSaveFinishedSignal { public int Slot; public bool Success; public long DurationMs; public int BytesWritten; }
    public struct SlotLoadStartedSignal { public int Slot; }
    public struct SlotLoadFinishedSignal { public int Slot; public bool Success; public long DurationMs; public int BytesRead; public bool Migrated; }

    public struct CrossSaveStartedSignal { }
    public struct CrossSaveFinishedSignal { public bool Success; public long DurationMs; public int BytesWritten; }
    public struct CrossLoadStartedSignal { }
    public struct CrossLoadFinishedSignal { public bool Success; public long DurationMs; public int BytesRead; public bool Migrated; }
}
