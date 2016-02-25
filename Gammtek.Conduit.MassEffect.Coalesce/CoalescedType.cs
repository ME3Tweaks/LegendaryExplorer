using System.ComponentModel.DataAnnotations;

namespace Gammtek.Conduit.MassEffect.Coalesce
{
	public enum CoalescedType
	{
		[Display(Name = @"Binary")]
		Binary,

		[Display(Name = @"None")]
		None,

		[Display(Name = @"Xml")]
		Xml
	}
}
