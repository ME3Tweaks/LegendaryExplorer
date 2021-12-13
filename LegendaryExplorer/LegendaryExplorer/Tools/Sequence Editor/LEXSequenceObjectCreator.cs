using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorer.Packages;
using LegendaryExplorerCore.Kismet;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;

namespace LegendaryExplorer.Tools.Sequence_Editor
{
    /// <summary>
    /// Wrapper for creating sequence objects and showing the relink results.
    /// </summary>
    public class LEXSequenceObjectCreator
    {
        /// <summary>
        /// Creates a sequence object, showing the relink results if any issues were encountered during creation. This should be used when creating objects from LEX.
        /// </summary>
        /// <param name="pcc"></param>
        /// <param name="className"></param>
        /// <param name="str"></param>
        /// <param name="cache"></param>
        /// <returns></returns>
        public static ExportEntry CreateSequenceObject(IMEPackage pcc, string className, PackageCache cache = null)
        {
            return SequenceObjectCreator.CreateSequenceObject(pcc, className, cache, x => EntryImporterExtended.ShowRelinkResultsIfAny(x.RelinkReport));
        }
    }
}
