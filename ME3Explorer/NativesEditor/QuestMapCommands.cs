using System.Windows.Input;

namespace MassEffect.NativesEditor
{
	public static class QuestMapCommands
	{
		static QuestMapCommands()
		{
			AddQuest = new RoutedUICommand("Add", "AddQuest", typeof(QuestMapCommands));
			AddQuestEntry = new RoutedUICommand("Add", "AddQuestEntry", typeof(QuestMapCommands));
			AddQuestGoal = new RoutedUICommand("Add", "AddQuestGoal", typeof(QuestMapCommands));
			AddQuestPlotItem = new RoutedUICommand("Add", "AddQuestPlotItem", typeof(QuestMapCommands));
			AddQuestTask = new RoutedUICommand("Add", "AddQuestTask", typeof(QuestMapCommands));

			RemoveQuest = new RoutedUICommand("Remove", "RemoveQuest", typeof(QuestMapCommands));
			RemoveQuestEntry = new RoutedUICommand("Remove", "RemoveQuestEntry", typeof(QuestMapCommands));
			RemoveQuestGoal = new RoutedUICommand("Remove", "RemoveQuestGoal", typeof(QuestMapCommands));
			RemoveQuestPlotItem = new RoutedUICommand("Remove", "RemoveQuestPlotItem", typeof(QuestMapCommands));
			RemoveQuestTask = new RoutedUICommand("Remove", "RemoveQuestTask", typeof(QuestMapCommands));
		}

		public static RoutedUICommand AddQuest { get; private set; }
		public static RoutedUICommand AddQuestEntry { get; private set; }
		public static RoutedUICommand AddQuestGoal { get; private set; }
		public static RoutedUICommand AddQuestPlotItem { get; private set; }
		public static RoutedUICommand AddQuestTask { get; private set; }

		public static RoutedUICommand RemoveQuest { get; private set; }
		public static RoutedUICommand RemoveQuestEntry { get; private set; }
		public static RoutedUICommand RemoveQuestGoal { get; private set; }
		public static RoutedUICommand RemoveQuestPlotItem { get; private set; }
		public static RoutedUICommand RemoveQuestTask { get; private set; }
	}
}