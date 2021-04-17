using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ME3ExplorerCore.GameFilesystem;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.Packages;
using ME3ExplorerCore.Packages.CloningImportingAndRelinking;
using ME3ExplorerCore.UnrealScript.Analysis.Symbols;

namespace ME3ExplorerCore.UnrealScript
{
    public partial class FileLib : IPackageUser, IDisposable
    {
        private SymbolTable _symbols;
        public SymbolTable GetSymbolTable() => IsInitialized ? _symbols?.Clone() : null;

        public SymbolTable ReadonlySymbolTable => IsInitialized ? _symbols : null;

        public bool IsInitialized { get; private set; }

        public bool HadInitializationError { get; private set; }

        public event Action<bool> InitializationStatusChange;

        private readonly object initializationLock = new();

        private readonly BaseLib Base;

        public async Task<bool> Initialize()
        {
            if (IsInitialized)
            {
                return true;
            }

            if (!await Base.InitializeStandardLib())
            {
                HadInitializationError = Base.HadInitializationError;
                InitializationStatusChange?.Invoke(true);
                return false;
            }

            if (Base.BaseFileNames.Contains(Path.GetFileName(Pcc.FilePath)))
            {
                _symbols = Base.GetSymbolTable();
                HadInitializationError = false;
                IsInitialized = true;
                InitializationStatusChange?.Invoke(true);
                return true;
            }

            return await Task.Run(() =>
            {
                if (IsInitialized)
                {
                    return true;
                }
                bool success;
                lock (initializationLock)
                {
                    if (IsInitialized)
                    {
                        return true;
                    }

                    success = InternalInitialize();
                    IsInitialized = success;
                    HadInitializationError = !success;
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
                var files = EntryImporter.GetPossibleAssociatedFiles(Pcc, false);
                var gameFiles = MELoadedFiles.GetFilesLoadedInGame(Pcc.Game);
                foreach (var fileName in Enumerable.Reverse(files))
                {
                    if (gameFiles.TryGetValue(fileName, out string path) &&  File.Exists(path))
                    {
                        using var pcc = MEPackageHandler.OpenMEPackage(path);
                        if (!BaseLib.ResolveAllClassesInPackage(pcc, ref _symbols))
                        {
                            return false;
                        }
                    }
                }
                return BaseLib.ResolveAllClassesInPackage(Pcc, ref _symbols);
            }
            catch (Exception e) when(!ME3ExplorerCoreLib.IsDebug)
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
            //TODO: invalidate this when changes are made to script objects in this file
            foreach (PackageUpdate update in updates.Where(u => u.Change.Has(PackageChange.Export)))
            {
                if (ScriptUIndexes.Contains(update.Index) 
                 || Pcc.GetEntry(update.Index) is ExportEntry exp && (IsScriptExport(exp) || exp.ClassName == "Function"))
                {
                    lock (initializationLock)
                    {
                        IsInitialized = false;
                        HadInitializationError = false;
                        _symbols = null;
                    }
                    InitializationStatusChange?.Invoke(false);
                    return;
                }
            }
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
