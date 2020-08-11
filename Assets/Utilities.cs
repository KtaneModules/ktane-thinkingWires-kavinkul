using System;
using System.Collections.Generic;

namespace Utilities
{
    //https://stackoverflow.com/a/35327419
    public static class EnumerableExtension
    {
        public static IEnumerable<TResult> SelectTwo<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, TSource, TResult> selector)
        {
            if (source == null) throw new ArgumentNullException("source cannot be null.");
            if (selector == null) throw new ArgumentNullException("selector cannot be null.");

            return SelectTwoImpl(source, selector);
        }

        private static IEnumerable<TResult> SelectTwoImpl<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, TSource, TResult> selector)
        {
            using (var iterator = source.GetEnumerator())
            {
                var item2 = default(TSource);
                var i = 0;
                while (iterator.MoveNext())
                {
                    var item1 = item2;
                    item2 = iterator.Current;
                    i++;

                    if (i >= 2)
                        yield return selector(item1, item2);
                }
            }
        }

        public static IEnumerable<TResult> SelectThree<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, TSource, TSource, TResult> selector)
        {
            if (source == null) throw new ArgumentNullException("source cannot be null.");
            if (selector == null) throw new ArgumentNullException("selector cannot be null.");

            return SelectThreeImpl(source, selector);
        }

        private static IEnumerable<TResult> SelectThreeImpl<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, TSource, TSource, TResult> selector)
        {
            using (var iterator = source.GetEnumerator())
            {
                var item2 = default(TSource);
                var item3 = default(TSource);
                var i = 0;
                while (iterator.MoveNext())
                {
                    var item1 = item2;
                    item2 = item3;
                    item3 = iterator.Current;
                    i++;

                    if (i >= 3)
                        yield return selector(item1, item2, item3);
                }
            }
        }

        public static IEnumerable<TResult> Zip<TSourceA, TSourceB, TResult>(this IEnumerable<TSourceA> sourceA, IEnumerable<TSourceB> sourceB, Func<TSourceA, TSourceB, TResult> selector)
        {
            if (sourceA == null) throw new ArgumentNullException("sourceA can't be null.");
            if (sourceB == null) throw new ArgumentNullException("sourceB can't be null");

            return ZipImpl(sourceA, sourceB, selector);
        }

        private static IEnumerable<TResult> ZipImpl<TSourceA, TSourceB, TResult>(this IEnumerable<TSourceA> sourceA, IEnumerable<TSourceB> sourceB, Func<TSourceA, TSourceB, TResult> selector)
        {
            using (var iteratorA = sourceA.GetEnumerator())
            using (var iteratorB = sourceB.GetEnumerator())
            {
                while (iteratorA.MoveNext() && iteratorB.MoveNext())
                    yield return selector(iteratorA.Current, iteratorB.Current);
            }
        }
    }
}