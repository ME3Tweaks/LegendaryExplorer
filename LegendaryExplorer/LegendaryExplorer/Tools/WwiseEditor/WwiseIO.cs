using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorer.Audio;
using LegendaryExplorer.UnrealExtensions;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Newtonsoft.Json.Linq;
using WwiseTools.Objects;
using WwiseTools.Src.Models.SoundBank;
using WwiseTools.Utils;
using WwiseTools.Utils.SoundBank;

namespace LegendaryExplorer.Tools.WwiseEditor
{
    internal class WwiseIO
    {
        /// <summary>
        /// Exports a WwiseBank (LE only) to a basic Wwise Project - this will lose a lot of the finer settings!
        /// </summary>
        /// <param name="export"></param>
        /// <param name="dlgFileName"></param>
        public static async void ExportBankToProject(ExportEntry export, string projectOutputDirectory)
        {
            if (export.Game != MEGame.LE2 && export.Game != MEGame.LE3)
                throw new Exception("Unsupported game for wwisebank export");

            // Create a project
            var projFile = WwiseCliHandler.CreateNewProject(projectOutputDirectory);

            // Run Wwise in automation mode
            WwiseCliHandler.RunWwiseInAutomatedMode(export.Game, projFile);

            if (!await WwiseUtility.Instance.ConnectAsync())
            {
                Debug.WriteLine(@"Wwise API endpoint not running!");
                return;
            }

            // Generate a soundbank
            var soundbank = await WwiseUtility.Instance.CreateObjectAtPathAsync(export.ObjectName, WwiseObject.ObjectType.SoundBank, "\\Soundbanks\\Default Work Unit");

            // Now we inspect the WwiseBank
            WwiseBank bank = ObjectBinary.From<WwiseBank>(export);
            List<EmbeddedWEMFile> wems = new List<EmbeddedWEMFile>();
            foreach ((uint wemID, byte[] wemData) in bank.EmbeddedFiles)
            {
                var wem = new EmbeddedWEMFile(wemData, "", export, wemID);
                wems.Add(wem);
            }

            foreach (var wem in wems)
            {
                // Right now we don't support preloading - that probably has to be done in 
                // wwise settings.
                if (!wem.HasBeenFixed)
                {
                    string basePath = $"{Path.GetTempPath()}ME3EXP_SOUND_{Guid.NewGuid()}";
                    var outpath = basePath + ".wem";
                    File.WriteAllBytes(outpath, wem.WemData);
                    var audioStream = AudioStreamHelper.ConvertRIFFToWaveVGMStream(outpath); //use vgmstream
                    File.Delete(outpath);

                    var wavFile = Path.Combine(Path.GetTempPath(), $"{export.ObjectName.Name}_{wem.Id:X8}.wav");
                    audioStream.WriteToFile(wavFile);

                    // Import to the default location
                    var addeSound = await WwiseUtility.Instance.ImportSoundAsync(wavFile);

                    // Add to soundbank
                    await WwiseUtility.Instance.AddSoundBankInclusionAsync(soundbank, new SoundBankInclusion() { Object = addeSound });
                }
            }

            // Add the events
            foreach (var ev in export.FileRef.Exports.Where(x => x.ClassName == "WwiseEvent"))
            {
                if (ev.GetProperty<StructProperty>("Relationships")?.Properties.GetProp<ObjectProperty>("Bank")?.Value != export.UIndex)
                    continue; // Not an event for our bank

                var evt = await WwiseUtility.Instance.CreateObjectAtPathAsync(ev.ObjectName.Name, WwiseObject.ObjectType.Event, "\\Events\\Default Work Unit");
                var action = await WwiseUtility.Instance.CreateObjectAtPathAsync(ev.ObjectName.Name, WwiseObject.ObjectType.Event, "\\Events\\Default Work Unit");

                //await WwiseUtility.Instance.SetObjectPropertyAsync(ev.ObjectName.Name, WwiseObject.ObjectType.Event, "\\Events\\Default Work Unit");

            }

            await WwiseUtility.Instance.SaveWwiseProjectAsync();

            //await WwiseUtility.Instance.SetAutomationMode(false);
            //var info = await WwiseUtility.Instance.LoadWwiseProjectAsync(projFile, false); // do not save current
            //Debug.WriteLine(info);
        }
    }
}
