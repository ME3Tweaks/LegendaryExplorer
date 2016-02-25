using System.Collections.Generic;
using Caliburn.Micro;

namespace Gammtek.Conduit.Caliburn.Micro
{
	public static class BindableCollectionExtensions
	{
		[NotNull]
		public static BindableCollection<T> InitCollection<T>()
		{
			return new BindableCollection<T>();
		}

		[NotNull]
		public static BindableCollection<T> InitCollection<T>(IEnumerable<T> collection)
		{
			if (collection == null)
			{
				ThrowHelper.ThrowArgumentNullException("collection");
			}

			return new BindableCollection<T>(collection);
		}
	}
}
