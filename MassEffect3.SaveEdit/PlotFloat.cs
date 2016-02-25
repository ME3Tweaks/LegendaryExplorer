using Newtonsoft.Json;

namespace MassEffect3.SaveEdit
{
	[JsonObject(MemberSerialization.OptIn)]
	public class PlotFloat
	{
		[JsonProperty(PropertyName = "hint", Required = Required.Default)]
		public string Hint;

		[JsonProperty(PropertyName = "id", Required = Required.Always)]
		public int Id;

		[JsonProperty(PropertyName = "name", Required = Required.Always)]
		public string Name;
	}
}