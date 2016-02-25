using System;

namespace Gammtek.Conduit.CommandLine
{
	/// <summary>
	///     Base type of all errors related to bad token detection.
	/// </summary>
	public abstract class TokenError : Error, IEquatable<TokenError>
	{
		internal TokenError(ErrorType tag, string token)
			: base(tag)
		{
			if (token == null)
			{
				throw new ArgumentNullException("token");
			}

			Token = token;
		}

		/// <summary>
		///     The string containing the token text.
		/// </summary>
		public string Token { get; private set; }

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
			var other = obj as TokenError;

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
			return Tag.GetHashCode() ^ Token.GetHashCode();
		}

		/// <summary>
		///     Returns a value that indicates whether the current instance and a specified <see cref="CommandLine.TokenError" /> have the same value.
		/// </summary>
		/// <param name="other">The <see cref="CommandLine.TokenError" /> instance to compare.</param>
		/// <returns>
		///     <value>true</value>
		///     if this instance of <see cref="CommandLine.TokenError" /> and <paramref name="other" /> have the same value; otherwise,
		///     <value>false</value>
		///     .
		/// </returns>
		public bool Equals(TokenError other)
		{
			if (other == null)
			{
				return false;
			}

			return Tag.Equals(other.Tag) && Token.Equals(other.Token);
		}
	}
}
