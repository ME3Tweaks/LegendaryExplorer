using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace LegendaryExplorerCore.Packages
{
    public abstract partial class UnrealPackageFile
    {
        /// <summary>
        /// The package as it was when a <see cref="PackageEditScope"/> opened, and the change notifications held back since
        /// </summary>
        private sealed class EditScopeState
        {
            public readonly List<string> Names;
            public readonly uint NamesAdded;
            public readonly bool IsModified;
            public readonly ExportEntry.EditState[] Exports;
            public readonly ImportEntry.EditState[] Imports;
            public readonly HashSet<PackageUpdate> Updates = [];

            public EditScopeState(UnrealPackageFile package)
            {
                Names = package.names.ToList();
                NamesAdded = package.namesAdded;
                IsModified = package.IsModified;
                Exports = package.exports.Select(e => new ExportEntry.EditState(e)).ToArray();
                Imports = package.imports.Select(i => new ImportEntry.EditState(i)).ToArray();
            }
        }

        private EditScopeState editScope;

        /// <summary>
        /// Whether a <see cref="PackageEditScope"/> is open on this package. It can't be saved meanwhile
        /// </summary>
        public bool IsInEditScope => editScope is not null;

        internal void BeginEditScope()
        {
            if (editScope is not null)
            {
                throw new InvalidOperationException($"{FileNameNoExtension} is already in an edit scope. Scopes don't nest");
            }
            editScope = new EditScopeState(this);
        }

        /// <summary>
        /// Keeps the scope's edits, and sends the change notifications it held back
        /// </summary>
        internal void CompleteEditScope()
        {
            EditScopeState state = editScope;
            editScope = null;
            if (state is null) return;
            foreach (PackageUpdate update in state.Updates)
            {
                UpdateTools(update.Change, update.Index);
            }
        }

        /// <summary>
        /// Puts the names, imports and exports back as they were when the scope opened: the same entry objects, at the same indexes,
        /// with the same headers and data. The scope's change notifications are never sent
        /// </summary>
        internal void RollBackEditScope()
        {
            EditScopeState state = editScope;
            editScope = null;
            if (state is null) return;

            names = state.Names;
            mapNames();
            NameCount = names.Count;
            namesAdded = state.NamesAdded;

            var restored = new List<IEntry>();
            RestoreTable(exports, state.Exports.Select(s => s.Entry), e => e.PropertyChanged -= exportChanged, e => e.PropertyChanged += exportChanged);
            foreach (ExportEntry.EditState s in state.Exports)
            {
                if (s.Restore()) restored.Add(s.Entry);
            }
            ExportCount = exports.Count;
            RestoreTable(imports, state.Imports.Select(s => s.Entry), i => i.PropertyChanged -= importChanged, i => i.PropertyChanged += importChanged);
            foreach (ImportEntry.EditState s in state.Imports)
            {
                if (s.Restore()) restored.Add(s.Entry);
            }
            ImportCount = imports.Count;

            IsModified = state.IsModified;
            //names and links may have changed, so instanced paths may have too
            lookupTableNeedsToBeRegenerated = true;

            foreach (IEntry entry in restored)
            {
                if (entry is ExportEntry export) export.NotifyRestored();
                else ((ImportEntry)entry).NotifyRestored();
            }
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NameCount)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ImportCount)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ExportCount)));
        }

        /// <summary>
        /// Restores a table's entries, keeping the package subscribed to exactly the entries in it
        /// </summary>
        private static void RestoreTable<T>(List<T> table, IEnumerable<T> original, Action<T> unsubscribe, Action<T> subscribe) where T : class
        {
            var current = new HashSet<T>(table, ReferenceEqualityComparer.Instance);
            var kept = original.ToList();
            var keptSet = new HashSet<T>(kept, ReferenceEqualityComparer.Instance);
            foreach (T entry in table.Where(e => !keptSet.Contains(e)))
            {
                unsubscribe(entry);
            }
            foreach (T entry in kept.Where(e => !current.Contains(e)))
            {
                subscribe(entry);
            }
            table.Clear();
            table.AddRange(kept);
        }

        internal void ThrowIfInEditScope()
        {
            if (editScope is not null)
            {
                throw new InvalidOperationException($"{FileNameNoExtension} can't be saved while an edit scope is open on it");
            }
        }
    }
}
