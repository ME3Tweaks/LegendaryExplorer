using LegendaryExplorerCore.Packages;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LegendaryExplorerCore.UnrealScript.Utilities
{
    // TODO: figure out how to maybe store line data in an export metadata somehow instead of json file?
    public class ClassLineStore
    {
        //Multiton
        private static readonly ConcurrentDictionary<MEGame, ClassLineStore> _stores = new();

        public static ClassLineStore GetStore(MEGame game) =>
        _stores.GetOrAdd(game, g => new ClassLineStore(g));

        private readonly string _mapFile;
        private Dictionary<string, Dictionary<string, int>> map;

        public ClassLineStore(MEGame game) 
        {
            //Save in AppData folder   
            var baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LegendaryExplorer", "PackageEditor");
            Directory.CreateDirectory(baseDir);

            _mapFile = Path.Combine(baseDir, game + "_IDELineMap.json");
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

        public int? GetLine(string pccFile, string className)
        {
            if(map.TryGetValue(pccFile, out var classDic) && classDic.TryGetValue(className, out var line))
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

            //Don't bother saving classes with line numbers beyond a scroll
            var filteredMap = map
                .Where(kv => kv.Value != null && kv.Value.Count > 0)
                .ToDictionary(
                    pkg=>pkg.Key, 
                    kv => kv.Value
                        .Where(clDic=>clDic.Value > 70)
                        .ToDictionary(clDic=>clDic.Key, clDic=>clDic.Value)
                );

            if (filteredMap.Values.Count == 0)
            {
                // No items found, keep this avoid overwriting/saving?
                return;
            }

            var json = JsonConvert.SerializeObject(filteredMap, Formatting.Indented);
            File.WriteAllText(_mapFile, json);
        }
    }
}
