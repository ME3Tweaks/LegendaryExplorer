using System.Windows.Input;

namespace MassEffect.NativesEditor
{
	public static class CodexMapCommands
	{
		static CodexMapCommands()
		{
			AddCodexEntry = new RoutedUICommand("Add", "AddCodexEntry", typeof(CodexMapCommands));
			AddCodexPage = new RoutedUICommand("Add", "AddCodexPage", typeof(CodexMapCommands));
			AddCodexSection = new RoutedUICommand("Add", "AddCodexSection", typeof(CodexMapCommands));

			RemoveCodexEntry = new RoutedUICommand("Remove", "RemoveCodexEntry", typeof(CodexMapCommands));
			RemoveCodexPage = new RoutedUICommand("Remove", "RemoveCodexPage", typeof(CodexMapCommands));
			RemoveCodexSection = new RoutedUICommand("Remove", "RemoveCodexSection", typeof(CodexMapCommands));
		}

		public static RoutedUICommand AddCodexEntry { get; private set; }
		public static RoutedUICommand AddCodexPage { get; private set; }
		public static RoutedUICommand AddCodexSection { get; private set; }

		public static RoutedUICommand RemoveCodexEntry { get; private set; }
		public static RoutedUICommand RemoveCodexPage { get; private set; }
		public static RoutedUICommand RemoveCodexSection { get; private set; }
	}
}
