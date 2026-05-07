namespace Ruka.Utils.Core
{
    public static class StringExtensions
    {
        public static int GetStableHash(this string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return 0;
            }

            unchecked
            {
                const int offsetBasis = unchecked((int)2166136261);
                const int prime = 16777619;
                var hash = offsetBasis;

                for (var i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= prime;
                }

                return hash;
            }
        }
    }
}
