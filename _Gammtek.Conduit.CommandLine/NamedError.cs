using System;

namespace Gammtek.Conduit.CommandLine
{
	/// <summary>
	///     Base type of all erros with name information.
	/// </summary>
	public abstract class NamedError : Error, IEquatable<NamedError>
	{
		internal NamedError(ErrorType tag, NameInfo nameInfo)
			: base(tag)
		{
			NameInfo = nameInfo;
		}

		/// <summary>
		///     Name information relative to this error instance.
		/// </summary>
		public NameInfo NameInfo { get; private set; }

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
			var other = obj as NamedError;

			return other != null ? Equals(other) : base.Equals(obj);
		}

		/// <summary>
		///     Serves as a hash function for a particular type.
		/// </summary>
		/// <remarks>A hash code for the current <see cref="System.Object" />.</remarks>
		public override int GetHashCode()
		{
			return Tag.GetHashCode() ^ NameInfo.GetHashCode();
		}

		/// <summary>
		///     Returns a value that indicates whether the current instance and a specified <see cref="CommandLine.NamedError" /> have the same value.
		/// </summary>
		/// <param name="other">The <see cref="CommandLine.NamedError" /> instance to compare.</param>
		/// <returns>
		///     <value>true</value>
		///     if this instance of <see cref="CommandLine.NamedError" /> and <paramref name="other" /> have the same value; otherwise,
		///     <value>false</value>
		///     .
		/// </returns>
		public bool Equals(NamedError other)
		{
			if (other == null)
			{
				return false;
			}

			return Tag.Equals(other.Tag) && NameInfo.Equals(other.NameInfo);
		}
	}
}
