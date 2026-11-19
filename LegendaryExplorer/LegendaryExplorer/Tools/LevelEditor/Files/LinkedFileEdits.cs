using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace LegendaryExplorer.Tools.LevelEditor;

internal enum LinkedFileCloseChoice { Save, Discard, Cancel }

internal sealed record LinkedFileSaveTarget(OpenLevelFile File, string Path);

/// <summary>Coordinates saving and closing files whose committed edits must be kept together.</summary>
internal sealed class LinkedFileEdits
{
    private readonly List<HashSet<OpenLevelFile>> groups = [];

    /// <summary>
    /// Register all affected files after applying an operation's changes to their packages.
    /// Overlapping operations form a single group until every member is saved or closed.
    /// </summary>
    internal void Register(params OpenLevelFile[] files)
    {
        var group = files.ToHashSet();
        if (group.Count < 2) return;
        foreach (var previous in groups.Where(g => g.Overlaps(group)).ToArray())
        {
            group.UnionWith(previous);
            groups.Remove(previous);
        }
        groups.Add(group);
    }

    internal IReadOnlyList<OpenLevelFile> GetLinkedFiles(OpenLevelFile file) =>
        groups.FirstOrDefault(g => g.Contains(file))?.ToArray() ?? [file];

    internal IEnumerable<OpenLevelFile> FilesWithPendingEdits =>
        groups.Where(g => g.Any(f => f.Package.IsModified)).SelectMany(g => g);

    internal void RemoveSavedGroups() => groups.RemoveAll(g => g.All(f => !f.Package.IsModified));

    internal void RemoveClosedGroups(ICollection<OpenLevelFile> openFiles) =>
        groups.RemoveAll(g => g.All(f => !openFiles.Contains(f)));

    internal void Clear() => groups.Clear();

    internal static IReadOnlyList<LinkedFileSaveTarget> PlanSaveAs(OpenLevelFile selected, IEnumerable<OpenLevelFile> group, string selectedPath)
    {
        string output = Path.GetFullPath(selectedPath);
        string directory = Path.GetDirectoryName(output);
        var targets = group.Append(selected).Distinct().Select(file => new LinkedFileSaveTarget(file,
            file == selected ? output : Path.Combine(directory, file.FileName))).ToArray();
        if (targets.Select(t => t.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count() != targets.Length)
            throw new InvalidOperationException("Two linked files would be saved to the same filename. Choose a different filename or resolve the duplicate package names before saving.");

        var originals = targets.Select(t => Path.GetFullPath(t.File.FilePath)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        bool allInPlace = targets.All(t => StringComparer.OrdinalIgnoreCase.Equals(t.Path, Path.GetFullPath(t.File.FilePath)));
        if (targets.Length > 1 && !allInPlace && targets.Any(t => originals.Contains(t.Path)))
            throw new InvalidOperationException("Saving this linked group here would overwrite part of the original package set. Choose a separate output folder so all original files remain unchanged.");
        // PackageSaver distinguishes Save from Save As by the exact path string, not filesystem identity.
        return targets.Select(t => StringComparer.OrdinalIgnoreCase.Equals(t.Path, Path.GetFullPath(t.File.FilePath))
            ? t with { Path = t.File.FilePath } : t).ToArray();
    }

    internal static async Task<bool> CloseGroupAsync(IReadOnlyList<OpenLevelFile> group, OpenLevelFile selected,
        LinkedFileCloseChoice choice, Func<Task<bool>> saveGroup, Action<OpenLevelFile> closeFile)
    {
        switch (choice)
        {
            case LinkedFileCloseChoice.Cancel:
                return false;
            case LinkedFileCloseChoice.Save:
                if (!await saveGroup()) return false;
                closeFile(selected);
                return true;
            case LinkedFileCloseChoice.Discard:
                // Discarding only one member would leave an incomplete operation saveable.
                foreach (var file in group) closeFile(file);
                return true;
            default:
                throw new ArgumentOutOfRangeException(nameof(choice));
        }
    }
}
