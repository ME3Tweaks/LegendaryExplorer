using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ME3Explorer
{
    /// <summary>
    /// Methods called from OnlineContent should be done only from background threads as they will block the current thread.
    /// </summary>
    public static class OnlineContent
    {
        public static string EnsureStaticExecutable(string staticExecutableName, Action<long, long> progressCallback = null, string hash = null)
        {
            string staticExecutable = Path.Combine(App.StaticExecutablesDirectory, staticExecutableName);
            if (!File.Exists(staticExecutable)) //In future we will want to have a way to hash check this or something so we can update this if necessary without user intervention.
            {
                using (var wc = new System.Net.WebClient())
                {
                    string downloadError = null;
                    wc.DownloadProgressChanged += (a, args) =>
                    {
                        progressCallback?.Invoke(args.BytesReceived, args.TotalBytesToReceive);
                    };
                    wc.DownloadFileCompleted += (a, args) =>
                    {
                        lock (args.UserState)
                        {
                            //releases blocked thread
                            Monitor.Pulse(args.UserState);
                        }

                        downloadError = args.Error?.Message;
                        if (downloadError != null) { File.Delete(staticExecutable); }
                    };
                    var fullURL = App.StaticFilesBaseURL + staticExecutableName;



                    var syncObject = new Object();
                    lock (syncObject)
                    {
                        wc.DownloadFileAsync(new Uri(fullURL), staticExecutable, syncObject);
                        //This will block the thread until download completes
                        Monitor.Wait(syncObject);
                    }

                    return downloadError;
                }
            }

            return null; //File exists
        }
    }
}
