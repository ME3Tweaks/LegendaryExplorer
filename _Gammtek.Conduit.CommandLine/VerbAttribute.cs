// Copyright 2005-2013 Giacomo Stelluti Scala & Contributors. All rights reserved. See doc/License.md in the project root for license information.

using System;

namespace Gammtek.Conduit.CommandLine
{
	/// <summary>
	///     Models a verb command specification.
	/// </summary>
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
	public sealed class VerbAttribute : Attribute
	{
		private string _helpText;

		/// <summary>
		///     Initializes a new instance of the <see cref="CommandLine.VerbAttribute" /> class.
		/// </summary>
		/// <param name="name">The long name of the verb command.</param>
		/// <exception cref="System.ArgumentException">Thrown if <paramref name="name" /> is null, empty or whitespace.</exception>
		public VerbAttribute(string name)
		{
			if (string.IsNullOrWhiteSpace(name))
			{
				throw new ArgumentException("name");
			}

			Name = name;
		}

		/// <summary>
		///     Gets or sets a short description of this command line option. Usually a sentence summary.
		/// </summary>
		public string HelpText
		{
			get { return _helpText; }
			set
			{
				if (value == null)
				{
					throw new ArgumentNullException("value");
				}

				_helpText = value;
			}
		}

		/// <summary>
		///     Gets the verb name.
		/// </summary>
		public string Name { get; private set; }
	}
}
