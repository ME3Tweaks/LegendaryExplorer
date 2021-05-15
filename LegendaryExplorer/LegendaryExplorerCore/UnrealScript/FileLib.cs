using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.UnrealScript.Analysis.Symbols;
using LegendaryExplorerCore.UnrealScript.Compiling.Errors;

namespace LegendaryExplorerCore.UnrealScript
{
    public partial class FileLib : IPackageUser, IDisposable
    {
        private SymbolTable _symbols;
        public SymbolTable GetSymbolTable() => IsInitialized ? _symbols?.Clone() : null;

        public SymbolTable ReadonlySymbolTable => IsInitialized ? _symbols : null;

        public bool IsInitialized { get; private set; }

        public bool HadInitializationError { get; private set; }

        public MessageLog InitializationLog;

        public event Action<bool> InitializationStatusChange;

        private readonly object initializationLock = new();

        private readonly BaseLib Base;

        public async Task<bool> Initialize()
        {
            if (IsInitialized)
            {
                return true;
            }

            return await Task.Run(() =>
            {
                bool success = false;
                lock (initializationLock)
                {
                    if (IsInitialized)
                    {
                        return true;
                    }

                    InitializationLog = new MessageLog();
                    if (!Base.InitializeStandardLib(InitializationLog).Result)
                    {
                        HadInitializationError = true;
                    }
                    else if (BaseFileNames(Base.Game).Contains(Path.GetFileName(Pcc.FilePath)))
                    {
                        _symbols = Base.GetSymbolTable();
                        HadInitializationError = false;
                        IsInitialized = true;

                        success = true;
                    }

                    if (!IsInitialized && !HadInitializationError)
                    {
                        success = InternalInitialize();
                        IsInitialized = success;
                        HadInitializationError = !success;
                    }
                }

                InitializationStatusChange?.Invoke(true);
                return success;
            });
        }

        private bool InternalInitialize()
        {
            try
            {
                _symbols = Base.GetSymbolTable();
                var files = EntryImporter.GetPossibleAssociatedFiles(Pcc, includeNonBioPRelated: false);
                if (Pcc.Game is MEGame.ME3)
                {
                    if (Pcc.FindEntry("SFXGameMPContent") is IEntry {ClassName: "Package"} && !files.Contains("BIOP_MP_COMMON.pcc"))
                    {
                        files.Add("BIOP_MP_COMMON.pcc");
                    }
                    if (Pcc.FindEntry("SFXGameContentDLC_CON_MP2") is IEntry {ClassName: "Package"})
                    {
                        files.Add("Startup_DLC_CON_MP2_INT.pcc");
                    }
                    if (Pcc.FindEntry("SFXGameContentDLC_CON_MP3") is IEntry { ClassName: "Package" })
                    {
                        files.Add("Startup_DLC_CON_MP3_INT.pcc");
                    }
                    if (Pcc.FindEntry("SFXGameContentDLC_CON_MP4") is IEntry { ClassName: "Package" })
                    {
                        files.Add("Startup_DLC_CON_MP4_INT.pcc");
                    }
                    if (Pcc.FindEntry("SFXGameContentDLC_CON_MP5") is IEntry { ClassName: "Package" })
                    {
                        files.Add("Startup_DLC_CON_MP5_INT.pcc");
                    }
                }
                var gameFiles = MELoadedFiles.GetFilesLoadedInGame(Pcc.Game);
                foreach (var fileName in Enumerable.Reverse(files))
                {
                    if (gameFiles.TryGetValue(fileName, out string path) && File.Exists(path))
                    {
                        using var pcc = MEPackageHandler.OpenMEPackage(path);
                        if (!BaseLib.ResolveAllClassesInPackage(pcc, ref _symbols, InitializationLog))
                        {
                            return false;
                        }
                    }
                }
                return BaseLib.ResolveAllClassesInPackage(Pcc, ref _symbols, InitializationLog);
            }
            catch (Exception e) when (!LegendaryExplorerCoreLib.IsDebug)
            {
                return false;
            }
        }

        public IMEPackage Pcc { get; }

        public readonly List<int> ScriptUIndexes = new();

        public FileLib(IMEPackage pcc)
        {
            Pcc = pcc;
            pcc.WeakUsers.Add(this);
            ScriptUIndexes.AddRange(pcc.Exports.Where(IsScriptExport).Select(exp => exp.UIndex));
            Base = pcc.Game switch
            {
                MEGame.ME3 => BaseLib.ME3BaseLib,
                MEGame.ME2 => BaseLib.ME2BaseLib,
                MEGame.ME1 => BaseLib.ME1BaseLib,
                _ => throw new ArgumentOutOfRangeException(nameof(pcc), $"Cannot compile scripts for this game version: {pcc.Game}")
            };
        }

        static bool IsScriptExport(ExportEntry exp)
        {
            switch (exp.ClassName)
            {
                case "Class":
                case "State":
                case "Enum":
                case "Const":
                case "Function":
                case "ScriptStruct":
                case "IntProperty":
                case "BoolProperty":
                case "FloatProperty":
                case "NameProperty":
                case "StrProperty":
                case "StringRefProperty":
                case "ByteProperty":
                case "ObjectProperty":
                case "ComponentProperty":
                case "InterfaceProperty":
                case "ArrayProperty":
                case "StructProperty":
                case "BioMask4Property":
                case "MapProperty":
                case "ClassProperty":
                case "DelegateProperty":
                    return true;
                default:
                    return false;
            }
        }

        void IPackageUser.handleUpdate(List<PackageUpdate> updates)
        {
            if (_symbols is null)
            {
                return;
            }
            //foreach (PackageUpdate update in updates.Where(u => u.Change.Has(PackageChange.Export)))
            //{
            //    if (ScriptUIndexes.Contains(update.Index)
            //     || Pcc.GetEntry(update.Index) is ExportEntry exp && (IsScriptExport(exp) || exp.ClassName == "Function"))
            //    {
            //        lock (initializationLock)
            //        {
            //            IsInitialized = false;
            //            HadInitializationError = false;
            //            _symbols = null;
            //        }
            //        InitializationStatusChange?.Invoke(false);
            //        return;
            //    }
            //}
        }

        void IPackageUser.RegisterClosed(Action handler)
        {
        }

        void IPackageUser.ReleaseUse()
        {
        }

        public void Dispose()
        {
            Pcc?.WeakUsers.Remove(this);
        }
    }
}
