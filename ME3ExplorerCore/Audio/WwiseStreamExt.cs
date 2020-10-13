using System.IO;
using ME3ExplorerCore.Helpers;

// Do not change namespace, its partial class
namespace ME3ExplorerCore.Unreal.BinaryConverters
{
    public partial class WwiseStream
    {
        // Non binary addition to class
        public bool IsPCCStored => Filename == null;

        public string GetPathToAFC()
        {
            //Check if pcc-stored
            if (IsPCCStored)
            {
                return null; //it's pcc stored. we will return null for this case since we already coded for "".
            }

            //Look in currect directory first


            string path = Path.Combine(Path.GetDirectoryName(Export.FileRef.FilePath), Filename + ".afc");
            if (File.Exists(path))
            {
                return path; //in current directory of this pcc file
            }

            var gameFiles = MELoadedFiles.GetFilesLoadedInGame(Export.FileRef.Game, includeAFCs: true);
            gameFiles.TryGetValue(Filename + ".afc", out string afcPath);
            return afcPath ?? "";
        }
    }
}
