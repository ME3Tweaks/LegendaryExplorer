using System;
using System.Collections.Generic;
using System.IO;
using ME3Explorer.Packages;
using ME3Explorer.Unreal;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ME3Explorer.Pathfinding_Editor
{
    class SharedPathfinding
    {
        public static Dictionary<string, Dictionary<string, string>> ImportClassDB = new Dictionary<string, Dictionary<string, string>>(); //SFXGame.Default__SFXEnemySpawnPoint -> class, packagefile (can infer link and name)
        public static Dictionary<string, Dictionary<string, string>> ExportClassDB = new Dictionary<string, Dictionary<string, string>>(); //SFXEnemy SpawnPoint -> class, name, ...etc
        private static bool ClassesDBLoaded;
        private static string ClassesDatabasePath = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "exec", "pathfindingclassdb.json");

        public static void GenerateNewRandomGUID(IExportEntry export)
        {
            StructProperty guidProp = export.GetProperty<StructProperty>("NavGuid");
            if (guidProp != null)
            {
                Random rnd = new Random();
                IntProperty A = guidProp.GetProp<IntProperty>("A");
                IntProperty B = guidProp.GetProp<IntProperty>("B");
                IntProperty C = guidProp.GetProp<IntProperty>("C");
                IntProperty D = guidProp.GetProp<IntProperty>("D");
                byte[] data = export.Data;

                WriteMem(data, (int)A.ValueOffset, BitConverter.GetBytes(rnd.Next()));
                WriteMem(data, (int)B.ValueOffset, BitConverter.GetBytes(rnd.Next()));
                WriteMem(data, (int)C.ValueOffset, BitConverter.GetBytes(rnd.Next()));
                WriteMem(data, (int)D.ValueOffset, BitConverter.GetBytes(rnd.Next()));
                export.Data = data;
            }
        }

        /// <summary>
        /// Writes the buffer to the memory array starting at position pos
        /// </summary>
        /// <param name="memory">Memory array to overwrite onto</param>
        /// <param name="pos">Position to start writing at</param>
        /// <param name="buff">byte array to write, in order</param>
        /// <returns>Modified memory</returns>
        public static byte[] WriteMem(byte[] memory, int pos, byte[] buff)
        {
            for (int i = 0; i < buff.Length; i++)
                memory[pos + i] = buff[i];

            return memory;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="export"></param>
        /// <returns></returns>
        public static string GetReachSpecEndName(IExportEntry export)
        {
            return export.FileRef.Game != MEGame.ME1 ? "Actor" : "Nav";
        }

        /// <summary>
        /// Fetches the NavGuid object as a UnrealGUID
        /// </summary>
        /// <param name="export"></param>
        /// <returns></returns>
        public static UnrealGUID GetNavGUID(IExportEntry export)
        {
            StructProperty navGuid = export.GetProperty<StructProperty>("NavGuid");
            if (navGuid != null)
            {
                UnrealGUID guid = GetGUIDFromStruct(navGuid);
                guid.export = export;
                return guid;
            }

            return null;
        }

        /// <summary>
        /// Fetches an UnrealGUID object from a GUID struct.
        /// </summary>
        /// <param name="guidStruct"></param>
        /// <returns></returns>
        public static UnrealGUID GetGUIDFromStruct(StructProperty guidStruct)
        {
            int a = guidStruct.GetProp<IntProperty>("A");
            int b = guidStruct.GetProp<IntProperty>("B");
            int c = guidStruct.GetProp<IntProperty>("C");
            int d = guidStruct.GetProp<IntProperty>("D");
            UnrealGUID guid = new UnrealGUID();
            guid.A = a;
            guid.B = b;
            guid.C = c;
            guid.D = d;
            return guid;
        }


        /// <summary>
        /// Reindexes all objects in this pcc that have the same full path.
        /// USE WITH CAUTION!
        /// </summary>
        /// <param name="exportToReindex">Export that contains the path you want to reindex</param>
        public static void ReindexMatchingObjects(IExportEntry exportToReindex)
        {
            string fullpath = exportToReindex.GetFullPath;
            int index = 1; //we'll start at 1.
            foreach (IExportEntry export in exportToReindex.FileRef.Exports)
            {
                if (fullpath == export.GetFullPath && export.ClassName != "Class")
                {
                    export.indexValue = index;
                    index++;
                }
            }
        }

        internal static void LoadClassesDB()
        {
            if (!ClassesDBLoaded)
            {
                if (File.Exists(ClassesDatabasePath))
                {
                    string raw = File.ReadAllText(ClassesDatabasePath);
                    JObject o = JObject.Parse(raw);
                    JToken exportjson = o.SelectToken("exporttypes");
                    JToken importjson = o.SelectToken("importtypes");
                    ExportClassDB = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(exportjson.ToString());
                    ImportClassDB = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(importjson.ToString());
                    ClassesDBLoaded = true;
                }
            }
        }
    }

    public class UnrealGUID
    {
        public int A, B, C, D, levelListIndex;
        public IExportEntry export;

        public override bool Equals(Object obj)
        {
            if (obj == null || GetType() != obj.GetType())
                return false;
            UnrealGUID other = (UnrealGUID)obj;
            return other.A == A && other.B == B && other.C == C && other.D == D;
        }

        //public override int GetHashCode()
        //{
        //    return x ^ y;
        //}

        public override string ToString()
        {
            return A + " " + B + " " + C + " " + D;
        }
    }


}
