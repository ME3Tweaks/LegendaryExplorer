using System;
using System.Diagnostics;
using LegendaryExplorer.Libraries;
using Microsoft.Win32;

namespace LegendaryExplorer.Misc
{
    public class FileAssociation
    {
        public string Extension { get; set; }
        public string ProgId { get; set; }
        public string FileTypeDescription { get; set; }
        public string ExecutableFilePath { get; set; }
        public int IconIndex { get; set; } = -1;
    }

    public class FileAssociations
    {
        private const int SHCNE_ASSOCCHANGED = 0x8000000;
        private const int SHCNF_FLUSH = 0x1000;

        /// <summary>
        /// Sets a file association. 
        /// </summary>
        /// <param name="extension">File extension to set. Must not contain a .</param>
        /// <param name="filetypeDescription">Description to show in WE details for "Type"</param>
        public static void EnsureAssociationsSet(string extension, string filetypeDescription, int iconIndex = -1)
        {
            var filePath = Process.GetCurrentProcess().MainModule.FileName;
            EnsureAssociationsSet(
                new FileAssociation
                {
                    Extension = "." + extension,
                    ProgId = "LegendaryExplorerMTF." + extension,
                    FileTypeDescription = filetypeDescription,
                    ExecutableFilePath = filePath,
                    IconIndex = iconIndex
                });
        }

        public static void EnsureAssociationsSet(params FileAssociation[] associations)
        {
            bool madeChanges = false;
            foreach (var association in associations)
            {
                madeChanges |= SetAssociation(
                    association.Extension,
                    association.ProgId,
                    association.FileTypeDescription,
                    association.ExecutableFilePath,
                    association.IconIndex);
            }

            if (madeChanges)
            {
                // needed so that Explorer windows get refreshed after the registry is updated
                WindowsAPI.SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_FLUSH, IntPtr.Zero, IntPtr.Zero);
            }
        }

        public static bool SetAssociation(string extension, string progId, string fileTypeDescription, string applicationFilePath, int iconIndex = -1)
        {
            bool madeChanges = false;
            madeChanges |= SetKeyDefaultValue(@"Software\Classes\" + extension, progId);
            madeChanges |= SetKeyDefaultValue(@"Software\Classes\" + progId, fileTypeDescription);
            madeChanges |= SetKeyDefaultValue($@"Software\Classes\{progId}\shell\open\command", "\"" + applicationFilePath + "\" -o \"%1\"");
            if (iconIndex >= 0)
            {
                madeChanges |= SetKeyDefaultValue($@"Software\Classes\{progId}\DefaultIcon", $"{applicationFilePath},{iconIndex}");
            }
            return madeChanges;
        }

        private static bool SetKeyDefaultValue(string keyPath, string value, RegistryValueKind? kind = null)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(keyPath))
            {
                if (kind != null)
                {
                    key.SetValue(null, value, kind.Value);
                    return true;
                }
                else
                {
                    if (key.GetValue(null) as string != value)
                    {
                        key.SetValue(null, value);
                        return true;
                    }
                }
            }

            return false;
        }

        public static void AssociatePCCSFM()
        {
            EnsureAssociationsSet("pcc", "Mass Effect Series Package File");
            EnsureAssociationsSet("sfm", "Mass Effect 1 Package File");
        }

        public static void AssociateUPKUDK()
        {
            EnsureAssociationsSet("upk", "Unreal Package File");
            EnsureAssociationsSet("udk", "UDK Package File");
            EnsureAssociationsSet("u", "Mass Effect 1 Package File");
        }

        public static void AssociateOthers()
        {
            // Icons start at 40001 in the exe - but are defined as 1 2 3 4... (0 being default icon) and are defined in iconlist.txt in the build folder.
            // DO NOT CHANGE THE ORDER WITHOUT CHANGING THESE
            EnsureAssociationsSet("tlk", "Talk Table File", 4);
            EnsureAssociationsSet("afc", "Audio File Cache File", 2);
            EnsureAssociationsSet("isb", "ISACT Bank File", 2);
            EnsureAssociationsSet("dlc", "Mass Effect DLC Mount File", 1);
            EnsureAssociationsSet("cnd", "Mass Effect Conditionals File", 4);
        }
    }
}