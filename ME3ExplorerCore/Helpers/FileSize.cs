using System;
// Taken from ALOT Installer v4

namespace ME3ExplorerCore.Helpers
{
    /// <summary>
    /// Class used to assist in filesize calculations and display
    /// </summary>
    public static class FileSize
    {
        // Load all suffixes in an array  
        static readonly string[] suffixes =
            {" bytes", "KB", "MB", "GB", "TB", "PB"};

        public static string FormatSize(long bytes)
        {
            if (bytes < 0) throw new Exception("Size of bytes to format can't be less than 0.");
            if (bytes < 1024) return $"{bytes} bytes";
            int counter = 0;
            decimal number = bytes;
            while (Math.Round(number / 1024) >= 1)
            {
                number = number / 1024;
                counter++;
            }

            return $"{number:n1}{suffixes[counter]}";
        }

        public static string FormatSize(ulong bytes)
        {
            if (bytes < 1024) return $"{bytes} bytes";
            int counter = 0;
            decimal number = bytes;
            while (Math.Round(number / 1024) >= 1)
            {
                number = number / 1024;
                counter++;
            }

            return $"{number:n1}{suffixes[counter]}";
        }

        /// <summary>
        /// 1000 bytes
        /// </summary>
        public static long KiloByte => 1000;
        /// <summary>
        /// 1024 bytes
        /// </summary>
        public static long KibiBytes => 2 ^ 10;
        /// <summary>
        /// 1000 Kilobytes
        /// </summary>
        public static long MegaByte => 1000 * 1000;
        /// <summary>
        /// 1024 Kibibytes
        /// </summary>
        public static long MebiByte => 1000 * 1000;
        /// <summary>
        /// 1000 Megabytes
        /// </summary>
        public static long GigaByte => MegaByte * 1000;
        /// <summary>
        /// 1024 Mebibytes
        /// </summary>
        public static long GibiByte => MebiByte ^ 10;
        /// <summary>
        /// 1000 Gigabytes
        /// </summary>
        public static long TeraByte => GigaByte * 1000;
        /// <summary>
        /// 1024 Gibibytes
        /// </summary>
        public static long TebiByte => TebiByte ^ 10;
    }


}