namespace Ruka.Core.Saves
{
    public struct SlotChannel { }
    public struct CrossChannel { }

    public interface ISaveMigration<TChannel>
    {
        int FromVersion { get; }
        int ToVersion { get; }

        void Migrate(SaveContainer container);
    }
}
