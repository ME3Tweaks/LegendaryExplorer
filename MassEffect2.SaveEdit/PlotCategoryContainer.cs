using System.Collections.Generic;
using Newtonsoft.Json;

namespace MassEffect2.SaveEdit
{
	[JsonObject(MemberSerialization.OptIn)]
	public class PlotCategoryContainer
	{
		[JsonProperty(PropertyName = "categories")]
		public List<PlotCategory> Categories = new List<PlotCategory>();

		[JsonProperty(PropertyName = "name", Required = Required.Always)]
		public string Name;

		[JsonProperty(PropertyName = "order")]
		public int Order = int.MaxValue;
	}
}