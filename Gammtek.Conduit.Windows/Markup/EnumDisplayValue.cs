namespace Gammtek.Conduit.Windows.Markup
{
	public class EnumDisplayValue
	{
		public EnumDisplayValue(object value, object name = null)
		{
			Value = value;
			Name = name ?? value;
		}

		public object Name { get; set; }

		public object Value { get; set; }
	}
}
