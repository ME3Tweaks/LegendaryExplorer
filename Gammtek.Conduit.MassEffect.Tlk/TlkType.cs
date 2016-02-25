using System.ComponentModel.DataAnnotations;

namespace Gammtek.Conduit.MassEffect.Tlk
{
	public enum TlkType
	{
		[Display(Name = @"Binary")]
		Binary,

		[Display(Name = @"None")]
		None,

		[Display(Name = @"Xml")]
		Xml
	}
}
