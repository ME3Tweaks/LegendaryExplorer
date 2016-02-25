using System.Collections.Generic;
using MassEffect3.SaveFormats;
using Newtonsoft.Json;

namespace MassEffect3.SaveEdit
{
	[JsonObject(MemberSerialization.OptIn)]
	public class PlotCategory
	{
		[JsonProperty(PropertyName = "bools")]
		public List<PlotBool> Bools = new List<PlotBool>();

		[JsonProperty(PropertyName = "floats")]
		public List<PlotFloat> Floats = new List<PlotFloat>();

		[JsonProperty(PropertyName = "ints")]
		public List<PlotInt> Ints = new List<PlotInt>();

		[JsonProperty(PropertyName = "variables")]
		public List<PlotPlayerVariable> PlayerVariables = new List<PlotPlayerVariable>();

		[JsonProperty(PropertyName = "multiline_bools")]
		public bool MultilineBools = true;

		[JsonProperty(PropertyName = "name", Required = Required.Always)]
		public string Name;

		[JsonProperty(PropertyName = "note")]
		public string Note;

		[JsonProperty(PropertyName = "order")]
		public int Order = int.MaxValue;
	}
}