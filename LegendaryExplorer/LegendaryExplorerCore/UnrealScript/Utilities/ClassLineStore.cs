using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LegendaryExplorerCore.UnrealScript.Utilities
{
    public class ClassLineStore
    {
        // TODO: figure out how to store this data in an export somehow
        private readonly string _mapFile;
        private Dictionary<string, Dictionary<string, int>> map;

        //AppData folder
        public ClassLineStore() 
        {
            
            var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\LegendaryExplorer\\PackageEditor";
            _mapFile = Path.Combine(baseDir, "ScriptLineMap.json");
            LoadFromFile();
        }

        private void LoadFromFile() 
        {
            if (File.Exists(_mapFile)){
                var json = File.ReadAllText(_mapFile);
                map = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, int>>>(json)
                    ?? new Dictionary<string, Dictionary<string, int>>();
            }
            else{
                map = new Dictionary<string, Dictionary<string, int>>();
            }
        }

        public int? GetLine(string file, string className)
        {
            if(map.TryGetValue(file, out var classDic) && classDic.TryGetValue(className, out var line))
            {
                return line;
            }
            return null;
        }

        public void Set(string file, string className, int line)
        {
            ArgumentNullException.ThrowIfNull(file, className);

            if (className.StartsWith("Default"))
            {
                return;
            }
            if (!map.TryGetValue(file, out var classDic)) {
                classDic = new Dictionary<string, int>();
                map[file] = classDic;
            }
            
            classDic[className] = line;
        }

        public void Save()
        {
            var json = JsonConvert.SerializeObject(map, Formatting.Indented);
            File.WriteAllText(_mapFile, json);
        }
    }
}
