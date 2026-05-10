namespace Ruka.Core.Random
{
    public readonly struct MasterSeed
    {
        public int Value { get; }

        public MasterSeed(int value)
        {
            Value = value;
        }
    }
}
