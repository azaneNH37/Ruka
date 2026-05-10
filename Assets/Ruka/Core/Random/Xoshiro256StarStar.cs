using UnityEngine;

namespace Ruka.Core.Random
{
    public struct Xoshiro256StarStar
    {
        private ulong _s0;
        private ulong _s1;
        private ulong _s2;
        private ulong _s3;

        public Xoshiro256StarStar(ulong seed)
        {
            var split = new SplitMix64(seed);
            _s0 = split.Next();
            _s1 = split.Next();
            _s2 = split.Next();
            _s3 = split.Next();
        }

        public ulong NextUInt64()
        {
            var result = RotateLeft(_s1 * 5UL, 7) * 9UL;
            var t = _s1 << 17;

            _s2 ^= _s0;
            _s3 ^= _s1;
            _s1 ^= _s2;
            _s0 ^= _s3;
            _s2 ^= t;
            _s3 = RotateLeft(_s3, 45);

            return result;
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (minInclusive >= maxExclusive)
                return minInclusive;

            var range = (uint)(maxExclusive - minInclusive);
            var value = (uint)(NextUInt64() % range);
            return minInclusive + (int)value;
        }

        public float NextFloat(float minInclusive, float maxInclusive)
        {
            if (minInclusive >= maxInclusive)
                return minInclusive;

            var normalized = (float)((NextUInt64() >> 40) * (1.0 / (1UL << 24)));
            return Mathf.Lerp(minInclusive, maxInclusive, normalized);
        }

        public Vector2 NextPointInCircle(float radius)
        {
            var theta = NextFloat(0f, Mathf.PI * 2f);
            var r = Mathf.Sqrt(NextFloat(0f, 1f)) * radius;
            return new Vector2(Mathf.Cos(theta), Mathf.Sin(theta)) * r;
        }

        public Vector2 NextPointInRect(Rect rect)
        {
            var x = NextFloat(rect.xMin, rect.xMax);
            var y = NextFloat(rect.yMin, rect.yMax);
            return new Vector2(x, y);
        }

        public Vector2 NextPointInAnnulus(float innerRadius, float outerRadius)
        {
            var theta = NextFloat(0f, Mathf.PI * 2f);
            var innerSq = innerRadius * innerRadius;
            var outerSq = outerRadius * outerRadius;
            var r = Mathf.Sqrt(NextFloat(innerSq, outerSq));
            return new Vector2(Mathf.Cos(theta), Mathf.Sin(theta)) * r;
        }

        private static ulong RotateLeft(ulong value, int shift)
        {
            return (value << shift) | (value >> (64 - shift));
        }

        private struct SplitMix64
        {
            private ulong _state;

            public SplitMix64(ulong seed)
            {
                _state = seed;
            }

            public ulong Next()
            {
                _state += 0x9E3779B97F4A7C15UL;
                var z = _state;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }
    }
}
