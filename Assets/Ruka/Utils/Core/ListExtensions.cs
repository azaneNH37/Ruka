using System;
using System.Collections.Generic;

namespace Ruka.Utils.Core
{
    public static class ListExtensions
    {
        public static void AddRange<T>(this IList<T> list, IEnumerable<T> items)
        {
            if (list == null)
            {
                throw new ArgumentNullException(nameof(list));
            }

            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            foreach (var item in items)
            {
                list.Add(item);
            }
        }

        public static int RemoveAll<T>(this IList<T> list, Predicate<T> predicate)
        {
            if (list == null)
            {
                throw new ArgumentNullException(nameof(list));
            }

            if (predicate == null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }

            var removed = 0;
            for (var i = list.Count - 1; i >= 0; i--)
            {
                if (!predicate(list[i]))
                {
                    continue;
                }

                list.RemoveAt(i);
                removed++;
            }

            return removed;
        }
    }
}
