using System;
using System.Collections.Generic;

namespace Gammtek.Conduit.MassEffect.Localization.Tlk
{
	public class TlkFile
	{
		public TlkFile(IList<TlkString> strings = null)
		{
			MaleStrings = strings ?? new List<TlkString>();
			FemaleStrings = strings ?? new List<TlkString>();
		}

		public IList<TlkString> FemaleStrings { get; set; }

		public string Id { get; set; }

		public IList<TlkString> MaleStrings { get; set; }

		public string Name { get; set; }

		public string Source { get; set; }
	}
}
