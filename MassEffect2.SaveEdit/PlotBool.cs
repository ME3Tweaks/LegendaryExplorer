using Newtonsoft.Json;

namespace MassEffect2.SaveEdit
{
	[JsonObject(MemberSerialization.OptIn)]
	public class PlotBool
	{
		[JsonProperty(PropertyName = "hint", Required = Required.Default)]
		public string Hint;

		[JsonProperty(PropertyName = "id", Required = Required.Always)]
		public int Id;

		[JsonProperty(PropertyName = "name", Required = Required.Always)]
		public string Name;

		public override string ToString()
		{
			return Name;
		}
	}
}