using System.Collections.Generic;

namespace Gammtek.Conduit.IO.Tlk
{
	public class TlkEntries : List<TlkEntry>
	{
		/// <summary>
		///     Initializes a new instance of the <see cref="T:Gammtek.Conduit.IO.Tlk.TlkEntries" /> class that is empty and has the default initial
		///     capacity.
		/// </summary>
		public TlkEntries() {}

		/// <summary>
		///     Initializes a new instance of the <see cref="T:Gammtek.Conduit.IO.Tlk.TlkEntries" /> class that is empty and has the specified initial
		///     capacity.
		/// </summary>
		/// <param name="capacity">The number of elements that the new list can initially store.</param>
		/// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="capacity" /> is less than 0. </exception>
		public TlkEntries(int capacity)
			: base(capacity) {}

		/// <summary>
		///     Initializes a new instance of the <see cref="T:Gammtek.Conduit.IO.Tlk.TlkEntries" /> class that contains elements copied from the
		///     specified collection and has sufficient capacity to accommodate the number of elements copied.
		/// </summary>
		/// <param name="collection">The collection whose elements are copied to the new list.</param>
		/// <exception cref="T:System.ArgumentNullException"><paramref name="collection" /> is null.</exception>
		public TlkEntries(IEnumerable<TlkEntry> collection)
			: base(collection) {}
	}
}
