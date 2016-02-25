using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;

namespace NDepend.Path
{
	/// <summary>
	///     Provides a set of extension methods dedicated to enumerables.
	/// </summary>
	internal static class InternalEnumerableExtensions
	{
		/// <summary>
		///     Append <paramref name="element" /> at the end of <paramref name="source" />.
		/// </summary>
		/// <typeparam name="T">The element type.</typeparam>
		/// <param name="source">A sequence of elements.</param>
		/// <param name="element">The element to append.</param>
		/// <remarks>This extension method has a <i>constant</i> time complexity.</remarks>
		public static IEnumerable<T> Append<T>(this IEnumerable<T> source, T element)
		{
			Contract.Requires(source != null, "seq must not be null");

			//Contract.Requires(element != null, "element must not be null");
			Contract.Ensures(Contract.Result<IEnumerable<T>>() != null, "returned sequence is not null");
			Contract.Ensures(Contract.Result<IEnumerable<T>>().Any(), "returned sequence contains at least element");

			foreach (var elem in source)
			{
				yield return elem;
			}

			yield return element;
		}

		/// <summary>
		///     Append <paramref name="element1" /> and <paramref name="element1" /> at the end of <paramref name="source" />.
		/// </summary>
		/// <typeparam name="T">The element type.</typeparam>
		/// <param name="source">A sequence of elements.</param>
		/// <param name="element1">The element to append first.</param>
		/// <param name="element2">The element to append after <paramref name="element1" />.</param>
		/// <remarks>This extension method has a <i>constant</i> time complexity.</remarks>
		public static IEnumerable<T> Append<T>(this IEnumerable<T> source, T element1, T element2)
		{
			Contract.Requires(source != null, "seq must not be null");

			//Contract.Requires(element != null, "element must not be null");
			Contract.Ensures(Contract.Result<IEnumerable<T>>() != null, "returned sequence is not null");
			Contract.Ensures(Contract.Result<IEnumerable<T>>().Any(), "returned sequence contains at least element");

			foreach (var elem in source)
			{
				yield return elem;
			}

			yield return element1;
			yield return element2;
		}

		/// <summary>
		///     Append <paramref name="element1" />, <paramref name="element2" /> and <paramref name="element3" /> at the end of <paramref name="source" />.
		/// </summary>
		/// <remarks>To append more than 3 elements to <paramref name="source" />, use the <i>.Concat(new [] { a, b, c, d ... } )</i> syntax.</remarks>
		/// <typeparam name="T">The element type.</typeparam>
		/// <param name="source">A sequence of elements.</param>
		/// <param name="element1">The element to append first.</param>
		/// <param name="element2">The element to append after <paramref name="element1" />.</param>
		/// <param name="element3">The element to append after <paramref name="element2" />.</param>
		/// <remarks>This extension method has a <i>constant</i> time complexity.</remarks>
		public static IEnumerable<T> Append<T>(this IEnumerable<T> source, T element1, T element2, T element3)
		{
			Contract.Requires(source != null, "seq must not be null");

			//Contract.Requires(element != null, "element must not be null");
			Contract.Ensures(Contract.Result<IEnumerable<T>>() != null, "returned sequence is not null");
			Contract.Ensures(Contract.Result<IEnumerable<T>>().Any(), "returned sequence contains at least element");

			foreach (var elem in source)
			{
				yield return elem;
			}

			yield return element1;
			yield return element2;
			yield return element3;
		}

		/// <summary>
		///     Gets an enumerable object that contains first <paramref name="source" /> and then elements of <paramref name="elements" />, in this order.
		/// </summary>
		/// <typeparam name="TElement">The type of <paramref name="source" />.</typeparam>
		/// <param name="source">The first element in the returned enumerable.</param>
		/// <param name="elements">The last elements in the returned enumerable.</param>
		public static IEnumerable<TElement> Concat<TElement>(this TElement source, IEnumerable<TElement> elements)
		{
			Contract.Ensures(Contract.Result<IEnumerable<TElement>>() != null, "returned enumerable object is not null");

			return new[] { source }.Concat(elements);
		}

		/// <summary>
		///     Gets an enumerable object that contains the elements of <paramref name="source" /> and then <paramref name="element" />, in this order.
		/// </summary>
		/// <typeparam name="TElement">The type of <paramref name="element" />.</typeparam>
		/// <param name="source">The first elements in the returned enumerable.</param>
		/// <param name="element">The last element in the returned enumerable.</param>
		public static IEnumerable<TElement> Concat<TElement>(this IEnumerable<TElement> source, TElement element)
		{
			Contract.Ensures(Contract.Result<IEnumerable<TElement>>() != null, "returned enumerable object is not null");

			return source.Concat(new[] { element });
		}
		
		/// <summary>
		///     Produces the set <paramref name="source" /> excluding <paramref name="excludedElement" />. The equality test relies on the <i>Equals()</i>
		///     method.
		/// </summary>
		/// <typeparam name="T">The element type.</typeparam>
		/// <param name="source">A sequence of elements.</param>
		/// <param name="excludedElement">The element excluded.</param>
		/// <returns>A sequence that contains the set difference of the elements of <paramref name="source" /> minus <paramref name="excludedElement" />.</returns>
		/// <remarks>This extension method has a <i>constant</i> time complexity.</remarks>
		public static IEnumerable<T> Except<T>(this IEnumerable<T> source, T excludedElement)
		{
			Contract.Requires(source != null, "seq must not be null");

			//Contract.Requires(elementExcluded != null, "elementExcluded must not be null");
			Contract.Ensures(Contract.Result<IEnumerable<T>>() != null, "returned sequence is not null");

			return source.Where(element => !excludedElement.Equals(element));
		}

		/// <summary>
		///     Determines the index of a specific item in <paramref name="source" />.
		/// </summary>
		/// <typeparam name="T">The type parameter of the items in the read-only list.</typeparam>
		/// <param name="source"></param>
		/// <param name="value">The object to locate in <paramref name="source" />.</param>
		/// <returns>The index of value if found in the list; otherwise, -1.</returns>
		/// <remarks>This method uses the EqualityComparer$lt;T&gt;.Default.Equals() method on <paramref name="value" /> to determine whether item exists</remarks>
		public static int IndexOf<T>(this IReadOnlyList<T> source, T value)
		{
			Contract.Requires(source != null, "readOnlyList must not be null");

			var count = source.Count;
			var equalityComparer = EqualityComparer<T>.Default;

			for (var i = 0; i < count; i++)
			{
				var current = source[i];

				if (equalityComparer.Equals(current, value))
				{
					return i;
				}
			}

			return -1;
		}

		/// <summary>
		///     Gets an enumerable object that contains only the single element <paramref name="source" />.
		/// </summary>
		/// <typeparam name="TElement">The type of <paramref name="source" />.</typeparam>
		/// <param name="source">The single element in the returned enumerable.</param>
		public static IEnumerable<TElement> ToEnumerable<TElement>(this TElement source)
		{
			Contract.Ensures(Contract.Result<IEnumerable<TElement>>() != null, "returned enumerable object is not null");

			// This implementation is faster in all case than:   yield return elem;
			return new[] { source };
		}

		/// <summary>
		///     Creates a <see cref="Lookup{TKey,TElement}" /> from an <see cref="IEnumerable{T}" /> according to a specified key selector function.
		///     <b>The funtion can return zero, one or several keys for an element.</b>.
		/// </summary>
		/// <remarks>
		///     The difference with
		///     <seealso cref="Enumerable.ToLookup{TSource,TKey}(IEnumerable{TSource}, Func{TSource,TKey})" /> is that an
		///     element can have zero, one or several keys.<br />
		///     Hence an element can be contained in several groups, if it has several keys.
		/// </remarks>
		/// <typeparam name="T">The type of the elements of <paramref name="source" />.</typeparam>
		/// <typeparam name="TKey">The type of the keys in the result <see cref="Lookup{TKey,TElement}" />.</typeparam>
		/// <param name="source">An <i>IEnumerable&lt;T&gt;</i> to create an hashset from.</param>
		/// <param name="func">A function to extract a sequence of keys from each element.</param>
		/// <returns>An hashset that contains the elements from the input sequence.</returns>
		/// <seealso cref="Enumerable.ToLookup{TSource,TKey}(IEnumerable{TSource}, Func{TSource,TKey})" />
		public static ILookup<TKey, T> ToMultiKeyLookup<T, TKey>(this IEnumerable<T> source, Func<T, IEnumerable<TKey>> func)
		{
			Contract.Requires(source != null, "seq must not be null");
			Contract.Requires(func != null, "func must not be null");
			Contract.Ensures(Contract.Result<ILookup<TKey, T>>() != null, "returned lookup object is not null");

			Debug.Assert(source != null);

			var dictionaryLookup = new DictionaryLookup<TKey, T>();

			foreach (var elem in source)
			{
				var keys = func(elem);

				if (keys == null)
				{
					continue;
				}

				foreach (var key in keys)
				{
					IEnumerable<T> tempEnumerable;

					if (!dictionaryLookup.TryGetValue(key, out tempEnumerable))
					{
						// A single sequence is added first, and if several elements share the same key we transform the single sequence into a list.
						// This is an optimisation because a single sequence is cheaper than a List<T>,
						// and we hope that most keys are not not shared by several elements.
						dictionaryLookup.Add(key, elem.ToEnumerable());

						continue;
					}

					var list = tempEnumerable as List<T>;

					if (list != null)
					{
						list.Add(elem);

						continue;
					}

					list = new List<T> { tempEnumerable.First(), elem };
					dictionaryLookup[key] = list;
				}
			}

			return dictionaryLookup;
		}

		/// <summary>
		///     Creates a <see cref="IReadOnlyCollection{T}" /> cloned collection around <paramref name="source" />.
		/// </summary>
		/// <typeparam name="T">The type parameter of the items in the collection.</typeparam>
		/// <param name="source">A collection object.</param>
		public static IReadOnlyCollection<T> ToReadOnlyClonedCollection<T>(this IEnumerable<T> source)
		{
			Contract.Requires(source != null, "collection must not be null");
			Contract.Ensures(Contract.Result<IReadOnlyCollection<T>>() != null, "returned read-only collection is not null");
			Contract.Ensures(Contract.Result<IReadOnlyCollection<T>>().Count == source.Count(),
				"returned read-only collection has the same number of elements as collection");

			return new CollectionReadOnlyWrapper<T>(source.ToArray());
		}

		/// <summary>
		///     Creates a <see cref="IReadOnlyCollection{T}" /> cloned collection around <paramref name="source" />.
		/// </summary>
		/// <typeparam name="T">The type parameter of the items in the list.</typeparam>
		/// <param name="source">A list object.</param>
		public static IReadOnlyList<T> ToReadOnlyClonedList<T>(this IEnumerable<T> source)
		{
			Contract.Requires(source != null, "collection must not be null");
			Contract.Ensures(Contract.Result<IReadOnlyList<T>>() != null, "returned read-only list is not null");
			Contract.Ensures(Contract.Result<IReadOnlyList<T>>().Count == source.Count(), "returned read-only list has the same number of elements as list");

			return new ListReadOnlyWrapper<T>(source.ToArray());
		}

		/// <summary>
		///     Creates a <see cref="IReadOnlyCollection{T}" /> wrapper collection around <paramref name="source" />.
		/// </summary>
		/// <typeparam name="T">The type parameter of the items in the collection.</typeparam>
		/// <param name="source">A collection object.</param>
		public static IReadOnlyCollection<T> ToReadOnlyWrappedCollection<T>(this ICollection<T> source)
		{
			Contract.Requires(source != null, "collection must not be null");
			Contract.Ensures(Contract.Result<IReadOnlyCollection<T>>() != null, "returned read-only collection is not null");
			Contract.Ensures(Contract.Result<IReadOnlyCollection<T>>().Count == source.Count,
				"returned read-only collection has the same number of elements as collection");

			return new CollectionReadOnlyWrapper<T>(source);
		}

		/// <summary>
		///     Creates a <see cref="IReadOnlyCollection{T}" /> wrapper collection around <paramref name="source" />.
		/// </summary>
		/// <typeparam name="T">The type parameter of the items in the list.</typeparam>
		/// <param name="source">A list object.</param>
		public static IReadOnlyList<T> ToReadOnlyWrappedList<T>(this IList<T> source)
		{
			Contract.Requires(source != null, "collection must not be null");
			Contract.Ensures(Contract.Result<IReadOnlyList<T>>() != null, "returned read-only list is not null");
			Contract.Ensures(Contract.Result<IReadOnlyList<T>>().Count == source.Count, "returned read-only list has the same number of elements as list");

			return new ListReadOnlyWrapper<T>(source);
		}
		
		/// <summary>
		///     Add a pair of <paramref name="key" /> and <paramref name="value" /> to <paramref name="dictionary" />, only if <paramref name="dictionary" />
		///     doesn't already
		///     contain the <paramref name="key" />.
		/// </summary>
		/// <returns>
		///     <i>false</i> if the <paramref name="dictionary" /> already contains the <paramref name="key" />. Else add the pair <paramref name="key" /> and
		///     <paramref name="value" /> and returns <i>true</i>.
		/// </returns>
		/// <param name="dictionary">This dictionary.</param>
		/// <param name="key">The key.</param>
		/// <param name="value">The value.</param>
		public static bool TryAdd<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue value)
		{
			Contract.Requires(dictionary != null, "dico must not be null");

			if (dictionary.ContainsKey(key))
			{
				return false;
			}

			dictionary.Add(key, value);

			return true;
		}

		private class CollectionReadOnlyWrapper<T> : IReadOnlyCollection<T>
		{
			internal CollectionReadOnlyWrapper(ICollection<T> collection)
			{
				Debug.Assert(collection != null);

				Collection = collection;
			}

			public int Count => Collection.Count;

			private ICollection<T> Collection { get; }

			public IEnumerator<T> GetEnumerator()
			{
				return Collection.GetEnumerator();
			}

			IEnumerator IEnumerable.GetEnumerator()
			{
				return ((IEnumerable) Collection).GetEnumerator();
			}
		}

		private sealed class DictionaryLookup<TKey, T> : Dictionary<TKey, IEnumerable<T>>, ILookup<TKey, T>
		{
			public new int Count => base.Count;

			public new IEnumerable<T> this[TKey key]
			{
				get { return base[key]; }
				set { base[key] = value; }
			}

			public new IEnumerator<IGrouping<TKey, T>> GetEnumerator()
			{
				var dictionary = this as Dictionary<TKey, IEnumerable<T>>;

				return dictionary.Select(pair => new Grouping(pair.Key, pair.Value)).Cast<IGrouping<TKey, T>>().GetEnumerator();
			}

			bool ILookup<TKey, T>.Contains(TKey key)
			{
				return ContainsKey(key);
			}

			private sealed class Grouping : IGrouping<TKey, T>
			{
				internal Grouping(TKey key, IEnumerable<T> seq)
				{
					Debug.Assert(seq != null);

					Key = key;
					Enumerable = seq;
				}

				public TKey Key { get; }

				private IEnumerable<T> Enumerable { get; }

				public IEnumerator<T> GetEnumerator()
				{
					return Enumerable.GetEnumerator();
				}

				IEnumerator IEnumerable.GetEnumerator()
				{
					return GetEnumerator();
				}
			}
		}

		private sealed class ListReadOnlyWrapper<T> : CollectionReadOnlyWrapper<T>, IReadOnlyList<T>
		{
			internal ListReadOnlyWrapper(IList<T> list)
				: base(list)
			{
				Debug.Assert(list != null);

				List = list;
			}

			private IList<T> List { get; }

			public T this[int index] => List[index];
		}
	}
}
