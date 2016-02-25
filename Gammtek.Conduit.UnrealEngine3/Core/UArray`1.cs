using System.Collections.Generic;

namespace Gammtek.Conduit.UnrealEngine3.Core
{
	public class UArray<T> : List<T> // where T : IUnrealDeserializableClass, new()
	{
		/*public UArray(IUnrealStream stream = null, int count = -1)
		{
			if (stream != null)
			{
				Deserialize(stream, count);
			}
		}

		public void Deserialize(IUnrealStream stream, int count = -1, Action<T> action = null)
		{
			Capacity = (count < 0) ? stream.ReadIndex() : count;

			for (var index = 0; index < Capacity; ++index)
			{
				var obj = new T();

				if (action != null)
				{
					action(obj);
				}

				obj.Deserialize(stream);

				Add(obj);
			}
		}*/
	}
}
