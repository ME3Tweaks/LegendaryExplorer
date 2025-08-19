using System.IO;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.UnrealScript.Documentation;

namespace LegendaryExplorer.Misc
{
    public static class LEXDocuDB
    {

        /// <summary>
        /// LEX convenience wrapper for loading/fetching binary DB
        /// </summary>
        /// <param name="game"></param>
        /// <returns></returns>
        public static DocuDB LoadDocuDB(MEGame game)
        {
            var loadedDb = DocuDB.GetLoadedDB(game);
            if (loadedDb != null) return loadedDb;

            // Right now we only support this on Debug builds
#if DEBUG
            switch (game)
            {
                case MEGame.LE1:
                case MEGame.LE2:
                case MEGame.LE3:
                    var docuDbPath = Path.Combine(AppDirectories.ExecFolder, "BinaryDocuDB", $"DocuDB_{game}.bin");
                    if (File.Exists(docuDbPath))
                    {
                        loadedDb = BinaryDocuDB.Deserialize(docuDbPath);
                        DocuDB.AddLoadedDb(game, loadedDb);
                        return loadedDb;
                    }
                    else
                    {
                        // This prevents File.Exists() calls since the db file doesn't exist in exec.
                        var emptyDb = DocuDB.GetEmptyDB(game);
                        DocuDB.AddLoadedDb(game, emptyDb);
                    }
                    break;
                default:
                    {
                        // This prevents File.Exists() calls since the db file doesn't exist in exec.
                        var emptyDb = DocuDB.GetEmptyDB(game); ;
                        DocuDB.AddLoadedDb(game, emptyDb);
                        return emptyDb;
                    }
            }
#endif

            return null;
        }

        public static string GetDocumentationForClassMember(MEGame game, string className, NameReference propertyName)
        {
            var db = LoadDocuDB(game);
            if (db?.ClassDocumentation != null && db.ClassDocumentation.TryGetValue(className, out var cls) && cls.Variables.TryGetValue(propertyName.Instanced, out var member))
            {
                return member.MemberDocumentation;
            }

            return null;
        }
    }
}
