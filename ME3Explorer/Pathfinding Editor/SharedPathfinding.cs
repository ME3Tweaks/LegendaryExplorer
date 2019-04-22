using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ME3Explorer.Packages;
using ME3Explorer.Unreal;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ME3Explorer.Pathfinding_Editor
{
    class SharedPathfinding
    {
        //Defaults to empty to prevent issues
        public static Dictionary<string, Dictionary<string, string>> ImportClassDB = new Dictionary<string, Dictionary<string, string>>(); //SFXGame.Default__SFXEnemySpawnPoint -> class, packagefile (can infer link and name)
        public static List<PathfindingDB_ExportType> ExportClassDB = new List<PathfindingDB_ExportType>(); //SFXEnemy SpawnPoint -> class, name, ...etc
        private static bool ClassesDBLoaded;
        internal static string ClassesDatabasePath = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "exec", "pathfindingclassdb.json");

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

        public static void CreateReachSpec(IExportEntry startNode, bool createTwoWay, IExportEntry destinationNode, string reachSpecClass, ReachSpecSize size, UnrealGUID externalGUID = null)
        {
            IMEPackage Pcc = startNode.FileRef;
            IExportEntry reachSpectoClone = Pcc.Exports.FirstOrDefault(x => x.ClassName == "ReachSpec");

            /*if (externalGUID != null) //EXTERNAL
            {
                //external node

                //Debug.WriteLine("Num Exports: " + pcc.Exports.Count);
                int outgoingSpec = pcc.ExportCount;
                int incomingSpec = pcc.ExportCount + 1;


                if (reachSpectoClone != null)
                {
                    pcc.addExport(reachSpectoClone.Clone()); //outgoing

                    IExportEntry outgoingSpecExp = pcc.Exports[outgoingSpec]; //cloned outgoing
                    ImportEntry reachSpecClassImp = getOrAddImport(reachSpecClass); //new class type.

                    outgoingSpecExp.idxClass = reachSpecClassImp.UIndex;
                    outgoingSpecExp.idxObjectName = reachSpecClassImp.idxObjectName;

                    ObjectProperty outgoingSpecStartProp = outgoingSpecExp.GetProperty<ObjectProperty>("Start"); //START
                    StructProperty outgoingEndStructProp = outgoingSpecExp.GetProperty<StructProperty>("End"); //Embeds END
                    ObjectProperty outgoingSpecEndProp = outgoingEndStructProp.Properties.GetProp<ObjectProperty>(SharedPathfinding.GetReachSpecEndName(outgoingSpecExp)); //END
                    outgoingSpecStartProp.Value = startNode.UIndex;
                    outgoingSpecEndProp.Value = 0; //we will have to set the GUID - maybe through form or something


                    //Add to source node prop
                    ArrayProperty<ObjectProperty> PathList = startNode.GetProperty<ArrayProperty<ObjectProperty>>("PathList");
                    byte[] memory = startNode.Data;
                    memory = addObjectArrayLeaf(memory, (int)PathList.ValueOffset, outgoingSpecExp.UIndex);
                    startNode.Data = memory;
                    outgoingSpecExp.WriteProperty(outgoingSpecStartProp);
                    outgoingSpecExp.WriteProperty(outgoingEndStructProp);

                    //Write Spec Size
                    int radVal = -1;
                    int heightVal = -1;

                    System.Drawing.Point sizePair = PathfindingNodeInfoPanel.getDropdownSizePair(size);
                    radVal = sizePair.X;
                    heightVal = sizePair.Y;
                    setReachSpecSize(outgoingSpecExp, radVal, heightVal);

                    //Reindex reachspecs.
                    reindexObjectsWithName(reachSpecClass);
                }
            }
            else
            {*/
            //Debug.WriteLine("Source Node: " + startNode.Index);

            //Debug.WriteLine("Num Exports: " + pcc.Exports.Count);
            //int outgoingSpec = pcc.ExportCount;
            //int incomingSpec = pcc.ExportCount + 1;


            if (reachSpectoClone != null)
            {
                IExportEntry outgoingSpec = reachSpectoClone.Clone();
                Pcc.addExport(outgoingSpec);
                IExportEntry incomingSpec = null;
                if (createTwoWay)
                {
                    incomingSpec = reachSpectoClone.Clone();
                    Pcc.addExport(incomingSpec);
                }

                ImportEntry reachSpecClassImp = GetOrAddImport(Pcc, reachSpecClass); //new class type.

                outgoingSpec.idxClass = reachSpecClassImp.UIndex;
                outgoingSpec.idxObjectName = reachSpecClassImp.idxObjectName;

                var outgoingSpecProperties = outgoingSpec.GetProperties();
                if (reachSpecClass == "Engine.SlotToSlotReachSpec")
                {
                    outgoingSpecProperties.Add(new ByteProperty(1, "SpecDirection")); //We might need to find a way to support this edit
                }

                //Debug.WriteLine("Outgoing UIndex: " + outgoingSpecExp.UIndex);

                ObjectProperty outgoingSpecStartProp = outgoingSpecProperties.GetProp<ObjectProperty>("Start"); //START
                StructProperty outgoingEndStructProp = outgoingSpecProperties.GetProp<StructProperty>("End"); //Embeds END
                ObjectProperty outgoingSpecEndProp = outgoingEndStructProp.Properties.GetProp<ObjectProperty>(SharedPathfinding.GetReachSpecEndName(outgoingSpec)); //END
                outgoingSpecStartProp.Value = startNode.UIndex;
                outgoingSpecEndProp.Value = destinationNode.UIndex;

                //Add to source node prop
                ArrayProperty<ObjectProperty> PathList = startNode.GetProperty<ArrayProperty<ObjectProperty>>("PathList");
                PathList.Add(new ObjectProperty(outgoingSpec.UIndex));
                startNode.WriteProperty(PathList);

                //Write Spec Size
                SetReachSpecSize(Pcc, outgoingSpecProperties, size.SpecRadius, size.SpecHeight);
                outgoingSpec.WriteProperties(outgoingSpecProperties);

                if (createTwoWay)
                {
                    incomingSpec.idxClass = reachSpecClassImp.UIndex;
                    incomingSpec.idxObjectName = reachSpecClassImp.idxObjectName;
                    var incomingSpecProperties = incomingSpec.GetProperties();
                    if (reachSpecClass == "Engine.SlotToSlotReachSpec")
                    {
                        incomingSpecProperties.Add(new ByteProperty(2, "SpecDirection"));
                    }

                    ObjectProperty incomingSpecStartProp = incomingSpecProperties.GetProp<ObjectProperty>("Start"); //START
                    StructProperty incomingEndStructProp = incomingSpecProperties.GetProp<StructProperty>("End"); //Embeds END
                    ObjectProperty incomingSpecEndProp = incomingEndStructProp.Properties.GetProp<ObjectProperty>(SharedPathfinding.GetReachSpecEndName(incomingSpec)); //END

                    incomingSpecStartProp.Value = destinationNode.UIndex;//Uindex
                    incomingSpecEndProp.Value = startNode.UIndex;


                    //Add reachspec to destination node's path list (returning)
                    ArrayProperty<ObjectProperty> DestPathList = destinationNode.GetProperty<ArrayProperty<ObjectProperty>>("PathList");
                    DestPathList.Add(new ObjectProperty(incomingSpec.UIndex));
                    destinationNode.WriteProperty(DestPathList);

                    //destNode.WriteProperty(DestPathList);
                    SetReachSpecSize(Pcc, incomingSpecProperties, size.SpecRadius, size.SpecHeight);

                    incomingSpec.WriteProperties(incomingSpecProperties);
                }

                //Reindex reachspecs.
                SharedPathfinding.ReindexMatchingObjects(outgoingSpec);
            }
        }

        /// <summary>
        /// Modifies the incoming properties collection to update the spec size
        /// </summary>
        /// <param name="specProperties"></param>
        /// <param name="radius"></param>
        /// <param name="height"></param>
        public static void SetReachSpecSize(IMEPackage Pcc, PropertyCollection specProperties, int radius, int height)
        {
            IntProperty radiusProp = specProperties.GetProp<IntProperty>("CollisionRadius");
            IntProperty heightProp = specProperties.GetProp<IntProperty>("CollisionHeight");
            if (radiusProp != null)
            {
                radiusProp.Value = radius;
            }
            if (heightProp != null)
            {
                heightProp.Value = height;
            }
        }

        /// <summary>
        /// Sets the reach spec size and commits the results back to the export
        /// </summary>
        /// <param name="spec"></param>
        /// <param name="radius"></param>
        /// <param name="height"></param>
        public static void SetReachSpecSize(IExportEntry spec, int radius, int height)
        {
            PropertyCollection specProperties = spec.GetProperties();
            SetReachSpecSize(spec.FileRef, specProperties, radius, height);
            spec.WriteProperties(specProperties); //write it back.
        }

        public static ImportEntry GetOrAddImport(IMEPackage Pcc, string importFullName)
        {
            foreach (ImportEntry imp in Pcc.Imports)
            {
                if (imp.GetFullPath == importFullName)
                {
                    return imp;
                }
            }

            //Import doesn't exist, so we're gonna need to add it
            //But first we need to figure out what needs to be added.
            string[] importParts = importFullName.Split('.');
            List<int> upstreamLinks = new List<int>(); //0 = top level, 1 = next level... n = what we wanted to import
            int upstreamCount = 1;

            ImportEntry upstreamImport = null;
            while (upstreamCount < importParts.Count())
            {
                string upstream = string.Join(".", importParts, 0, importParts.Count() - upstreamCount);
                foreach (ImportEntry imp in Pcc.Imports)
                {
                    if (imp.GetFullPath == upstream)
                    {
                        upstreamImport = imp;
                        break;
                    }
                }

                if (upstreamImport != null)
                {
                    break;
                }
                upstreamCount++;
            }

            if (upstreamImport == null)
            {
                //There is no top level import, which is very unlikely (engine, sfxgame)
                return null;
            }

            //Have an upstream import, now we need to add downstream imports.
            ImportEntry mostdownstreamimport = null;

            while (upstreamCount > 0)
            {
                upstreamCount--;
                string fullobjectname = string.Join(".", importParts, 0, importParts.Count() - upstreamCount);
                Dictionary<string, string> importdbinfo = ImportClassDB[fullobjectname];

                int downstreamName = Pcc.FindNameOrAdd(importParts[importParts.Count() - upstreamCount - 1]);
                Debug.WriteLine(Pcc.Names[downstreamName]);
                int downstreamLinkIdx = upstreamImport.UIndex;
                Debug.WriteLine(upstreamImport.GetFullPath);

                int downstreamPackageName = Pcc.FindNameOrAdd(importdbinfo["packagefile"]);
                string downstreamClassName = importdbinfo["fullclasspath"];
                int lastPeriodIndex = downstreamClassName.LastIndexOf(".");
                if (lastPeriodIndex > 0)
                {
                    downstreamClassName = importdbinfo["fullclasspath"].Substring(lastPeriodIndex + 1);

                }

                int downstreamClassNameIdx = Pcc.FindNameOrAdd(downstreamClassName);
                Debug.WriteLine("Finding name " + downstreamClassName);
                //ImportEntry classImport = getOrAddImport();
                //int downstreamClass = 0;
                //if (classImport != null) {
                //    downstreamClass = classImport.UIndex; //no recursion pls
                //} else
                //{
                //    throw new Exception("No class was found for importing");
                //}

                mostdownstreamimport = new ImportEntry(Pcc);
                mostdownstreamimport.idxLink = downstreamLinkIdx;
                mostdownstreamimport.idxClassName = downstreamClassNameIdx;
                mostdownstreamimport.idxObjectName = downstreamName;
                mostdownstreamimport.idxPackageFile = downstreamPackageName;
                Pcc.addImport(mostdownstreamimport);
                upstreamImport = mostdownstreamimport;
            }
            return mostdownstreamimport;
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
        /// Gets the end name of a ReachSpec for property parsing. ME1 uses Nav, while ME2 and above use Actor.
        /// </summary>
        /// <param name="export">export used to determine which game is being parsed</param>
        /// <returns>Actor for ME2/ME3, Nav for ME1</returns>
        public static string GetReachSpecEndName(IExportEntry export)
        {
            return export.FileRef.Game != MEGame.ME1 ? "Actor" : "Nav";
        }

        /// <summary>
        /// Rounds a double to an int. Because apparently Microsoft doesn't know how to round numbers.
        /// </summary>
        /// <param name="d">Double to round</param>
        /// <returns>Rounded int</returns>
        public static int RoundDoubleToInt(double d)
        {
            if (d < 0)
            {
                return (int)(d - 0.5);
            }
            return (int)(d + 0.5);
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
                    var obj = JsonConvert.DeserializeObject<dynamic>(exportjson.ToString());

                    ExportClassDB = JsonConvert.DeserializeObject<List<PathfindingDB_ExportType>>(exportjson.ToString());
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

    [DebuggerDisplay("ReachSpecSize | {Header} {SpecHeight}x{SpecRadius}")]
    public class ReachSpecSize : NotifyPropertyChangedBase, IEquatable<ReachSpecSize>
    {
        public const int MOOK_RADIUS = 34;
        public const int MOOK_HEIGHT = 90;
        public const int MINIBOSS_RADIUS = 105;
        public const int MINIBOSS_HEIGHT = 145;
        public const int BOSS_RADIUS = 140;
        public const int BOSS_HEIGHT = 195;
        public const int BANSHEE_RADIUS = 50;
        public const int BANSHEE_HEIGHT = 125;

        public bool CustomSized;

        public ReachSpecSize()
        {

        }

        public ReachSpecSize(string header, int height, int radius, bool customsized = false)
        {
            Header = header;
            SpecHeight = height;
            SpecRadius = radius;
            CustomSized = customsized;
        }

        private string _header;
        public string Header
        {
            get => _header;
            set => SetProperty(ref _header, value);
        }

        private int _specRadius;
        public int SpecRadius
        {
            get => _specRadius;
            set => SetProperty(ref _specRadius, value);
        }

        private int _specHeight;
        public int SpecHeight
        {
            get => _specHeight;
            set => SetProperty(ref _specHeight, value);
        }

        public bool Equals(ReachSpecSize other)
        {
            return SpecRadius == other.SpecRadius && SpecHeight == other.SpecHeight;
        }
    }

    public class PathfindingDB_ExportType_EnsuredProperty
    {
        public string name { get; set; }
        public string type { get; set; }
        public string defaultvalue { get; set; }
    }

    public class PathfindingDB_ExportType
    {
        public string nodetypename { get; set; }
        public string fullclasspath { get; set; }
        public string name { get; set; }
        public string cylindercomponentarchetype { get; set; }
        public bool pathnode { get; set; }
        public string description { get; set; }
        public bool usesbtop { get; set; }
        public bool upgradetomaxpathsize { get; set; }
        public List<PathfindingDB_ExportType_EnsuredProperty> ensuredproperties { get; set; } = new List<PathfindingDB_ExportType_EnsuredProperty>();
        public string inboundspectype { get; set; }
    }
}
