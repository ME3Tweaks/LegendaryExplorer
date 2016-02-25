using System.Collections.Generic;
using Gammtek.Conduit.UnrealEngine3.Serialization;

namespace Gammtek.Conduit.UnrealEngine3.Core
{
	public static class UArrayList
	{
		public static void Deserialize(this List<int> indexes, IUnrealStream stream)
		{
			indexes.Capacity = stream.ReadInt32();

			for (var index = 0; index < indexes.Capacity; ++index)
			{
				indexes.Add(stream.ReadIndex());
			}
		}
	}
}
