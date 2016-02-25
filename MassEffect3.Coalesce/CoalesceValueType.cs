using Gammtek.Conduit.Attributes.EnumAttributes;

namespace MassEffect3.Coalesce
{
	public enum CoalesceValueType
	{
		[StringValue("Add or Replace")]
		AddOrReplace = 0,

		[StringValue("Clear")]
		Clear = 1,

		[StringValue("Add Unique")]
		AddUnique = 2,

		[StringValue("Add")]
		Add = 3,

		[StringValue("Remove")]
		Remove = 4
	}
}