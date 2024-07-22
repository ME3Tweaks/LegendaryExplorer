using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using LegendaryExplorer.Tools.AssetDatabase.Scanners;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.ME1;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.Classes;
using LegendaryExplorerCore.UnrealScript;

namespace LegendaryExplorer.Tools.AssetDatabase
{
    public sealed record AssetDBScanOptions (bool ScanCRC, MELocalization Localization = MELocalization.INT);

    /// <summary>
    /// Caches info about the export being scanned, containing expensive calls such as GetProperties, IsDefault, etc
    /// </summary>
    internal class ExportScanInfo
    {
        public ExportScanInfo(ExportEntry export, string fileName, int fileKey, bool isMod, bool isDlc)
        {
            Export = export;
            FileName = fileName;
            FileKey = fileKey;
            IsMod = isMod;
            IsDlc = isDlc;
        }

        private ExportEntry export;
        public ExportEntry Export
        {
            get => export;
            set
            {
                export = value;
                assetKey = null;
                props = null;
                ObjectNameInstanced = export.ObjectName.Instanced;
                ClassName = export.ClassName;
                IsDefault = export?.IsDefaultObject == true;
            }
        }

        public int FileKey { get; }
        public bool IsMod { get; }
        public bool IsDlc { get; }
        public string FileName { get; }
        public string ObjectNameInstanced { get; private set; }
        public bool IsDefault { get; private set; }
        public string ClassName { get; private set; }
        public FileLib FileLib { get; set; }

        //This is usually not needed, and creates many gigabytes of string allocations if done on every single export
        private string assetKey;
        public string AssetKey => assetKey ??= export.InstancedFullPath.ToLower();

        //Since ExportEntry:GetProperties() is very expensive,
        //we lazy load it so that we don't pay the price on exports where we don't need it.
        //(This reduces the percentage of database generation time spent on getting properties from ~60% to ~10%!)
        private PropertyCollection props;
        public PropertyCollection Properties => props ??= export.GetProperties();
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

                bool isDlc = pcc.IsInOfficialDLC();
                bool isMod = !pcc.IsInBasegame() && !isDlc;
                ExportScanInfo esi = null;
                foreach (ExportEntry export in pcc.Exports)
                {
                    if (esi is null)
                    {
                        esi = new ExportScanInfo(export, _file, _fileKey, isMod, isDlc);
                    }
                    else
                    {
                        esi.Export = export;
                    }

                    if (DumpCanceled)
                    {
                        return;
                    }

                    try
                    {
                        foreach (AssetScanner scanner in Scanners)
                        {
                            scanner.ScanExport(esi, dbScanner, _options);
                        }
                    }
                    catch (Exception e) //when (!App.IsDebug)
                    {
                        Application.Current.Dispatcher.Invoke(() => 
                            MessageBox.Show($"Error while scanning {export.FileRef.FilePath} #{export.UIndex} {export.InstancedFullPath}\n\n{e.FlattenException()}"));
                    }
                }
            }
            catch (Exception e) //when (!App.IsDebug)
            {
                throw new Exception($"Error dumping package file {_file}. See the inner exception for details.", e);
            }
        }
    }
}