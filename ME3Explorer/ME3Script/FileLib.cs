using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Explorer;
using ME3Explorer.ME3Script;
using ME3Explorer.Packages;
using ME3Script.Analysis.Symbols;
using ME3Script.Language.Tree;

namespace ME3Script
{
    public class FileLib : IPackageUser
    {
        private SymbolTable _symbols;
        public SymbolTable GetSymbolTable() => IsInitialized ? _symbols?.Clone() : null;

        public SymbolTable ReadonlySymbolTable => IsInitialized ? _symbols : null;

        public bool IsInitialized { get; private set; }

        public bool HadInitializationError { get; private set; }

        public event EventHandler Initialized;

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

                Initialized?.Invoke(null, EventArgs.Empty);
                return success;
            });
        }

        private bool InternalInitialize()
        {
            try
            {
                _symbols = StandardLibrary.GetSymbolTable();
                //TODO: make file libs for non-standardlib files this depends on
                return StandardLibrary.ResolveAllClassesInPackage(Pcc.FilePath, ref _symbols);
            }
            catch (Exception e)
            {
                return false;
            }
        }

        public IMEPackage Pcc { get; }

        public FileLib(IMEPackage pcc)
        {
            Pcc = pcc;
            //pcc.Users.Add(this);//TODO: Once librarysplit merge has been completed, create an actual system for weak users in UnrealPackage
        }

        void IPackageUser.handleUpdate(List<PackageUpdate> updates)
        {
            //TODO: invalidate this when changes are made to script objects in this file
        }

        void IPackageUser.RegisterClosed(Action handler)
        {
        }

        void IPackageUser.ReleaseUse()
        {
        }
    }
}
