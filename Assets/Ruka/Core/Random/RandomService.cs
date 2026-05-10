using System;
using System.Collections.Generic;
using Ruka.Core.Symbols;
using UnityEngine;

namespace Ruka.Core.Random
{
    public sealed class RandomService : IGlobalRandomService
    {
        private readonly MasterSeed _masterSeed;
        private readonly Dictionary<Symbol<RandomSeq>, SequenceState> _seqById = new();

        private sealed class SequenceState
        {
            public ulong Seed;
            public Xoshiro256StarStar Engine;
        }

        public RandomService(MasterSeed masterSeed)
        {
            _masterSeed = masterSeed;
        }

        public ulong NextUInt64(Symbol<RandomSeq> seq)
        {
            return RequireState(seq).Engine.NextUInt64();
        }

        public int RangeInt(Symbol<RandomSeq> seq, int min, int maxExclusive)
        {
            return RequireState(seq).Engine.NextInt(min, maxExclusive);
        }

        public float RangeFloat(Symbol<RandomSeq> seq, float min = 0f, float max = 1f)
        {
            return RequireState(seq).Engine.NextFloat(min, max);
        }

        public T Sample<T>(Symbol<RandomSeq> seq, IReadOnlyList<T> list)
        {
            if (list == null || list.Count == 0)
                throw new InvalidOperationException("Cannot sample from empty list.");

            var index = RequireState(seq).Engine.NextInt(0, list.Count);
            return list[index];
        }

        public void Shuffle<T>(Symbol<RandomSeq> seq, IList<T> list)
        {
            var state = RequireState(seq);
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = state.Engine.NextInt(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        public Vector2 NextPointInCircle(Symbol<RandomSeq> seq, float radius)
        {
            return RequireState(seq).Engine.NextPointInCircle(radius);
        }

        public Vector2 NextPointInRect(Symbol<RandomSeq> seq, Rect rect)
        {
            return RequireState(seq).Engine.NextPointInRect(rect);
        }

        public Vector2 NextPointInAnnulus(Symbol<RandomSeq> seq, float innerRadius, float outerRadius)
        {
            return RequireState(seq).Engine.NextPointInAnnulus(innerRadius, outerRadius);
        }

        private SequenceState RequireState(Symbol<RandomSeq> seq)
        {
            if (_seqById.TryGetValue(seq, out var state))
                return state;

            state = new SequenceState
            {
                Seed = StableSeedHash(_masterSeed.Value, seq.Value),
            };
            state.Engine = new Xoshiro256StarStar(state.Seed);
            _seqById.Add(seq, state);
            return state;
        }

        private static ulong StableSeedHash(int masterSeed, string symbol)
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;

            unchecked
            {
                ulong hash = offset;
                HashInt(masterSeed, ref hash, prime);
                HashString(symbol, ref hash, prime);
                return hash;
            }
        }

        private static void HashInt(int value, ref ulong hash, ulong prime)
        {
            unchecked
            {
                hash ^= (uint)value;
                hash *= prime;
            }
        }

        private static void HashString(string value, ref ulong hash, ulong prime)
        {
            unchecked
            {
                if (string.IsNullOrEmpty(value))
                    return;

                for (var i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= prime;
                }
            }
        }
    }
}
