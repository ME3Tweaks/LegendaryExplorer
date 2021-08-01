using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using LegendaryExplorer.Tools.AssetDatabase.Scanners;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.ME1;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.Classes;

namespace LegendaryExplorer.Tools.AssetDatabase
{
    public sealed record AssetDBScanOptions (bool ScanCRC, bool ScanLines, bool ScanPlotUsages);

    /// <summary>
    /// Caches info about the export being scanned, containing expensive calls such as GetProperties, IsDefault, etc
    /// </summary>
    internal class ExportScanInfo
    {
        public ExportScanInfo(ExportEntry export, string FileName, int FileKey, bool IsMod, bool IsDLC)
        {
            Export = export;
            this.FileName = FileName;
            this.FileKey = FileKey;
            this.IsMod = IsMod;
            IsDlc = IsDLC;
        }

        private ExportEntry export;
        public ExportEntry Export
        {
            get => export;
            set
            {
                export = value;
                Properties = export.GetProperties(false, false);
                AssetKey = export.InstancedFullPath.ToLower();
                ObjectNameInstanced = export.ObjectName.Instanced;
                ClassName = export.ClassName;
                IsDefault = export?.IsDefaultObject == true;
            }
        }

        public int FileKey { get; private set; }
        public bool IsMod { get; private set; }
        public bool IsDlc { get; private set; }
        public string FileName { get; private set; }
        public PropertyCollection Properties { get; private set; }
        public string AssetKey { get; private set; }
        public string ObjectNameInstanced { get; private set; }
        public bool IsDefault { get; private set; }
        public string ClassName { get; private set; }
    }

    /// <summary>
    /// Scans a single package file, adding all found records into a generated database
    /// </summary>
    public class SingleFileScanner
    {
        public string ShortFileName { get; }
        public bool DumpCanceled;

        private static readonly List<AssetScanner> Scanners = new()
        {
            new ClassScanner(),
            new MaterialScanner(),
            new AnimationScanner(),
            new MeshScanner(),
            new VFXScanner(),
            new TextureScanner(),
            new GUIScanner(),
            new ConversationScanner(),
            new PlotUsageScanner()
        };

        private readonly int FileKey;
        private readonly string File;
        private readonly AssetDBScanOptions Options;

        public SingleFileScanner(string file, int filekey, AssetDBScanOptions options)
        {
            File = file;
            ShortFileName = Path.GetFileNameWithoutExtension(file);
            FileKey = filekey;
            Options = options;
        }

        /// <summary>
        /// Dumps Property data to concurrent dictionary
        /// </summary>
        public void DumpPackageFile(MEGame GameBeingDumped, ConcurrentAssetDB dbScanner)
        {

            try
            {
                using IMEPackage pcc = MEPackageHandler.OpenMEPackage(File);
                if (pcc.Game != GameBeingDumped)
                {
                    return; //rogue file from other game or UDK
                }

                bool IsDLC = pcc.IsInOfficialDLC();
                bool IsMod = !pcc.IsInBasegame() && !IsDLC;
                ExportScanInfo esi = null;
                foreach (ExportEntry entry in pcc.Exports)
                {
                    if (esi == null)
                    {
                        esi = new ExportScanInfo(entry, File, FileKey, IsMod, IsDLC);
                    }
                    else esi.Export = entry;

                    //TODO: NEED BETTER WAY TO HANDLE LANGUAGES
                    if (DumpCanceled || pcc.FilePath.Contains("_LOC_") && !pcc.FilePath.Contains("INT"))
                    {
                        return;
                    }

                    try
                    {
                        if (entry is not null)
                        {
                            foreach (var scanner in Scanners)
                            {
                                scanner.ScanExport(esi, dbScanner, Options);
                            }
                        }
                    }
                    catch (Exception e) when (!App.IsDebug)
                    {
                        MessageBox.Show($"Exception Bug detected in single file: {entry.FileRef.FilePath} Export:{entry.UIndex}");
                    }
                }
            }
            catch (Exception e) when (!App.IsDebug)
            {
                throw new Exception($"Error dumping package file {File}. See the inner exception for details.", e);
            }
        }
    }
}