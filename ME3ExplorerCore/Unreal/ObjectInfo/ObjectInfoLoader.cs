using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.Packages;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace ME3ExplorerCore.Unreal.ObjectInfo
{
    public static class ObjectInfoLoader
    {
        public static string LoadEmbeddedJSONText(MEGame game)
        {
            try
            {
                var infosStream = Utilities.LoadEmbeddedFile("Infos.zip");
                if (infosStream != null)
                {
                    var decompressedStream = Utilities.LoadFileFromZipStream(infosStream, $"{game}ObjectInfo.json");
                    using (StreamReader reader = new StreamReader(decompressedStream))
                    {
                        return reader.ReadToEnd();
                    }
                }


            }
            catch (Exception e)
            {
                Debug.WriteLine($"Failed to load embedded json file for {game}: {e.Message}");
            }

            return null;
        }
    }
}
