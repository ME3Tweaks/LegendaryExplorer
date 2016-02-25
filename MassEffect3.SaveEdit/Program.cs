using System;
using System.Globalization;
using System.Threading;
using System.Windows.Forms;

namespace MassEffect3.SaveEdit
{
	internal static class Program
	{
		/// <summary>
		///     The main entry point for the application.
		/// </summary>
		[STAThread]
		public static void Main(string[] args)
		{
			if (args.Length >= 2 &&
				args[0].ToLowerInvariant() == "-culture")
			{
				Thread.CurrentThread.CurrentCulture =
					CultureInfo.GetCultureInfo(args[1]);
				Thread.CurrentThread.CurrentUICulture =
					CultureInfo.GetCultureInfo(args[1]);
			}

			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			Application.Run(new Editor());
		}
	}
}