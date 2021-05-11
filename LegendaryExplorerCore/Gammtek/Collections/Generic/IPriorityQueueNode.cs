namespace LegendaryExplorerCore.Gammtek.Collections.Generic
{
	public interface IPriorityQueueNode
	{
		long InsertionIndex { get; set; }

		double Priority { get; set; }

		int QueueIndex { get; set; }
	}
}
