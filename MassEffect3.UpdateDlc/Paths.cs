using System;
using System.IO;

namespace MassEffect3.UpdateDlc
{
	public static class Paths
	{
		static Paths()
		{
			ProgramFiles = Environment.GetFolderPath(Environment.Is64BitOperatingSystem
				? Environment.SpecialFolder.ProgramFilesX86
				: Environment.SpecialFolder.ProgramFiles);
		}

		public static string ProgramFiles { get; private set; }

		// ReSharper disable InconsistentNaming

		public static class MassEffect3
		{
			/*public static string BinariesRoot
			{
				get { return Path.GetFullPath(string.Format("{0}/Binaries", Root)); }
			}*/

			public static string Root
			{
				get { return Path.GetFullPath(string.Format("{0}/Mass Effect 3", OriginGames.Root)); }
			}

			public static class BioGame
			{
				public static string Root
				{
					get { return Path.GetFullPath(string.Format("{0}/BioGame", MassEffect3.Root)); }
				}

				public static class CookedPCConsole
				{
					public static string Root
					{
						get { return Path.GetFullPath(string.Format("{0}/CookedPCConsole", BioGame.Root)); }
					}
				}

				public static class Dlc
				{
					public static string Root
					{
						get { return Path.GetFullPath(string.Format("{0}/DLC", BioGame.Root)); }
					}

					public static class ConApp01
					{
						public static string Root
						{
							get { return Path.GetFullPath(string.Format("{0}/DLC_CON_APP01", Dlc.Root)); }
						}

						public static string DefaultSfar
						{
							get { return Path.GetFullPath(string.Format("{0}/CookedPCConsole/Default.sfar", Root)); }
						}
					}

					public static class ConDH1
					{
						public static string Root
						{
							get { return Path.GetFullPath(string.Format("{0}/DLC_CON_DH1", Dlc.Root)); }
						}

						public static string DefaultSfar
						{
							get { return Path.GetFullPath(string.Format("{0}/CookedPCConsole/Default.sfar", Root)); }
						}
					}

					public static class ConEnd
					{
						public static string Root
						{
							get { return Path.GetFullPath(string.Format("{0}/DLC_CON_END", Dlc.Root)); }
						}

						public static string DefaultSfar
						{
							get { return Path.GetFullPath(string.Format("{0}/CookedPCConsole/Default.sfar", Root)); }
						}
					}

					public static class ConGun01
					{
						public static string Root
						{
							get { return Path.GetFullPath(string.Format("{0}/DLC_CON_GUN01", Dlc.Root)); }
						}

						public static string DefaultSfar
						{
							get { return Path.GetFullPath(string.Format("{0}/CookedPCConsole/Default.sfar", Root)); }
						}
					}

					public static class ConGun02
					{
						public static string Root
						{
							get { return Path.GetFullPath(string.Format("{0}/DLC_CON_GUN02", Dlc.Root)); }
						}

						public static string DefaultSfar
						{
							get { return Path.GetFullPath(string.Format("{0}/CookedPCConsole/Default.sfar", Root)); }
						}
					}

					public static class ConMP1
					{
						public static string Root
						{
							get { return Path.GetFullPath(string.Format("{0}/DLC_CON_MP1", Dlc.Root)); }
						}

						public static string DefaultSfar
						{
							get { return Path.GetFullPath(string.Format("{0}/CookedPCConsole/Default.sfar", Root)); }
						}
					}

					public static class ConMP2
					{
						public static string Root
						{
							get { return Path.GetFullPath(string.Format("{0}/DLC_CON_MP2", Dlc.Root)); }
						}

						public static string DefaultSfar
						{
							get { return Path.GetFullPath(string.Format("{0}/CookedPCConsole/Default.sfar", Root)); }
						}
					}

					public static class ConMP3
					{
						public static string Root
						{
							get { return Path.GetFullPath(string.Format("{0}/DLC_CON_MP3", Dlc.Root)); }
						}

						public static string DefaultSfar
						{
							get { return Path.GetFullPath(string.Format("{0}/CookedPCConsole/Default.sfar", Root)); }
						}
					}

					public static class ConMP4
					{
						public static string Root
						{
							get { return Path.GetFullPath(string.Format("{0}/DLC_CON_MP4", Dlc.Root)); }
						}

						public static string DefaultSfar
						{
							get { return Path.GetFullPath(string.Format("{0}/CookedPCConsole/Default.sfar", Root)); }
						}
					}

					public static class ConMP5
					{
						public static string Root
						{
							get { return Path.GetFullPath(string.Format("{0}/DLC_CON_MP5", Dlc.Root)); }
						}

						public static string DefaultSfar
						{
							get { return Path.GetFullPath(string.Format("{0}/CookedPCConsole/Default.sfar", Root)); }
						}
					}

					public static class ExpPack001
					{
						public static string Root
						{
							get { return Path.GetFullPath(string.Format("{0}/DLC_EXP_Pack001", Dlc.Root)); }
						}

						public static string DefaultSfar
						{
							get { return Path.GetFullPath(string.Format("{0}/CookedPCConsole/Default.sfar", Root)); }
						}
					}

					public static class ExpPack002
					{
						public static string Root
						{
							get { return Path.GetFullPath(string.Format("{0}/DLC_EXP_Pack002", Dlc.Root)); }
						}

						public static string DefaultSfar
						{
							get { return Path.GetFullPath(string.Format("{0}/CookedPCConsole/Default.sfar", Root)); }
						}
					}

					public static class ExpPack003
					{
						public static string Root
						{
							get { return Path.GetFullPath(string.Format("{0}/DLC_EXP_Pack003", Dlc.Root)); }
						}

						public static string DefaultSfar
						{
							get { return Path.GetFullPath(string.Format("{0}/CookedPCConsole/Default.sfar", Root)); }
						}
					}

					public static class ExpPack003Base
					{
						public static string Root
						{
							get { return Path.GetFullPath(string.Format("{0}/DLC_EXP_Pack003_Base", Dlc.Root)); }
						}

						public static string DefaultSfar
						{
							get { return Path.GetFullPath(string.Format("{0}/CookedPCConsole/Default.sfar", Root)); }
						}
					}

					public static class HenPr
					{
						public static string Root
						{
							get { return Path.GetFullPath(string.Format("{0}/DLC_HEN_PR", Dlc.Root)); }
						}

						public static string DefaultSfar
						{
							get { return Path.GetFullPath(string.Format("{0}/CookedPCConsole/Default.sfar", Root)); }
						}
					}

					public static class OnlinePassHidCE
					{
						public static string Root
						{
							get { return Path.GetFullPath(string.Format("{0}/DLC_OnlinePassHidCE", Dlc.Root)); }
						}

						public static string DefaultSfar
						{
							get { return Path.GetFullPath(string.Format("{0}/CookedPCConsole/Default.sfar", Root)); }
						}
					}

					public static class UpdPatch01
					{
						public static string Root
						{
							get { return Path.GetFullPath(string.Format("{0}/DLC_UPD_Patch01", Dlc.Root)); }
						}

						public static string DefaultSfar
						{
							get { return Path.GetFullPath(string.Format("{0}/CookedPCConsole/Default.sfar", Root)); }
						}
					}

					public static class UpdPatch02
					{
						public static string Root
						{
							get { return Path.GetFullPath(string.Format("{0}/DLC_UPD_Patch02", Dlc.Root)); }
						}
						public static string DefaultSfar
						{
							get { return Path.GetFullPath(string.Format("{0}/CookedPCConsole/Default.sfar", Root)); }
						}
					}
				}

				public static class Patches
				{
					public static string Root
					{
						get { return Path.GetFullPath(string.Format("{0}/Patches", BioGame.Root)); }
					}

					public static class PCConsole
					{
						public static string Root
						{
							get { return Path.GetFullPath(string.Format("{0}/PCConsole", Patches.Root)); }
						}

						public static string Patch001Sfar
						{
							get { return Path.GetFullPath(string.Format("{0}/Patch_001.sfar", Root)); }
						}
					}
				}
			}
		}

		// ReSharper restore InconsistentNaming

		public static class OriginGames
		{
			public static string Root
			{
				get { return Path.GetFullPath(string.Format("{0}/Origin Games", ProgramFiles)); }
			}
		}
	}
}
