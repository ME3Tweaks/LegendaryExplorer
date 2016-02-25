using System.Collections.Generic;
using Newtonsoft.Json;

namespace MassEffect2.SaveEdit
{
	[JsonObject(MemberSerialization.OptIn)]
	public class AppearancePreset
	{
		[JsonProperty(PropertyName = "HairMesh")]
		public string HairMesh;

		[JsonProperty(PropertyName = "Name")]
		public string Name;

		[JsonProperty(PropertyName = "Scalars")]
		public Parameter<float> Scalars = new Parameter<float>();

		[JsonProperty(PropertyName = "Textures")]
		public Parameter<string> Textures = new Parameter<string>();

		[JsonProperty(PropertyName = "Vectors")]
		public Parameter<LinearColor> Vectors = new Parameter<LinearColor>();

		[JsonObject(MemberSerialization.OptIn)]
		public class LinearColor
		{
			[JsonProperty(PropertyName = "A")]
			public float A;

			[JsonProperty(PropertyName = "B")]
			public float B;

			[JsonProperty(PropertyName = "G")]
			public float G;

			[JsonProperty(PropertyName = "R")]
			public float R;
		}

		[JsonObject(MemberSerialization.OptIn)]
		public class Parameter<TType>
		{
			[JsonProperty(PropertyName = "Add")]
			public List<KeyValuePair<string, TType>> Add = new List<KeyValuePair<string, TType>>();

			[JsonProperty(PropertyName = "Clear")]
			public bool Clear;

			[JsonProperty(PropertyName = "Remove")]
			public List<string> Remove = new List<string>();

			[JsonProperty(PropertyName = "Set")]
			public List<KeyValuePair<string, TType>> Set = new List<KeyValuePair<string, TType>>();
		}
	}
}