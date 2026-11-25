using LegendaryExplorer.Tools.AssetDatabase;
using LegendaryExplorer.Lightmaps.Lightmass;
using LegendaryExplorer.Misc;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.SharedUI.Bases;
using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using Microsoft.Win32;
using LegendaryExplorer.Tools.PackageEditor;
using LegendaryExplorer.UserControls.ExportLoaderControls;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>
/// Interaction logic for LevelEditor.xaml
/// </summary>
public class RecentFileSet
{
    public MEGame Game { get; set; }
    public List<string> FilePaths { get; set; } = [];
    public List<string> ReadOnlyFilePaths { get; set; } = [];

    [JsonIgnore]
    public string DisplayName => FilePaths.Count switch
    {
        0 => "(empty)",
        1 => Path.GetFileName(FilePaths[0]),
        _ => $"{Path.GetFileName(FilePaths[0])} (+{FilePaths.Count - 1} more)"
    };

    /// <summary>Where the camera was when the set was last closed. Null for a set never closed since this was recorded</summary>
    public RecentCameraView Camera { get; set; }

    /// <summary>Where the first file is: Basegame, the DLC folder it's in, or its directory if it's outside the game</summary>
    [JsonIgnore]
    public string LocationText => FilePaths.Count == 0 ? "" : DescribeLocation(Game, FilePaths[0]);

    [JsonIgnore]
    public string TooltipText => string.Join("\n", FilePaths);

    private static string DescribeLocation(MEGame game, string filePath)
    {
        string directory = Path.GetDirectoryName(filePath);
        if (game.IsLEGame())
        {
            if (IsUnder(directory, MEDirectories.GetCookedPath(game))) return "Basegame";
            string dlcPath = MEDirectories.GetDLCPath(game);
            if (IsUnder(directory, dlcPath))
                return Path.GetRelativePath(dlcPath, directory).Split(Path.DirectorySeparatorChar)[0];
        }
        return directory;
    }

    private static bool IsUnder(string directory, string root)
    {
        if (string.IsNullOrEmpty(root)) return false;
        string relative = Path.GetRelativePath(root, directory);
        return relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar) && !Path.IsPathRooted(relative);
    }
}

/// <summary>The perspective camera's eye and rotation (radians), as <see cref="Scene3D.SceneCamera"/> has them</summary>
public class RecentCameraView
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float Pitch { get; set; }
    public float Yaw { get; set; }
}

public partial class LevelEditor
{
    private const int MaxRecentSets = 20;

    public ObservableCollectionExtended<RecentFileSet> RecentSets { get; } = [];

    /// <summary>The camera of the recent set being opened, which its first file starts at instead of the level's own view</summary>
    private RecentCameraView pendingRecentCamera;

    private static string RecentSetsFile => Path.Combine(
        Directory.CreateDirectory(Path.Combine(AppDirectories.AppDataFolder, "LevelEditor")).FullName,
        "RECENTSETS");

    private void LoadRecentSets()
    {
        if (!File.Exists(RecentSetsFile)) return;
        try
        {
            var json = File.ReadAllText(RecentSetsFile);
            var sets = JsonConvert.DeserializeObject<List<RecentFileSet>>(json);
            if (sets is null) return;
            foreach (var set in sets)
            {
                set.FilePaths.RemoveAll(p => !File.Exists(p));
                if (set.FilePaths.Count > 0)
                    RecentSets.Add(set);
            }
        }
        catch { /* corrupt file, ignore */ }
        RefreshRecentsMenu();
    }

    private void SaveRecentSets()
    {
        var json = JsonConvert.SerializeObject(RecentSets.ToList(), Formatting.Indented);
        File.WriteAllText(RecentSetsFile, json);
        RefreshRecentsMenu();
    }

    private void RecordCurrentFilesAsRecent()
    {
        if (OpenFiles.Count == 0) return;
        var currentPaths = OpenFiles.Select(f => f.FilePath).ToList();

        //(the set keeps its camera until it's closed again)
        RecentCameraView camera = FindRecentSet(currentPaths[0])?.Camera;
        RecentSets.RemoveAll(set => set.FilePaths.Count > 0 && IsSameFile(set.FilePaths[0], currentPaths[0]));

        RecentSets.Insert(0, new RecentFileSet
        {
            Game = Game,
            FilePaths = currentPaths,
            ReadOnlyFilePaths = OpenFiles.Where(f => f.IsReadOnly).Select(f => f.FilePath).ToList(),
            Camera = camera
        });

        while (RecentSets.Count > MaxRecentSets)
            RecentSets.RemoveAt(RecentSets.Count - 1);

        SaveRecentSets();
    }

    private RecentFileSet FindRecentSet(string firstFilePath) =>
        RecentSets.FirstOrDefault(set => set.FilePaths.Count > 0 && IsSameFile(set.FilePaths[0], firstFilePath));

    private static bool IsSameFile(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    /// <summary>Stores where the camera is with the open files' recent set, which reopening it from Recent returns to</summary>
    private void RememberCameraInRecents()
    {
        if (IsBakePreview || OpenFiles.Count == 0 || FindRecentSet(OpenFiles[0].FilePath) is not { } set) return;
        (Vector3 position, float pitch, float yaw) = RenderContext.Camera.PerspectiveView;
        set.Camera = new RecentCameraView { X = position.X, Y = position.Y, Z = position.Z, Pitch = pitch, Yaw = yaw };
        SaveRecentSets();
    }

    private async void OpenRecentFileSet(RecentFileSet set)
    {
        if (!ConfirmCloseFiles("Open the new files anyway?")) return;
        //(this stores the camera of the files open now, which may be this set)
        CloseAllFiles();

        using var guard = new RenderGuard(this);

        pendingRecentCamera = set.Camera;
        try
        {
            var paths = set.FilePaths.Where(File.Exists).ToList();
            for (int i = 0; i < paths.Count; i++)
            {
                string path = paths[i];
                await AddLevelFile(path, i + 1, paths.Count).ConfigureAwait(true);
                var openFile = OpenFiles.LastOrDefault(f => f.FilePath == path);
                if (openFile is not null && set.ReadOnlyFilePaths.Contains(path))
                    openFile.IsReadOnly = true;
            }
        }
        finally
        {
            pendingRecentCamera = null;
        }
    }

    private void RefreshRecentsMenu()
    {
        Recents_MenuItem.Items.Clear();
        Recents_MenuItem.IsEnabled = RecentSets.Count > 0;
        foreach (var set in RecentSets)
        {
            var mi = new MenuItem
            {
                Header = $"{set.DisplayName}  ({set.LocationText})".Replace("_", "__"),
                ToolTip = set.TooltipText,
                Tag = set
            };
            mi.Click += (_, _) => OpenRecentFileSet((RecentFileSet)mi.Tag);
            Recents_MenuItem.Items.Add(mi);
        }
    }
}
