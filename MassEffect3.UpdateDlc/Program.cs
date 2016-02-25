using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace MassEffect3.UpdateDlc
{
	public static class Program
	{
		private static string GetExecutableName()
		{
			return Path.GetFileName(Assembly.GetExecutingAssembly().Location);
		}

		public static void Main(string[] args)
		{
			/*Mode[] mode =
			{
				Mode.Unknown
			};

			var showHelp = false;

			var options = new OptionSet
			{
				{
					"b|xml2bin",
					"convert xml to bin",
					v => mode[0] = v != null ? Mode.XmlToBin : mode[0]
				},
				{
					"x|bin2xml",
					"convert bin to xml",
					v => mode[0] = v != null ? Mode.BinToXml : mode[0]
				},
				{
					"h|help",
					"show this message and exit",
					v => showHelp = v != null
				}
			};

			List<string> extras;

			try
			{
				extras = options.Parse(args);
			}
			catch (OptionException e)
			{
				Console.Write("{0}: ", GetExecutableName());
				Console.WriteLine(e.Message);
				Console.WriteLine("Try `{0} --help' for more information.", GetExecutableName());
				
				return;
			}

			if (mode[0] == Mode.Unknown && extras.Count >= 1)
			{
				var testPath = extras[0];

				if (Directory.Exists(testPath))
				{
					mode[0] = Mode.XmlToBin;
				}
				else if (File.Exists(testPath))
				{
					mode[0] = Mode.BinToXml;
				}
			}

			if (extras.Count < 1 || extras.Count > 2 || showHelp || mode[0] == Mode.Unknown)
			{
				Console.WriteLine("Usage: {0} [OPTIONS]+ -x input_bin [output_dir]", GetExecutableName());
				Console.WriteLine("       {0} [OPTIONS]+ -b input_dir [output_bin]", GetExecutableName());
				Console.WriteLine();
				Console.WriteLine("Options:");
				options.WriteOptionDescriptions(Console.Out);

				return;
			}*/

			//
			//var dlc = new DlcPackage(@"C:\Program Files (x86)\Origin Games\Mass Effect 3\BioGame\Patches\PCConsole\Patch_001.sfar");
			//Console.WriteLine(dlc.DlcFiles.Length);
			
			//GenerateOriginalWriteTimes();
			FixDlcWriteTimes();

			//Console.ReadKey();
		}

		private static void GenerateOriginalWriteTimes()
		{
			var info = new DirectoryInfo(Path.Combine(Paths.OriginGames.Root, "Mass Effect 3 Backup"));

			var xDoc = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), new XElement("MassEffect3", CreateDirectoryXml(info)));

			var settings = new XmlWriterSettings
			{
				Indent = true,
				IndentChars = "\t"
			};

			using (var writer = XmlWriter.Create("OriginalFileData.xml", settings))
			{
				xDoc.Save(writer);
			}
		}

		private static XElement CreateDirectoryXml(DirectoryInfo dirInfo)
		{
			var xmlInfo = new XElement("Directory", new XAttribute("name", dirInfo.Name));

			// Subdirectories
			var directories = dirInfo.EnumerateDirectories().OrderBy(d => d.Name);

			foreach (var dir in directories)
			{
				xmlInfo.Add(CreateDirectoryXml(dir));
			}

			// Files
			var files = dirInfo.EnumerateFiles().OrderBy(f => f.Name);

			foreach (var file in files)
			{
				const string fileTimeFormat = "MM/dd/yyyy HH:mm:ss";

				var lastWriteTimeUtc = file.LastWriteTimeUtc;
				var lastWriteTimeUtcString = lastWriteTimeUtc.ToString(fileTimeFormat, CultureInfo.InvariantCulture);

				xmlInfo.Add(new XElement("File", 
					new XAttribute("name", file.Name), 
					new XAttribute("lastModified", lastWriteTimeUtc.ToFileTimeUtc()),
					new XAttribute("size", file.Length)
					)
				);
			}

			return xmlInfo;
		}

		private static void FixDlcWriteTimes(bool patch105 = true)
		{
			var dlcPaths = new Dictionary<string, string>();
			var dlcWriteTimes = new Dictionary<string, DateTime>();
			var provider = CultureInfo.InvariantCulture;
			const string dateFormat = "MM/dd/yyyy HH:mm:ss";

			dlcPaths.Add("ConApp01", Paths.MassEffect3.BioGame.Dlc.ConApp01.Root);
			dlcPaths.Add("ConDH1", Paths.MassEffect3.BioGame.Dlc.ConDH1.Root);
			dlcPaths.Add("ConEnd", Paths.MassEffect3.BioGame.Dlc.ConEnd.Root);
			dlcPaths.Add("ConGun01", Paths.MassEffect3.BioGame.Dlc.ConGun01.Root);
			dlcPaths.Add("ConGun02", Paths.MassEffect3.BioGame.Dlc.ConGun02.Root);
			dlcPaths.Add("ConMP1", Paths.MassEffect3.BioGame.Dlc.ConMP1.Root);
			dlcPaths.Add("ConMP2", Paths.MassEffect3.BioGame.Dlc.ConMP2.Root);
			dlcPaths.Add("ConMP3", Paths.MassEffect3.BioGame.Dlc.ConMP3.Root);
			dlcPaths.Add("ConMP4", Paths.MassEffect3.BioGame.Dlc.ConMP4.Root);
			dlcPaths.Add("ConMP5", Paths.MassEffect3.BioGame.Dlc.ConMP5.Root);
			dlcPaths.Add("ExpPack001", Paths.MassEffect3.BioGame.Dlc.ExpPack001.Root);
			dlcPaths.Add("ExpPack002", Paths.MassEffect3.BioGame.Dlc.ExpPack002.Root);
			dlcPaths.Add("ExpPack003", Paths.MassEffect3.BioGame.Dlc.ExpPack003.Root);
			dlcPaths.Add("ExpPack003Base", Paths.MassEffect3.BioGame.Dlc.ExpPack003Base.Root);
			dlcPaths.Add("HenPr", Paths.MassEffect3.BioGame.Dlc.HenPr.Root);
			dlcPaths.Add("OnlinePassHidCE", Paths.MassEffect3.BioGame.Dlc.OnlinePassHidCE.Root);
			dlcPaths.Add("Patch104", Paths.MassEffect3.BioGame.Patches.PCConsole.Root);
			dlcPaths.Add("Patch105", Paths.MassEffect3.BioGame.Patches.PCConsole.Root);
			dlcPaths.Add("UpdPatch01", Paths.MassEffect3.BioGame.Dlc.UpdPatch01.Root);
			dlcPaths.Add("UpdPatch02", Paths.MassEffect3.BioGame.Dlc.UpdPatch02.Root);

			dlcWriteTimes.Add("ConApp01", DateTime.FromFileTimeUtc(129953680060000000));
			dlcWriteTimes.Add("ConDH1", DateTime.FromFileTimeUtc(130066283520000000));
			dlcWriteTimes.Add("ConEnd", DateTime.FromFileTimeUtc(129845323720000000));
			dlcWriteTimes.Add("ConGun01", DateTime.FromFileTimeUtc(129860896960000000));
			dlcWriteTimes.Add("ConGun02", DateTime.FromFileTimeUtc(129923340440000000));
			dlcWriteTimes.Add("ConMP1", DateTime.FromFileTimeUtc(129776061860000000));
			dlcWriteTimes.Add("ConMP2", DateTime.FromFileTimeUtc(129799429540000000));
			dlcWriteTimes.Add("ConMP3", DateTime.FromFileTimeUtc(129842291440000000));
			dlcWriteTimes.Add("ConMP4", DateTime.FromFileTimeUtc(129913768880000000));
			dlcWriteTimes.Add("ConMP5", DateTime.FromFileTimeUtc(130047743700000000));
			dlcWriteTimes.Add("ExpPack001", DateTime.FromFileTimeUtc(129883119320000000));
			dlcWriteTimes.Add("ExpPack002", DateTime.FromFileTimeUtc(129951540280000000));
			dlcWriteTimes.Add("ExpPack003", DateTime.FromFileTimeUtc(130046087960000000));
			dlcWriteTimes.Add("ExpPack003Base", DateTime.FromFileTimeUtc(130046093720000000));
			dlcWriteTimes.Add("HenPr", DateTime.FromFileTimeUtc(129727546400000000));
			dlcWriteTimes.Add("OnlinePassHidCE", DateTime.FromFileTimeUtc(129725113860000000));
			dlcWriteTimes.Add("Patch104", DateTime.FromFileTimeUtc(129918672740000000));
			dlcWriteTimes.Add("Patch105", DateTime.FromFileTimeUtc(130005974040000000));
			dlcWriteTimes.Add("UpdPatch01", DateTime.FromFileTimeUtc(129924598040000000));
			dlcWriteTimes.Add("UpdPatch02", DateTime.FromFileTimeUtc(130023194640000000));

			//var txtBuilder = new StringBuilder();

			foreach (var dlcPath in dlcPaths)
			{
				string sfar;

				if (patch105)
				{
					if (dlcPath.Key == "Patch104")
					{
						continue;
					}
				}
				else
				{
					if (dlcPath.Key == "Patch105")
					{
						continue;
					}
				}

				switch (dlcPath.Key)
				{
					case "Patch104":
					case "Patch105":
					{
						sfar = Path.Combine(dlcPath.Value, "Patch_001.sfar");
						
						break;
					}
					default:
					{
						sfar = Path.Combine(dlcPath.Value, "CookedPCConsole/Default.sfar");

						break;
					}
				}

				//var sfarBackup = Path.Combine(dlcPath.Value, dlcPath.Key != "Patch001" ? "CookedPCConsole/Backup/Default.sfar" : "Backup/Patch_001.sfar");
				//var sfarDateTime = new DateTime();
				//var sfarBackupDateTime = new DateTime();

				if (File.Exists(sfar))
				{
					//var sfarDateTime = File.GetLastWriteTime(sfar);

					Console.WriteLine("{0} => {1}", dlcPath.Key, dlcWriteTimes[dlcPath.Key]);
					File.SetLastWriteTime(sfar, dlcWriteTimes[dlcPath.Key]);
				}

				/*if (File.Exists(sfarBackup))
				{
					txtBuilder.AppendLine();

					sfarBackupDateTime = File.GetLastWriteTimeUtc(sfarBackup);
					Console.WriteLine("Backup_{0}: {1}", dlcPath.Key, sfarBackupDateTime.ToString(CultureInfo.InvariantCulture));

					txtBuilder.AppendLine(string.Format("Backup_{0}: {1}", dlcPath.Key, sfarBackupDateTime.ToString(CultureInfo.InvariantCulture)));
					txtBuilder.AppendLine(sfarBackupDateTime.ToBinary().ToString(CultureInfo.InvariantCulture));
					txtBuilder.AppendLine(sfarBackupDateTime.ToFileTime().ToString(CultureInfo.InvariantCulture));
				}

				if (File.Exists(sfar) && File.Exists(sfarBackup))
				{
					File.SetLastWriteTime(sfar, sfarBackupDateTime);
				}*/

				//Console.WriteLine();
				//txtBuilder.AppendLine();
			}
		}
	}
}
