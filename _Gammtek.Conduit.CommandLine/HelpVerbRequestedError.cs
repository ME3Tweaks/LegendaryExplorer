using System;

namespace Gammtek.Conduit.CommandLine
{
	/// <summary>
	///     Models an error generated when a user explicit requests help in verb commands scenario.
	/// </summary>
	public sealed class HelpVerbRequestedError : Error
	{
		internal HelpVerbRequestedError(string verb, Type type, bool matched)
			: base(ErrorType.HelpVerbRequestedError)
		{
			Verb = verb;
			Type = type;
			Matched = matched;
		}

		/// <summary>
		///     <value>true</value>
		///     if verb command is found; otherwise
		///     <value>false</value>
		///     .
		/// </summary>
		public bool Matched { get; private set; }

		/// <summary>
		///     <see cref="System.Type" /> of verb command.
		/// </summary>
		public Type Type { get; private set; }

		/// <summary>
		///     Verb command string.
		/// </summary>
		public string Verb { get; private set; }
	}
}
