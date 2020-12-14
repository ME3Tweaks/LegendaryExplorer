using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ME3Explorer.ME3Script;
using ME3ExplorerCore.GameFilesystem;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.Packages;
using ME3ExplorerCore.Packages.CloningImportingAndRelinking;
using ME3Script.Analysis.Symbols;

namespace ME3Script
{
    public class FileLib : IPackageUser, IDisposable
    {
        private SymbolTable _symbols;
        public SymbolTable GetSymbolTable() => IsInitialized ? _symbols?.Clone() : null;

        public SymbolTable ReadonlySymbolTable => IsInitialized ? _symbols : null;

        public bool IsInitialized { get; private set; }

        public bool HadInitializationError { get; private set; }

        public event Action<bool> InitializationStatusChange;

        private readonly object initializationLock = new object();
        public async Task<bool> Initialize()
        {
            if (IsInitialized)
            {
                return true;
            }

            return await StandardLibrary.InitializeStandardLib() && await Task.Run(() =>
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
                _symbols = StandardLibrary.GetSymbolTable();
                var files = EntryImporter.GetPossibleAssociatedFiles(Pcc);
                var gameFiles = MELoadedFiles.GetFilesLoadedInGame(Pcc.Game);
                foreach (var fileName in Enumerable.Reverse(files))
                {
                    if (gameFiles.TryGetValue(fileName, out string path) &&  File.Exists(path))
                    {
                        using var pcc = MEPackageHandler.OpenMEPackage(path);
                        if (!StandardLibrary.ResolveAllClassesInPackage(pcc, ref _symbols))
                        {
                            return false;
                        }
                    }
                }
                return StandardLibrary.ResolveAllClassesInPackage(Pcc, ref _symbols);
            }
            catch (Exception e)
            {
                return false;
            }
        }

        public IMEPackage Pcc { get; }

        public readonly List<int> ScriptUIndexes = new List<int>();

        public FileLib(IMEPackage pcc)
        {
            Pcc = pcc;
            pcc.WeakUsers.Add(this);
            ScriptUIndexes.AddRange(pcc.Exports.Where(IsScriptExport).Select(exp => exp.UIndex));
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
                 || update.Change.Has(PackageChange.Add) && Pcc.GetEntry(update.Index) is ExportEntry exp && (IsScriptExport(exp) || exp.ClassName == "Function"))
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
