using System.Collections.Generic;
using Ruka.Core.Symbols;
using UnityEngine;

namespace Ruka.Core.Random
{
    public interface IRandomService
    {
        ulong NextUInt64(Symbol<RandomSeq> seq);

        int RangeInt(Symbol<RandomSeq> seq, int min, int maxExclusive);

        float RangeFloat(Symbol<RandomSeq> seq, float min = 0f, float max = 1f);

        T Sample<T>(Symbol<RandomSeq> seq, IReadOnlyList<T> list);

        void Shuffle<T>(Symbol<RandomSeq> seq, IList<T> list);

        Vector2 NextPointInCircle(Symbol<RandomSeq> seq, float radius);

        Vector2 NextPointInRect(Symbol<RandomSeq> seq, Rect rect);

        Vector2 NextPointInAnnulus(Symbol<RandomSeq> seq, float innerRadius, float outerRadius);
    }

    public interface IGlobalRandomService : IRandomService
    {
    }
}
