using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using Gammtek.Conduit.CommandLine;
using Gammtek.Conduit.Extensions;
using Gammtek.Conduit.IO;

namespace MassEffect3.TlkEditor
{
	/// <summary>
	///     Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		/*#region Overrides of Application

		protected override void OnStartup(StartupEventArgs e)
		{
			StartupUri = new Uri("", UriKind.Relative);
		}

		#endregion*/

		public static string GetVersion()
		{
			var ver = Assembly.GetExecutingAssembly().GetName().Version;

			return ver.Major + "." + ver.Minor + "." + ver.Build;
		}

		private static string GetExecutableName()
		{
			return Path.GetFileName(Assembly.GetExecutingAssembly().Location);
		}

		private static string GetExePath()
		{
			return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
		}

		private void Application_Startup(object sender, StartupEventArgs e)
		{
			//
			/*var args = 
				new[]
				{
					"BIOGame_INT.tlk",
					/*"BIOGame_INT.tlk",#1#
					"--mode ToTlk",
					"--no-ui",
					//"--debug"
				};*/
			//

			/*var helpWriter = new StringWriter();
			var parser = new Parser(
				settings =>
				{
					settings.EnableDashDash = true;
					settings.HelpWriter = helpWriter;
				});

			var result = parser.ParseArguments<ProgramOptions>(e.Args);
			var options = result.Value;

			if (result.Errors.Any())
			{
				Console.WriteLine(helpWriter.ToString());
				return;
			}

			if (options.ConversionMode == ProgramConversionMode.Unknown && !options.Source.IsNullOrWhiteSpace())
			{
				if (File.Exists(options.Source))
				{
					if (options.Source.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
					{
						options.ConversionMode = ProgramConversionMode.ToTlk;
					}
					else if (options.Source.EndsWith(".tlk", StringComparison.OrdinalIgnoreCase))
					{
						options.ConversionMode = ProgramConversionMode.ToXml;
					}
				}
			}

			var sourcePath = options.Source;
			var destinationPath = options.Destination;

			if (options.NoUI && !sourcePath.IsNullOrWhiteSpace())
			{
				if (!Path.IsPathRooted(sourcePath))
				{
					sourcePath = Path.Combine(GetExePath(), sourcePath);
				}

				if (destinationPath.IsNullOrWhiteSpace())
				{
					destinationPath = Path.ChangeExtension(sourcePath, options.ConversionMode == ProgramConversionMode.ToTlk ? "tlk" : "xml");
				}

				if (!Path.IsPathRooted(destinationPath))
				{
					destinationPath = Path.Combine(GetExePath(), destinationPath);
				}

				if (!File.Exists(sourcePath))
				{
					return;
				}

				if (!Directory.Exists(Path.GetDirectoryName(destinationPath) ?? destinationPath))
				{
					Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? destinationPath);
				}

				if (options.ConversionMode == ProgramConversionMode.ToTlk)
				{
					var hc = new HuffmanCompression();
					hc.LoadInputData(sourcePath, TalkFile.Fileformat.Xml, options.IsDebugMode);

					hc.SaveToTlkFile(destinationPath);
				}
				else if (options.ConversionMode == ProgramConversionMode.ToXml)
				{
					var tf = new TalkFile();
					tf.LoadTlkData(sourcePath);

					tf.DumpToFile(destinationPath, TalkFile.Fileformat.Xml);
				}

				Current.Shutdown();
				//return;
			}

			var mainWindow = new MainWindow();
			mainWindow.Show();

			if (sourcePath.IsNullOrWhiteSpace())
			{
				return;
			}

			if (!Path.IsPathRooted(sourcePath))
			{
				sourcePath = Path.Combine(GetExePath(), sourcePath);
			}

			if (destinationPath.IsNullOrWhiteSpace())
			{
				destinationPath = Path.ChangeExtension(sourcePath, options.ConversionMode == ProgramConversionMode.ToTlk ? "tlk" : "xml");
			}

			if (!Path.IsPathRooted(destinationPath))
			{
				destinationPath = Path.Combine(GetExePath(), destinationPath);
			}

			switch (options.ConversionMode)
			{
				case ProgramConversionMode.ToXml:
				{
					mainWindow.InputTlkFilePath = sourcePath;
					mainWindow.OutputTextFilePath = destinationPath;
					mainWindow.mainTabControl.SelectedIndex = 0;

					break;
				}
				case ProgramConversionMode.ToTlk:
				{
					mainWindow.InputXmlFilePath = sourcePath;
					mainWindow.OutputTlkFilePath = destinationPath;
					mainWindow.mainTabControl.SelectedIndex = 1;
					mainWindow.DebugCheckBox.IsChecked = options.IsDebugMode;

					break;
				}
			}*/
		}
	}
}
