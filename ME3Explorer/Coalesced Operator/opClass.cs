using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Newtonsoft.Json;
using Gibbed.MassEffect3.FileFormats;
using NDesk.Options;
using Gibbed.MassEffect3.FileFormats.Coalesced;
using ME3Explorer.Coalesced_Editor;


namespace ME3Explorer.Coalesced_Operator
{
    public static class opClass
    {
        #region How to use

        //This class contains two sets of tools:
        //for manipulation of FileWrapper file stored in memory, 
        //and for decompiling/recompiling Coalesced.bin to/from JSON files

        //you need to load the FileWrapper yourself, use this:
        /*
            var input = System.IO.File.OpenRead(pathToJSONfile);
            var reader = new StreamReader(input);
            var text = reader.ReadToEnd();

            file = JsonConvert.DeserializeObject<FileWrapper>(text);
            Dictionary<string, Dictionary<string, List<string>>> d = file.Sections;

            reader.Close();
         
         
         */
        // pathToJSONfile is a string, and can be, example "tempPath + "\\10_bioweapon.json""

        //For manipulation of FileWrapper, use Read and Write entries functions

        //After manipulation of FileWrapper variable, you need to write it out on the 
        //disk before it can be compiled into Coalesced.bin!

        //To do that, use: 
        /*
                var outputFile = JsonConvert.SerializeObject(file, Formatting.Indented);
                File.WriteAllText(pathToJSONfile, outputFile);
          
        *to write out your loaded JSON file into the disk.
         *pathToJSONFile can be, for example: tempPath + "\\10_bioweapon.json"
         

        */

        #endregion

        #region Read and Write functions

        //This is the ReadEntry function, which will return one string at specified place in FileWrapper

        // For example, use:
        //      string neededValue = opClass.ReadEntry(file, "sfxgame.sfxinventorymanager", "fuelefficiency", 0);

        //          this will return "1.5" in unmodified Coalesced.bin which is the fuel efficiency used when 
        //          travelling with Normandy around the galaxy map 

        /// <summary>
        /// Returns a string which is a value of entry of given Index, in Key2, in Key1 of a FileWrapper variable
        /// </summary>
        /// <param name="file">FileWrapper variable deserialized from JSON</param>
        /// <param name="Key1">First Key to look for (section), example: "sfxgame.sfxinventorymanager"</param>
        /// <param name="Key2">Second Key within first Key, example: "fuelefficiency"</param>
        /// <param name="Index">Index of one value in Second Key's entries, example: 0</param>
        /// <returns>Returns value under given Index, example: "1.5" </returns>
        public static string ReadEntry(FileWrapper file, string Key1, string Key2, int Index)
        {
            string needed = "";
            foreach (KeyValuePair<string, Dictionary<string, List<Entry>>> firstKey in file.Sections)
            {
                if (firstKey.Key == Key1)
                {
                    foreach (KeyValuePair<string, List<Entry>> secondKey in firstKey.Value)
                    {
                        if (secondKey.Key == Key2)
                        {
                            if (Index >= 0 && Index < secondKey.Value.Count())
                                needed = secondKey.Value[Index].Value;
                        }
                    }

                }
            }
            return needed;
        }


        //similar to above, this will return ALL entries of a given section (weapon or whatever)
        //as an array of strings at specified entry - without index, it will return ALL indexes' values

        /// <summary>
        /// Returns a string array containing all values of a given entry
        /// </summary>
        /// <param name="file">FileWrapper variable deserialized from JSON</param>
        /// <param name="Key1">First key to look for (section), example: "sfxgame.sfxplayersquadloadoutdata"</param>
        /// <param name="Key2">Second key to look for in section, example: "shotguns"</param>
        /// <returns>Returns a string array containing all entries under "shotgun" entry in given section</returns>
        public static Entry[] ReadAllEntries(FileWrapper file, string Key1, string Key2)
        {
            Entry[] neededArray = { };
            foreach (KeyValuePair<string, Dictionary<string, List<Entry>>> firstKey in file.Sections)
            {
                if (firstKey.Key == Key1)
                {
                    foreach (KeyValuePair<string, List<Entry>> secondKey in firstKey.Value)
                    {
                        if (secondKey.Key == Key2) neededArray = secondKey.Value.ToArray();
                    }
                }
            }
            return neededArray;
        }


         //This will write a value into the FileWrapper which is currently in the memory

         // How to use

         //   opClass.WriteEntry(file, "sfxgame.sfxinventorymanager", "fuelefficiency", 0, textBox2.Text);
         
         //         This will write a string contained in TextBox2 into the JSON file under entry fuel efficiency
         
         // Remember, to apply any of this, you still need to write the FileWrapper into JSON on the disk

        /// <summary>
        /// Writes an entry into FileWrapper file in memory. Nothing will be written if section doesn't already exist.
        /// </summary>
        /// <param name="file">FileWrapper variable deserialized from JSON</param>
        /// <param name="Key1">First key to look for (section), example: "sfxgame.sfxinventorymanager"</param>
        /// <param name="Key2">Second key to look for in given section, example: "fuelefficiency"</param>
        /// <param name="Index">Index of value in Second Key to overwrite, example : 0</param>
        /// <param name="newValue">Value to write into Index, example: "1.0" </param>
        public static  void WriteEntry(FileWrapper file, string Key1, string Key2, int Index, string newValue)
        {
            foreach (KeyValuePair<string, Dictionary<string, List<Entry>>> firstKey in file.Sections)
            {
                if (firstKey.Key == Key1)
                {
                    foreach (KeyValuePair<string, List<Entry>> secondKey in firstKey.Value)
                    {
                        if (secondKey.Key == Key2)
                        {
                            if (Index >= 0 && Index < secondKey.Value.Count())
                            {
                                Entry e = secondKey.Value[Index];
                                e.Value = newValue;
                                secondKey.Value[Index] = e;
                            }
                        }
                    }
                }
            }
        }



        //will write new array of strings as values of Key2 of Key1 of file.Sections

        /// <summary>
        /// Will write an entire value set from string array into section
        /// </summary>
        /// <param name="file">FileWrapper variable in memory into which you will write</param>
        /// <param name="Key1">First key to look for, section, example: "sfxgame.sfxplayersquadloadoutdata"</param>
        /// <param name="Key2">Second key to look for in section, example: "shotguns"</param>
        /// <param name="newValue">A string array containing all values you wish to write</param>
        public static  void WriteAllEntries(FileWrapper file, string Key1, string Key2, string[] newValue)
        {
            foreach (KeyValuePair<string, Dictionary<string, List<Entry>>> firstKey in file.Sections)
            {
                if (firstKey.Key == Key1)
                {
                    foreach (KeyValuePair<string, List<Entry>> secondKey in firstKey.Value)
                    {
                        if (secondKey.Key == Key2)
                        {
                            for (int Index = 0; Index < secondKey.Value.Count(); Index++)
                            {
                                Entry e = secondKey.Value[Index];
                                e.Value = newValue[Index];
                                secondKey.Value[Index] = e;
                            }
                        }
                    }
                }
            }
        }

        #endregion

        #region Load and Save BIN functions

        //Voider's and Gibbed's code

        //Creating a whole bunch of JSON files out of Coalesced.bin
        /* How to use
         * string tempPath = System.IO.Path.GetTempPath() + "CoalTemp\\";
         * string coalescedPath = OpenFileDialog1.SelectedFile.ToString();
         * opClass.LoadBIN(coalescedPath, tempPath);
                This will create a lot of JSON files in Temporary folder + \CoalTemp,
                out of Coalesced.bin selected via OpenFileDialog
         
         */
        enum Mode
        {
            Unknown,
            ToJson,
            ToBin,
        }


        public static void LoadBIN(string path, string temp)
        {
            var mode = Mode.ToJson;
            if (mode == Mode.ToJson)
            {
                var inputPath = path;
                var outputPath = temp;

                using (var input = System.IO.File.OpenRead(inputPath))
                {
                    var coal = new CoalescedFile();
                    coal.Deserialize(input);

                    var padding = coal.Files.Count.ToString().Length;

                    var setup = new Setup
                    {
                        Endian = coal.Endian,
                        Version = coal.Version,
                    };

                    var counter = 0;
                    foreach (var file in coal.Files)
                    {
                        var iniPath = string.Format("{1}_{0}",
                            Path.GetFileNameWithoutExtension(file.Name),
                            counter.ToString().PadLeft(padding, '0'));
                        iniPath = Path.Combine(outputPath, Path.ChangeExtension(iniPath, ".json"));
                        counter++;

                        setup.Files.Add(Path.GetFileName(iniPath));

                        Directory.CreateDirectory(Path.GetDirectoryName(iniPath));
                        using (var output = System.IO.File.Create(iniPath))
                        {
                            var writer = new StreamWriter(output);
                            writer.Write(JsonConvert.SerializeObject(
                                new FileWrapper()
                                {
                                    Name = file.Name,
                                    Sections = file.Sections,
                                }, Formatting.Indented));
                            writer.Flush();
                        }
                    }

                    Directory.CreateDirectory(outputPath);
                    using (var output = System.IO.File.Create(Path.Combine(outputPath, "@coalesced.json")))
                    {
                        var writer = new StreamWriter(output);
                        writer.Write(JsonConvert.SerializeObject(
                            setup, Formatting.Indented));
                        writer.Flush();
                    }
                }
            }
        }

        //recompiling all JSON files back into Coalesced.bin
        /* How to use:
          editor.SaveBIN(coalescedPath, tempPath);
          
         */
        public static void SaveBIN(string path, string temp)
        {
            var mode = Mode.ToBin;
            var inputPath = temp;
            var outputPath = path;
            Setup setup;
            var setupPath = Path.Combine(inputPath, "@coalesced.json");
            using (var input = System.IO.File.OpenRead(setupPath))
            {
                var reader = new StreamReader(input);
                var text = reader.ReadToEnd();
                try
                {
                    setup = JsonConvert.DeserializeObject<Setup>(text);
                }
                catch (JsonReaderException e)
                {
                    return;
                }
            }

            var coal = new CoalescedFile
            {
                Endian = setup.Endian,
                Version = setup.Version,
            };

            foreach (var iniName in setup.Files)
            {
                string iniPath = Path.IsPathRooted(iniName) == false ?
                    Path.Combine(inputPath, iniName) : iniName;

                using (var input = System.IO.File.OpenRead(iniPath))
                {
                    var reader = new StreamReader(input);
                    var text = reader.ReadToEnd();

                    FileWrapper file;
                    try
                    {
                        file = JsonConvert.DeserializeObject<FileWrapper>(text);
                    }
                    catch (JsonReaderException e)
                    {
                        return;
                    }

                    coal.Files.Add(new Gibbed.MassEffect3.FileFormats.Coalesced.File()
                    {
                        Name = file.Name,
                        Sections = file.Sections,
                    }
                    );
                }
            }

            using (var output = System.IO.File.Create(outputPath))
            {
                coal.Serialize(output);
            }
        }

        #endregion

        #region Load and Save JSON functions
        /*
         These functions are for loading and saving JSON files into/out of memory
         * They are saved as FileWrapper file variable, which is later manipulated
         * with Read and Write functions of this class
         * 
         // How to use
         FileWrapper weaponsFile = LoadJSON(pathToJSON);
            *this will deserialize JSON file into the weaponsFile variable
         
         
         */
        /// <summary>
        /// Returns a FileWrapper variable deserialized from pointed JSON file
        /// </summary>
        /// <param name="pathToJSON">String ending in "\\[...].JSON";</param>
        /// <returns></returns>
        public static FileWrapper LoadJSON(string pathToJSON)
        {
            FileWrapper file;
            var input = System.IO.File.OpenRead(pathToJSON);
            var reader = new StreamReader(input);
            var text = reader.ReadToEnd();

            file = JsonConvert.DeserializeObject<FileWrapper>(text);
            reader.Close();
            return file;
        }

        /// <summary>
        /// Will create a JSON file pointed at path, serialized from a FileWrapper variable
        /// </summary>
        /// <param name="file">FileWrapper variable in memory</param>
        /// <param name="pathToJSON">String ending in "\\[...].JSON";</param>
        public static void SaveJSON(FileWrapper file, string pathToJSON)
        {
            var outputFile = JsonConvert.SerializeObject(file, Formatting.Indented);
            System.IO.File.WriteAllText(pathToJSON, outputFile);


        }
        #endregion
    }
}
