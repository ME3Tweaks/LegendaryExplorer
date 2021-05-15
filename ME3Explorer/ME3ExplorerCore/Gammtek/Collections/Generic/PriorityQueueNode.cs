namespace ME3ExplorerCore.Gammtek.Collections.Generic
{
	public class PriorityQueueNode : IPriorityQueueNode
	{
		public long InsertionIndex { get; set; }

		public double Priority { get; set; }

		public int QueueIndex { get; set; }
	}
}
