using System;
using System.Collections.Generic;
using System.Linq;

namespace LegendaryExplorerCore.Packages
{
    /// <summary>
    /// Makes a group of edits to one or more packages all-or-nothing. Unless <see cref="Complete"/> is called before it's disposed
    /// (such as when an exception escapes a <c>using</c> block), every package is put back exactly as it was when the scope began:
    /// its names, imports and exports, including entries appended or removed (by trashing) since, with the same entry objects at the same indexes.
    /// Change notifications to the package's users are held until the scope completes, and never sent if it's abandoned.
    /// </summary>
    /// <remarks>
    /// Edits must be made synchronously on one thread while the scope is open, and the packages can't be saved meanwhile.
    /// Rolling back restores package state only: objects built from the packages (such as parsed binaries or property collections) aren't updated.
    /// </remarks>
    public sealed class PackageEditScope : IDisposable
    {
        private readonly UnrealPackageFile[] packages;
        private bool finished;

        private PackageEditScope(UnrealPackageFile[] packages) => this.packages = packages;

        public static PackageEditScope Begin(params IEnumerable<IMEPackage> packages)
        {
            var distinct = packages.Cast<UnrealPackageFile>().Distinct().ToArray();
            int begun = 0;
            try
            {
                for (; begun < distinct.Length; begun++)
                {
                    distinct[begun].BeginEditScope();
                }
            }
            catch
            {
                for (int i = 0; i < begun; i++)
                {
                    distinct[i].RollBackEditScope();
                }
                throw;
            }
            return new PackageEditScope(distinct);
        }

        /// <summary>
        /// Keeps the edits, and sends the change notifications that were held back
        /// </summary>
        public void Complete()
        {
            ObjectDisposedException.ThrowIf(finished, this);
            finished = true;
            foreach (UnrealPackageFile package in packages)
            {
                package.CompleteEditScope();
            }
        }

        /// <summary>
        /// Abandons the edits, unless the scope was completed
        /// </summary>
        public void Dispose()
        {
            if (finished) return;
            finished = true;
            foreach (UnrealPackageFile package in packages)
            {
                package.RollBackEditScope();
            }
        }
    }
}
