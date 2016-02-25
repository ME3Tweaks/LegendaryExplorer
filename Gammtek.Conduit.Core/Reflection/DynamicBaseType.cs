namespace Gammtek.Conduit.Reflection
{
	public abstract class DynamicBaseType
	{
		public abstract object GetPropertyValue(string propertyName);

		public abstract void SetPropertyValue(string propertyName, object value);
	}
}
