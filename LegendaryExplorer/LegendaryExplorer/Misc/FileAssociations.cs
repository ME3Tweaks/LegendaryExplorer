using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace LegendaryExplorer.Misc
{
    public class FileAssociation
    {
        public string Extension { get; set; }
        public string ProgId { get; set; }
        public string FileTypeDescription { get; set; }
        public string ExecutableFilePath { get; set; }
    }

    public class FileAssociations
    {
        // needed so that Explorer windows get refreshed after the registry is updated
        [System.Runtime.InteropServices.DllImport("Shell32.dll")]
        private static extern int SHChangeNotify(int eventId, int flags, IntPtr item1, IntPtr item2);

        private const int SHCNE_ASSOCCHANGED = 0x8000000;
        private const int SHCNF_FLUSH = 0x1000;

        /// <summary>
        /// Sets a file association. 
        /// </summary>
        /// <param name="extension">File extension to set. Must not contain a .</param>
        /// <param name="filetypeDescription">Description to show in WE details for "Type"</param>
        public static void EnsureAssociationsSet(string extension, string filetypeDescription)
        {
            var filePath = Process.GetCurrentProcess().MainModule.FileName;
            EnsureAssociationsSet(
                new FileAssociation
                {
                    Extension = "." + extension,
                    ProgId = "ME3ExplorerMTF." + extension,
                    FileTypeDescription = filetypeDescription,
                    ExecutableFilePath = filePath
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
                    association.ExecutableFilePath);
            }

            if (madeChanges)
            {
                SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_FLUSH, IntPtr.Zero, IntPtr.Zero);
            }
        }

        public static bool SetAssociation(string extension, string progId, string fileTypeDescription, string applicationFilePath)
        {
            bool madeChanges = false;
            madeChanges |= SetKeyDefaultValue(@"Software\Classes\" + extension, progId);
            madeChanges |= SetKeyDefaultValue(@"Software\Classes\" + progId, fileTypeDescription);
            madeChanges |= SetKeyDefaultValue($@"Software\Classes\{progId}\shell\open\command", "\"" + applicationFilePath + "\" -o \"%1\"");
            return madeChanges;
        }

        private static bool SetKeyDefaultValue(string keyPath, string value)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(keyPath))
            {
                if (key.GetValue(null) as string != value)
                {
                    key.SetValue(null, value);
                    return true;
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
            EnsureAssociationsSet("tlk", "Talk Table File");
            EnsureAssociationsSet("afc", "Audio File Cache File");
            EnsureAssociationsSet("isb", "ISACT Bank File");
            EnsureAssociationsSet("dlc", "Mass Effect DLC Mount File");
            EnsureAssociationsSet("cnd", "Mass Effect Conditionals File");
        }
    }
}