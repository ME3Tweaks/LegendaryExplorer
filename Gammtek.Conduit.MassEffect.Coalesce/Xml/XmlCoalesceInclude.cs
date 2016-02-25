namespace Gammtek.Conduit.MassEffect.Coalesce.Xml
{
	public class XmlCoalesceInclude
	{
		public XmlCoalesceInclude(string source, string pointer = null, bool required = false)
		{
			Pointer = pointer;
			Required = required;
			Source = source;
		}

		public string Pointer { get; set; }

		public bool Required { get; set; }

		public string Source { get; set; }
	}
}
