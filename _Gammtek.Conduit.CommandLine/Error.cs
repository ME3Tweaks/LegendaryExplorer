using System;

namespace Gammtek.Conduit.CommandLine
{
	/// <summary>
	///     Base type of all errors.
	/// </summary>
	/// <remarks>All errors are defined within the system. There's no reason to create custom derivate types.</remarks>
	public abstract class Error : IEquatable<Error>
	{
		internal Error(ErrorType tag)
		{
			Tag = tag;
		}

		/// <summary>
		///     Error type discriminator, defined as <see cref="CommandLine.ErrorType" /> enumeration.
		/// </summary>
		public ErrorType Tag { get; private set; }

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
			var other = obj as Error;

			return other != null && Equals(other);
		}

		/// <summary>
		///     Serves as a hash function for a particular type.
		/// </summary>
		/// <remarks>A hash code for the current <see cref="System.Object" />.</remarks>
		public override int GetHashCode()
		{
			return Tag.GetHashCode();
		}

		/// <summary>
		///     Returns a value that indicates whether the current instance and a specified <see cref="CommandLine.Error" /> have the same value.
		/// </summary>
		/// <param name="other">The <see cref="CommandLine.Error" /> instance to compare.</param>
		/// <returns>
		///     <value>true</value>
		///     if this instance of <see cref="CommandLine.Error" /> and <paramref name="other" /> have the same value; otherwise,
		///     <value>false</value>
		///     .
		/// </returns>
		public bool Equals(Error other)
		{
			return other != null && Tag.Equals(other.Tag);
		}
	}
}
