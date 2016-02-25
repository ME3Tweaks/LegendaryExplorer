namespace Gammtek.Conduit.Mvp.Xml.Common.XPath
{
	public struct XPathVariable
	{
		private readonly string _name;
		private readonly object _value;

		public XPathVariable(string name, object value)
		{
			_name = name;
			_value = value;
		}

		public string Name
		{
			get { return _name; }
		}

		public object Value
		{
			get { return _value; }
		}

		public override bool Equals(object obj)
		{
			return Name == ((XPathVariable) obj).Name &&
				Value == ((XPathVariable) obj).Value;
		}

		public override int GetHashCode()
		{
			return (Name + "." + Value.GetHashCode()).GetHashCode();
		}

		public static bool operator ==(XPathVariable a, XPathVariable b)
		{
			return a.Equals(b);
		}

		public static bool operator !=(XPathVariable a, XPathVariable b)
		{
			return !a.Equals(b);
		}
	}
}
