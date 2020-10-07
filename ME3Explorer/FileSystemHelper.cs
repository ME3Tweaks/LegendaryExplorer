using System.Diagnostics;
using System.IO;
using System.Threading;

namespace ME3Explorer
{
    public class FileSystemHelper
    {
        public static bool DeleteFilesAndFoldersRecursively(string targetDirectory)
        {
            if (!Directory.Exists(targetDirectory))
            {
                Debug.WriteLine("Directory to delete doesn't exist: " + targetDirectory);
                return true;
            }
            bool result = true;
            foreach (string file in Directory.GetFiles(targetDirectory))
            {
                File.SetAttributes(file, FileAttributes.Normal); //remove read only
                try
                {
                    //Debug.WriteLine("Deleting file: " + file);
                    File.Delete(file);
                }
                catch
                {
                    return false;
                }
            }

            foreach (string subDir in Directory.GetDirectories(targetDirectory))
            {
                result &= DeleteFilesAndFoldersRecursively(subDir);
            }

            Thread.Sleep(10); // This makes the difference between whether it works or not. Sleep(0) is not enough.
            try
            {
                //Debug.WriteLine("Deleting directory: " + targetDirectory);

                Directory.Delete(targetDirectory);
            }
            catch
            {
                return false;
            }
            return result;
        }
    }
}
