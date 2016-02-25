using System.Windows.Input;

namespace MassEffect.NativesEditor
{
	public static class StateEventMapCommands
	{
		static StateEventMapCommands()
		{
			AddStateEvent = new RoutedUICommand("Add", "AddStateEvent", typeof(StateEventMapCommands));
			AddStateEventElement = new RoutedUICommand("Add", "AddStateEventElement", typeof(StateEventMapCommands));

			RemoveStateEvent = new RoutedUICommand("Remove", "RemoveStateEvent", typeof(StateEventMapCommands));
			RemoveStateEventElement = new RoutedUICommand("Remove", "RemoveStateEventElement", typeof(StateEventMapCommands));
		}

		public static RoutedUICommand AddStateEvent { get; private set; }
		public static RoutedUICommand AddStateEventElement { get; private set; }
		
		public static RoutedUICommand RemoveStateEvent { get; private set; }
		public static RoutedUICommand RemoveStateEventElement { get; private set; }
	}
}