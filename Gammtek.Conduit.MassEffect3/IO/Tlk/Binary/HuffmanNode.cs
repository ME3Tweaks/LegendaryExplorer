namespace Gammtek.Conduit.IO.Tlk.Binary
{
	public struct HuffmanNode
	{
		/// <summary>
		///     Initializes a new instance of the <see cref="T:Gammtek.Conduit.IO.Tlk.Binary.HuffmanNode" /> struct.
		/// </summary>
		public HuffmanNode(int leftNodeId = 0, int rightNodeId = 0)
			: this()
		{
			LeftNodeId = leftNodeId;
			RightNodeId = rightNodeId;
		}

		public int LeftNodeId { get; set; }

		public int RightNodeId { get; set; }
	}
}
