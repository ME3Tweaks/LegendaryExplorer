namespace Gammtek.Conduit.IO.Tlk.Binary
{
	public struct TlkStringRef
	{
		/// <summary>
		///     Initializes a new instance of the <see cref="TlkStringRef" /> class.
		/// </summary>
		public TlkStringRef(int id = 0, int offset = 0, int position = 0, string data = null, int stringStart = 0)
			: this()
		{
			Id = id;
			Offset = offset;
			Data = data;
			StringStart = stringStart;
			Position = position;
		}

		public string Data { get; set; }

		public int Id { get; set; }

		public int Offset { get; set; }

		public int Position { get; set; }

		public int StringStart { get; set; }
	}
}
