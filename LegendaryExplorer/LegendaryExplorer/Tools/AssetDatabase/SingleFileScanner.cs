using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using LegendaryExplorerCore.UnrealScript;

namespace LegendaryExplorer.Tools.AssetDatabase
{
    public sealed record AssetDBScanOptions (bool ScanCRC, bool ScanLines, bool ScanPlotUsages, MELocalization Localization = MELocalization.INT);

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
        public FileLib FileLib { get; set; }
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

        private readonly int _fileKey;
        private readonly string _file;
        private readonly AssetDBScanOptions _options;

        public SingleFileScanner(string file, int filekey, AssetDBScanOptions options)
        {
            ShortFileName = Path.GetFileNameWithoutExtension(file);
            _file = file;
            _fileKey = filekey;
            _options = options;
        }

        /// <summary>
        /// Dumps Property data to concurrent dictionary
        /// </summary>
        public void DumpPackageFile(MEGame gameBeingDumped, ConcurrentAssetDB dbScanner)
        {
            try
            {
                if (_file.EndsWith(".cnd", StringComparison.OrdinalIgnoreCase))
                {
                    new PlotUsageScanner().ScanCndFile(_file, _fileKey, dbScanner, _options);
                    return;
                }

                using IMEPackage pcc = MEPackageHandler.OpenMEPackage(_file);
                if (pcc.Game != gameBeingDumped)
                {
                    return; //rogue file from other game or UDK
                }

                if (pcc.Localization != _options.Localization && pcc.Localization is not MELocalization.None)
                {
                    return;
                }

                bool IsDLC = pcc.IsInOfficialDLC();
                bool IsMod = !pcc.IsInBasegame() && !IsDLC;
                ExportScanInfo esi = null;
                foreach (ExportEntry entry in pcc.Exports)
                {
                    if (esi == null)
                    {
                        esi = new ExportScanInfo(entry, _file, _fileKey, IsMod, IsDLC);
                    }
                    else esi.Export = entry;
                    
                    if (DumpCanceled)
                    {
                        return;
                    }

                    try
                    {
                        if (entry is not null)
                        {
                            foreach (var scanner in Scanners)
                            {
                                scanner.ScanExport(esi, dbScanner, _options);
                            }
                        }
                    }
                    catch (Exception) when (!App.IsDebug)
                    {
                        MessageBox.Show($"Exception Bug detected in single file: {entry.FileRef.FilePath} Export:{entry.UIndex}");
                    }
                }
            }
            catch (Exception e) when (!App.IsDebug)
            {
                throw new Exception($"Error dumping package file {_file}. See the inner exception for details.", e);
            }
        }
    }
}