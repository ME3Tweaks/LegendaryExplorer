using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.UnrealScript
{
    /// <summary>
    /// Object for passing through to various compiler functions to allow adding new features without having to edit method signatures.
    /// </summary>
    public class UnrealScriptOptionsPackage
    {
        /// <summary>
        /// Package cache to use in UnrealScript operations
        /// </summary>
        public PackageCache Cache { get; set; }

        /// <summary>
        /// Forced game path
        /// </summary>
        public string GamePathOverride { get; set; }

        /// <summary>
        /// Delegate that can be used to resolve package objects. When used properly this can significantly improve performance
        /// </summary>
        public Func<string, PackageCache, IMEPackage> CustomFileResolver { get; set; }

        public delegate IEntry ObjectResolutionDelegate(IMEPackage sourcePackage, string instancedFullPath, string className);

        /// <summary>
        /// Delegate that can be used to look up missing objects when compiling classes and a referenced object is not found in the local file. [Source package, IFP to import/export, Class name]
        /// </summary>
        public ObjectResolutionDelegate MissingObjectResolver { get; set; }

        /// <summary>
        /// Invoked to fetch a VTable for a class from a donor object. Used to align VTables typically across games. [IFP to VTable list]
        /// </summary>
        public Func<string, List<string>> GetVTableFromDonor { get; set; }

        /// <summary>
        /// When doing bulk-compiles of non-base files, providing this can significantly improve performance.
        /// It must be an initialized FileLib for the last file in the game's base file list, as defined in <see cref="FileLib.BaseFileNames"/>
        /// </summary>
        public FileLib BaseFilesFileLib { get; set; }
    }
}
