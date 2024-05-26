#region The MIT License (MIT)
//
// Copyright (c) 2017 Atif Aziz. All rights reserved.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy of
// this software and associated documentation files (the "Software"), to deal in
// the Software without restriction, including without limitation the rights to
// use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
// the Software, and to permit persons to whom the Software is furnished to do so,
// subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
// FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
// COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER
// IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
// CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
#endregion

using System;
using System.Collections.Generic;

namespace LegendaryExplorerCore.Misc
{
    public class ValueTupleEqualityComparer
    {
        /// <summary>
        /// Creates and returns an <see cref="IEqualityComparer{T}"/> instance for
        /// a tuple of 2 elements where each element of the tuple uses a
        /// user-supplied <see cref="IEqualityComparer{T}"/> instance.
        /// </summary>
        public static IEqualityComparer<(T1, T2)>
            Create<T1, T2>(
                IEqualityComparer<T1> comparer1,
                IEqualityComparer<T2> comparer2) =>
            new EqualityComparer<T1, T2>(
                comparer1,
                comparer2);

        sealed class EqualityComparer<T1, T2> :
            IEqualityComparer<(T1, T2)>
        {
            readonly IEqualityComparer<T1> _comparer1;
            readonly IEqualityComparer<T2> _comparer2;

            public EqualityComparer(
                    IEqualityComparer<T1> comparer1,
                    IEqualityComparer<T2> comparer2)
            {
                _comparer1 = comparer1 ?? EqualityComparer<T1>.Default;
                _comparer2 = comparer2 ?? EqualityComparer<T2>.Default;
            }

            public bool Equals((T1, T2) x,
                               (T1, T2) y)
                => _comparer1.Equals(x.Item1, y.Item1)
                && _comparer2.Equals(x.Item2, y.Item2);

            public int GetHashCode((T1, T2) obj) =>
                HashCode.Combine(
                _comparer1.GetHashCode(obj.Item1),
                _comparer2.GetHashCode(obj.Item2)
                );
        }

        /// <summary>
        /// Creates and returns an <see cref="IEqualityComparer{T}"/> instance for
        /// a tuple of 3 elements where each element of the tuple uses a
        /// user-supplied <see cref="IEqualityComparer{T}"/> instance.
        /// </summary>
        public static IEqualityComparer<(T1, T2, T3)>
            Create<T1, T2, T3>(
                IEqualityComparer<T1> comparer1,
                IEqualityComparer<T2> comparer2,
                IEqualityComparer<T3> comparer3) =>
            new EqualityComparer<T1, T2, T3>(
                comparer1,
                comparer2,
                comparer3);

        sealed class EqualityComparer<T1, T2, T3> :
            IEqualityComparer<(T1, T2, T3)>
        {
            readonly IEqualityComparer<T1> _comparer1;
            readonly IEqualityComparer<T2> _comparer2;
            readonly IEqualityComparer<T3> _comparer3;

            public EqualityComparer(
                    IEqualityComparer<T1> comparer1,
                    IEqualityComparer<T2> comparer2,
                    IEqualityComparer<T3> comparer3)
            {
                _comparer1 = comparer1 ?? EqualityComparer<T1>.Default;
                _comparer2 = comparer2 ?? EqualityComparer<T2>.Default;
                _comparer3 = comparer3 ?? EqualityComparer<T3>.Default;
            }

            public bool Equals((T1, T2, T3) x,
                               (T1, T2, T3) y)
                => _comparer1.Equals(x.Item1, y.Item1)
                && _comparer2.Equals(x.Item2, y.Item2)
                && _comparer3.Equals(x.Item3, y.Item3);

            public int GetHashCode((T1, T2, T3) obj) =>
                HashCode.Combine(
                _comparer1.GetHashCode(obj.Item1),
                _comparer2.GetHashCode(obj.Item2),
                _comparer3.GetHashCode(obj.Item3)
                );
        }

        /// <summary>
        /// Creates and returns an <see cref="IEqualityComparer{T}"/> instance for
        /// a tuple of 4 elements where each element of the tuple uses a
        /// user-supplied <see cref="IEqualityComparer{T}"/> instance.
        /// </summary>
        public static IEqualityComparer<(T1, T2, T3, T4)>
            Create<T1, T2, T3, T4>(
                IEqualityComparer<T1> comparer1,
                IEqualityComparer<T2> comparer2,
                IEqualityComparer<T3> comparer3,
                IEqualityComparer<T4> comparer4) =>
            new EqualityComparer<T1, T2, T3, T4>(
                comparer1,
                comparer2,
                comparer3,
                comparer4);

        sealed class EqualityComparer<T1, T2, T3, T4> :
            IEqualityComparer<(T1, T2, T3, T4)>
        {
            readonly IEqualityComparer<T1> _comparer1;
            readonly IEqualityComparer<T2> _comparer2;
            readonly IEqualityComparer<T3> _comparer3;
            readonly IEqualityComparer<T4> _comparer4;

            public EqualityComparer(
                    IEqualityComparer<T1> comparer1,
                    IEqualityComparer<T2> comparer2,
                    IEqualityComparer<T3> comparer3,
                    IEqualityComparer<T4> comparer4)
            {
                _comparer1 = comparer1 ?? EqualityComparer<T1>.Default;
                _comparer2 = comparer2 ?? EqualityComparer<T2>.Default;
                _comparer3 = comparer3 ?? EqualityComparer<T3>.Default;
                _comparer4 = comparer4 ?? EqualityComparer<T4>.Default;
            }

            public bool Equals((T1, T2, T3, T4) x,
                               (T1, T2, T3, T4) y)
                => _comparer1.Equals(x.Item1, y.Item1)
                && _comparer2.Equals(x.Item2, y.Item2)
                && _comparer3.Equals(x.Item3, y.Item3)
                && _comparer4.Equals(x.Item4, y.Item4);

            public int GetHashCode((T1, T2, T3, T4) obj) =>
                HashCode.Combine(
                _comparer1.GetHashCode(obj.Item1),
                _comparer2.GetHashCode(obj.Item2),
                _comparer3.GetHashCode(obj.Item3),
                _comparer4.GetHashCode(obj.Item4)
                );
        }

        /// <summary>
        /// Creates and returns an <see cref="IEqualityComparer{T}"/> instance for
        /// a tuple of 5 elements where each element of the tuple uses a
        /// user-supplied <see cref="IEqualityComparer{T}"/> instance.
        /// </summary>
        public static IEqualityComparer<(T1, T2, T3, T4, T5)>
            Create<T1, T2, T3, T4, T5>(
                IEqualityComparer<T1> comparer1,
                IEqualityComparer<T2> comparer2,
                IEqualityComparer<T3> comparer3,
                IEqualityComparer<T4> comparer4,
                IEqualityComparer<T5> comparer5) =>
            new EqualityComparer<T1, T2, T3, T4, T5>(
                comparer1,
                comparer2,
                comparer3,
                comparer4,
                comparer5);

        sealed class EqualityComparer<T1, T2, T3, T4, T5> :
            IEqualityComparer<(T1, T2, T3, T4, T5)>
        {
            readonly IEqualityComparer<T1> _comparer1;
            readonly IEqualityComparer<T2> _comparer2;
            readonly IEqualityComparer<T3> _comparer3;
            readonly IEqualityComparer<T4> _comparer4;
            readonly IEqualityComparer<T5> _comparer5;

            public EqualityComparer(
                    IEqualityComparer<T1> comparer1,
                    IEqualityComparer<T2> comparer2,
                    IEqualityComparer<T3> comparer3,
                    IEqualityComparer<T4> comparer4,
                    IEqualityComparer<T5> comparer5)
            {
                _comparer1 = comparer1 ?? EqualityComparer<T1>.Default;
                _comparer2 = comparer2 ?? EqualityComparer<T2>.Default;
                _comparer3 = comparer3 ?? EqualityComparer<T3>.Default;
                _comparer4 = comparer4 ?? EqualityComparer<T4>.Default;
                _comparer5 = comparer5 ?? EqualityComparer<T5>.Default;
            }

            public bool Equals((T1, T2, T3, T4, T5) x,
                               (T1, T2, T3, T4, T5) y)
                => _comparer1.Equals(x.Item1, y.Item1)
                && _comparer2.Equals(x.Item2, y.Item2)
                && _comparer3.Equals(x.Item3, y.Item3)
                && _comparer4.Equals(x.Item4, y.Item4)
                && _comparer5.Equals(x.Item5, y.Item5);

            public int GetHashCode((T1, T2, T3, T4, T5) obj) =>
                HashCode.Combine(
                _comparer1.GetHashCode(obj.Item1),
                _comparer2.GetHashCode(obj.Item2),
                _comparer3.GetHashCode(obj.Item3),
                _comparer4.GetHashCode(obj.Item4),
                _comparer5.GetHashCode(obj.Item5)
                );
        }

        /// <summary>
        /// Creates and returns an <see cref="IEqualityComparer{T}"/> instance for
        /// a tuple of 6 elements where each element of the tuple uses a
        /// user-supplied <see cref="IEqualityComparer{T}"/> instance.
        /// </summary>
        public static IEqualityComparer<(T1, T2, T3, T4, T5, T6)>
            Create<T1, T2, T3, T4, T5, T6>(
                IEqualityComparer<T1> comparer1,
                IEqualityComparer<T2> comparer2,
                IEqualityComparer<T3> comparer3,
                IEqualityComparer<T4> comparer4,
                IEqualityComparer<T5> comparer5,
                IEqualityComparer<T6> comparer6) =>
            new EqualityComparer<T1, T2, T3, T4, T5, T6>(
                comparer1,
                comparer2,
                comparer3,
                comparer4,
                comparer5,
                comparer6);

        sealed class EqualityComparer<T1, T2, T3, T4, T5, T6> :
            IEqualityComparer<(T1, T2, T3, T4, T5, T6)>
        {
            readonly IEqualityComparer<T1> _comparer1;
            readonly IEqualityComparer<T2> _comparer2;
            readonly IEqualityComparer<T3> _comparer3;
            readonly IEqualityComparer<T4> _comparer4;
            readonly IEqualityComparer<T5> _comparer5;
            readonly IEqualityComparer<T6> _comparer6;

            public EqualityComparer(
                    IEqualityComparer<T1> comparer1,
                    IEqualityComparer<T2> comparer2,
                    IEqualityComparer<T3> comparer3,
                    IEqualityComparer<T4> comparer4,
                    IEqualityComparer<T5> comparer5,
                    IEqualityComparer<T6> comparer6)
            {
                _comparer1 = comparer1 ?? EqualityComparer<T1>.Default;
                _comparer2 = comparer2 ?? EqualityComparer<T2>.Default;
                _comparer3 = comparer3 ?? EqualityComparer<T3>.Default;
                _comparer4 = comparer4 ?? EqualityComparer<T4>.Default;
                _comparer5 = comparer5 ?? EqualityComparer<T5>.Default;
                _comparer6 = comparer6 ?? EqualityComparer<T6>.Default;
            }

            public bool Equals((T1, T2, T3, T4, T5, T6) x,
                               (T1, T2, T3, T4, T5, T6) y)
                => _comparer1.Equals(x.Item1, y.Item1)
                && _comparer2.Equals(x.Item2, y.Item2)
                && _comparer3.Equals(x.Item3, y.Item3)
                && _comparer4.Equals(x.Item4, y.Item4)
                && _comparer5.Equals(x.Item5, y.Item5)
                && _comparer6.Equals(x.Item6, y.Item6);

            public int GetHashCode((T1, T2, T3, T4, T5, T6) obj) =>
                HashCode.Combine(
                _comparer1.GetHashCode(obj.Item1),
                _comparer2.GetHashCode(obj.Item2),
                _comparer3.GetHashCode(obj.Item3),
                _comparer4.GetHashCode(obj.Item4),
                _comparer5.GetHashCode(obj.Item5),
                _comparer6.GetHashCode(obj.Item6)
                );
        }

        /// <summary>
        /// Creates and returns an <see cref="IEqualityComparer{T}"/> instance for
        /// a tuple of 7 elements where each element of the tuple uses a
        /// user-supplied <see cref="IEqualityComparer{T}"/> instance.
        /// </summary>
        public static IEqualityComparer<(T1, T2, T3, T4, T5, T6, T7)>
            Create<T1, T2, T3, T4, T5, T6, T7>(
                IEqualityComparer<T1> comparer1,
                IEqualityComparer<T2> comparer2,
                IEqualityComparer<T3> comparer3,
                IEqualityComparer<T4> comparer4,
                IEqualityComparer<T5> comparer5,
                IEqualityComparer<T6> comparer6,
                IEqualityComparer<T7> comparer7) =>
            new EqualityComparer<T1, T2, T3, T4, T5, T6, T7>(
                comparer1,
                comparer2,
                comparer3,
                comparer4,
                comparer5,
                comparer6,
                comparer7);

        sealed class EqualityComparer<T1, T2, T3, T4, T5, T6, T7> :
            IEqualityComparer<(T1, T2, T3, T4, T5, T6, T7)>
        {
            readonly IEqualityComparer<T1> _comparer1;
            readonly IEqualityComparer<T2> _comparer2;
            readonly IEqualityComparer<T3> _comparer3;
            readonly IEqualityComparer<T4> _comparer4;
            readonly IEqualityComparer<T5> _comparer5;
            readonly IEqualityComparer<T6> _comparer6;
            readonly IEqualityComparer<T7> _comparer7;

            public EqualityComparer(
                    IEqualityComparer<T1> comparer1,
                    IEqualityComparer<T2> comparer2,
                    IEqualityComparer<T3> comparer3,
                    IEqualityComparer<T4> comparer4,
                    IEqualityComparer<T5> comparer5,
                    IEqualityComparer<T6> comparer6,
                    IEqualityComparer<T7> comparer7)
            {
                _comparer1 = comparer1 ?? EqualityComparer<T1>.Default;
                _comparer2 = comparer2 ?? EqualityComparer<T2>.Default;
                _comparer3 = comparer3 ?? EqualityComparer<T3>.Default;
                _comparer4 = comparer4 ?? EqualityComparer<T4>.Default;
                _comparer5 = comparer5 ?? EqualityComparer<T5>.Default;
                _comparer6 = comparer6 ?? EqualityComparer<T6>.Default;
                _comparer7 = comparer7 ?? EqualityComparer<T7>.Default;
            }

            public bool Equals((T1, T2, T3, T4, T5, T6, T7) x,
                               (T1, T2, T3, T4, T5, T6, T7) y)
                => _comparer1.Equals(x.Item1, y.Item1)
                && _comparer2.Equals(x.Item2, y.Item2)
                && _comparer3.Equals(x.Item3, y.Item3)
                && _comparer4.Equals(x.Item4, y.Item4)
                && _comparer5.Equals(x.Item5, y.Item5)
                && _comparer6.Equals(x.Item6, y.Item6)
                && _comparer7.Equals(x.Item7, y.Item7);

            public int GetHashCode((T1, T2, T3, T4, T5, T6, T7) obj) =>
                HashCode.Combine(
                _comparer1.GetHashCode(obj.Item1),
                _comparer2.GetHashCode(obj.Item2),
                _comparer3.GetHashCode(obj.Item3),
                _comparer4.GetHashCode(obj.Item4),
                _comparer5.GetHashCode(obj.Item5),
                _comparer6.GetHashCode(obj.Item6),
                _comparer7.GetHashCode(obj.Item7)
                );
        }

        /// <summary>
        /// Creates and returns an <see cref="IEqualityComparer{T}"/> instance for
        /// a tuple of 8 elements where each element of the tuple uses a
        /// user-supplied <see cref="IEqualityComparer{T}"/> instance.
        /// </summary>
        public static IEqualityComparer<(T1, T2, T3, T4, T5, T6, T7, T8)>
            Create<T1, T2, T3, T4, T5, T6, T7, T8>(
                IEqualityComparer<T1> comparer1,
                IEqualityComparer<T2> comparer2,
                IEqualityComparer<T3> comparer3,
                IEqualityComparer<T4> comparer4,
                IEqualityComparer<T5> comparer5,
                IEqualityComparer<T6> comparer6,
                IEqualityComparer<T7> comparer7,
                IEqualityComparer<T8> comparer8) =>
            new EqualityComparer<T1, T2, T3, T4, T5, T6, T7, T8>(
                comparer1,
                comparer2,
                comparer3,
                comparer4,
                comparer5,
                comparer6,
                comparer7,
                comparer8);

        sealed class EqualityComparer<T1, T2, T3, T4, T5, T6, T7, T8> :
            IEqualityComparer<(T1, T2, T3, T4, T5, T6, T7, T8)>
        {
            readonly IEqualityComparer<T1> _comparer1;
            readonly IEqualityComparer<T2> _comparer2;
            readonly IEqualityComparer<T3> _comparer3;
            readonly IEqualityComparer<T4> _comparer4;
            readonly IEqualityComparer<T5> _comparer5;
            readonly IEqualityComparer<T6> _comparer6;
            readonly IEqualityComparer<T7> _comparer7;
            readonly IEqualityComparer<T8> _comparer8;

            public EqualityComparer(
                    IEqualityComparer<T1> comparer1,
                    IEqualityComparer<T2> comparer2,
                    IEqualityComparer<T3> comparer3,
                    IEqualityComparer<T4> comparer4,
                    IEqualityComparer<T5> comparer5,
                    IEqualityComparer<T6> comparer6,
                    IEqualityComparer<T7> comparer7,
                    IEqualityComparer<T8> comparer8)
            {
                _comparer1 = comparer1 ?? EqualityComparer<T1>.Default;
                _comparer2 = comparer2 ?? EqualityComparer<T2>.Default;
                _comparer3 = comparer3 ?? EqualityComparer<T3>.Default;
                _comparer4 = comparer4 ?? EqualityComparer<T4>.Default;
                _comparer5 = comparer5 ?? EqualityComparer<T5>.Default;
                _comparer6 = comparer6 ?? EqualityComparer<T6>.Default;
                _comparer7 = comparer7 ?? EqualityComparer<T7>.Default;
                _comparer8 = comparer8 ?? EqualityComparer<T8>.Default;
            }

            public bool Equals((T1, T2, T3, T4, T5, T6, T7, T8) x,
                               (T1, T2, T3, T4, T5, T6, T7, T8) y)
                => _comparer1.Equals(x.Item1, y.Item1)
                && _comparer2.Equals(x.Item2, y.Item2)
                && _comparer3.Equals(x.Item3, y.Item3)
                && _comparer4.Equals(x.Item4, y.Item4)
                && _comparer5.Equals(x.Item5, y.Item5)
                && _comparer6.Equals(x.Item6, y.Item6)
                && _comparer7.Equals(x.Item7, y.Item7)
                && _comparer8.Equals(x.Item8, y.Item8);

            public int GetHashCode((T1, T2, T3, T4, T5, T6, T7, T8) obj) =>
                HashCode.Combine(
                _comparer1.GetHashCode(obj.Item1),
                _comparer2.GetHashCode(obj.Item2),
                _comparer3.GetHashCode(obj.Item3),
                _comparer4.GetHashCode(obj.Item4),
                _comparer5.GetHashCode(obj.Item5),
                _comparer6.GetHashCode(obj.Item6),
                _comparer7.GetHashCode(obj.Item7),
                _comparer8.GetHashCode(obj.Item8)
                );
        }
    }
}