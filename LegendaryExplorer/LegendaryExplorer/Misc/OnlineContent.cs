using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading;

namespace LegendaryExplorer.Misc
{
    /// <summary>
    /// Methods called from OnlineContent should be done only from background threads as they will block the current thread.
    /// </summary>
    public static class OnlineContent
    {
        public static string EnsureStaticExecutable(string staticExecutableName, Action<long, long> progressCallback = null, string hash = null)
        {
            string staticExecutable = Path.Combine(AppDirectories.StaticExecutablesDirectory, staticExecutableName);
            if (!File.Exists(staticExecutable)) //In future we will want to have a way to hash check this or something so we can update this if necessary without user intervention.
            {
                using var wc = new System.Net.WebClient();
                string downloadError = null;
                wc.DownloadProgressChanged += (a, args) =>
                {
                    progressCallback?.Invoke(args.BytesReceived, args.TotalBytesToReceive);
                };
                wc.DownloadFileCompleted += (a, args) =>
                {
                    downloadError = args.Error?.Message;
                    if (downloadError != null) { File.Delete(staticExecutable); }
                    lock (args.UserState)
                    {
                        //releases blocked thread
                        Monitor.Pulse(args.UserState);
                    }
                };
                var fullURL = AppDirectories.StaticFilesBaseURL + staticExecutableName;



                var syncObject = new object();
                lock (syncObject)
                {
                    wc.DownloadFileAsync(new Uri(fullURL), staticExecutable, syncObject);
                    //This will block the thread until download completes
                    Monitor.Wait(syncObject);
                }

                return downloadError;
            }

            return null; //File exists
        }

        public static string EnsureStaticZippedExecutable(string staticZipName, string foldername, string executablename, Action<long, long> progressCallback = null, string hash = null, bool forceDownload = false)
        {
            string staticExecutable = Path.Combine(AppDirectories.StaticExecutablesDirectory, foldername, executablename);
            if (forceDownload || !File.Exists(staticExecutable)) //In future we will want to have a way to hash check this or something so we can update this if necessary without user intervention.
            {
                using var wc = new System.Net.WebClient();
                string downloadError = null;
                wc.DownloadProgressChanged += (a, args) =>
                {
                    progressCallback?.Invoke(args.BytesReceived, args.TotalBytesToReceive);
                };
                wc.DownloadDataCompleted += (a, args) =>
                {
                    downloadError = args.Error?.Message;
                    if (downloadError != null)
                    {
                        if (File.Exists(staticExecutable)) 
                            File.Delete(staticExecutable);
                    }
                    else
                    {
                        var za = new ZipArchive(new MemoryStream(args.Result));
                        var outputdir = Directory.CreateDirectory(Path.Combine(AppDirectories.StaticExecutablesDirectory, foldername)).FullName;
                        za.ExtractToDirectory(outputdir, true);
                    }
                    lock (args.UserState)
                    {
                        //releases blocked thread
                        Monitor.Pulse(args.UserState);
                    }
                };
                var fullURL = AppDirectories.StaticFilesBaseURL + staticZipName;
                var syncObject = new object();
                lock (syncObject)
                {
                    Debug.WriteLine("Fetching zip via " + fullURL);
                    wc.DownloadDataAsync(new Uri(fullURL), syncObject);
                    //This will block the thread until download completes
                    Monitor.Wait(syncObject);
                }

                return downloadError;
            }

            return null; //File exists
        }
    }
}
