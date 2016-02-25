// Copyright 2005-2013 Giacomo Stelluti Scala & Contributors. All rights reserved. See doc/License.md in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Gammtek.Conduit.CommandLine.Infrastructure;

namespace Gammtek.Conduit.CommandLine
{
	/// <summary>
	///     Models a parser result. It contains an instance of type <typeparamref name="T" /> with parsed values and
	///     a sequence of <see cref="CommandLine.Error" />.
	/// </summary>
	/// <typeparam name="T">The type with attributes that define the syntax of parsing rules.</typeparam>
	public class ParserResult<T> : IEquatable<ParserResult<T>>
	{
		internal ParserResult(ParserResultType tag, T value, IEnumerable<Error> errors, Maybe<IEnumerable<Type>> verbTypes)
		{
			if (Equals(value, default(T)))
			{
				throw new ArgumentNullException("value");
			}

			if (errors == null)
			{
				throw new ArgumentNullException("errors");
			}

			Tag = tag;
			Value = value;
			Errors = errors;
			VerbTypes = verbTypes;
		}

		/// <summary>
		///     Gets the sequence of parsing errors.
		/// </summary>
		public IEnumerable<Error> Errors { get; private set; }

		/// <summary>
		///     Gets the instance with parsed values.
		/// </summary>
		public T Value { get; private set; }

		internal ParserResultType Tag { get; private set; }

		internal Maybe<IEnumerable<Type>> VerbTypes { get; private set; }

		/// <summary>
		///     Determines whether the specified <see cref="System.Object" /> is equal to the current <see cref="System.Object" />.
		/// </summary>
		/// <param name="obj">The <see cref="System.Object" /> to compare with the current <see cref="System.Object" />.</param>
		/// <returns>
		///     <value>true</value>
		///     if the specified <see cref="System.Object" /> is equal to the current <see cref="System.Object" />; otherwise,
		///     <value>false</value>
		///     .
		/// </returns>
		public override bool Equals(object obj)
		{
			var other = obj as ParserResult<T>;

			if (other != null)
			{
				return Equals(other);
			}

			return base.Equals(obj);
		}

		/// <summary>
		///     Serves as a hash function for a particular type.
		/// </summary>
		/// <remarks>A hash code for the current <see cref="System.Object" />.</remarks>
		public override int GetHashCode()
		{
			return Value.GetHashCode() ^ Errors.GetHashCode();
		}

		/// <summary>
		///     Returns a value that indicates whether the current instance and a specified <see cref="CommandLine.ParserResult{T}" /> have the same value.
		/// </summary>
		/// <param name="other">The <see cref="CommandLine.ParserResult{T}" /> instance to compare.</param>
		/// <returns>
		///     <value>true</value>
		///     if this instance of <see cref="CommandLine.ParserResult{T}" /> and <paramref name="other" /> have the same value; otherwise,
		///     <value>false</value>
		///     .
		/// </returns>
		public bool Equals(ParserResult<T> other)
		{
			if (other == null)
			{
				return false;
			}

			return Value.Equals(other.Value) && Errors.SequenceEqual(other.Errors);
		}
	}

	internal static class ParserResult
	{
		public static ParserResult<T> Create<T>(ParserResultType tag, T instance, IEnumerable<Error> errors)
		{
			return Create(tag, instance, errors, Maybe.Nothing<IEnumerable<Type>>());
		}

		public static ParserResult<T> Create<T>(ParserResultType tag, T instance, IEnumerable<Error> errors, Maybe<IEnumerable<Type>> verbTypes)
		{
			if (Equals(instance, default(T)))
			{
				throw new ArgumentNullException("instance");
			}

			if (errors == null)
			{
				throw new ArgumentNullException("errors");
			}

			if (verbTypes == null)
			{
				throw new ArgumentNullException("verbTypes");
			}

			return new ParserResult<T>(tag, instance, errors, verbTypes);
		}

		public static ParserResult<T> MapErrors<T>(this ParserResult<T> parserResult, Func<IEnumerable<Error>, IEnumerable<Error>> func)
		{
			return new ParserResult<T>(parserResult.Tag, parserResult.Value, func(parserResult.Errors), parserResult.VerbTypes);
		}
	}
}
