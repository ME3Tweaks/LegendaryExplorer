using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using LegendaryExplorer.Misc;
using LegendaryExplorerCore.Unreal;
using Microsoft.Win32;

namespace LegendaryExplorer.ToolsetDev;

/// <summary>
/// Interaction logic for PSAViewerWindow.xaml
/// </summary>
public partial class PSAViewerWindow : NotifyPropertyChangedWindowBase
{
    private PSA psa;
    public PSAViewerWindow()
    {
        DataContext = this;
        InitializeComponent();
        LoadRecentList();
        RefreshRecent();
    }
    private void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        var d = new OpenFileDialog
        {
            Filter = "*.psa|*.psa",
            CustomPlaces = AppDirectories.GameCustomPlaces
        };
        if (d.ShowDialog() == true)
        {
            LoadFile(d.FileName);
        }
    }

    private void LoadFile(string fileName)
    {
        Title = "PSA Viewer - " + fileName;
        AddRecent(fileName, false);
        SaveRecentList();
        RefreshRecent();

        psa = PSA.FromFile(fileName);

        var sb = new StringBuilder();
        sb.AppendLine("=========");
        sb.AppendLine("BONENAMES");
        sb.AppendLine("=========");
        foreach (PSA.PSABone bone in psa.Bones)
        {
            sb.AppendLine($"{bone.Name}: Flags({bone.Flags}), NumChildren({bone.NumChildren}), ParentIndex({bone.ParentIndex}), Rotation({bone.Rotation}), Position({bone.Position}), Length({bone.Length}), Size({bone.Size})");
        }
        sb.AppendLine("========");
        sb.AppendLine("ANIMINFO");
        sb.AppendLine("========");
        
        foreach (PSA.PSAAnimInfo info in psa.Infos)
        {
            sb.AppendLine($"{info.Name}: Group({info.Group}), TotalBones({info.TotalBones}), RootInclude{info.RootInclude}), KeyCompressionStyle({info.KeyCompressionStyle}), KeyQuotum({info.KeyQuotum})," +
                          $" KeyReduction({info.KeyReduction}), TrackTime({info.TrackTime}), AnimRate({info.AnimRate}), StartBone({info.StartBone}), FirstRawFrame({info.FirstRawFrame}), NumRawFrames({info.NumRawFrames})");
        }
        sb.AppendLine("========");
        sb.AppendLine("ANIMKEYS");
        sb.AppendLine("========");
        
        foreach (PSA.PSAAnimKeys key in psa.Keys)
        {
            sb.AppendLine($"Position({key.Position}), Rotation{key.Rotation}), Time({key.Time})");
        }
        PsaTextBox.Text = sb.ToString();
    }

    #region Recents

    private List<string> RFiles;
    private readonly string PSAViewerDataFolder = Path.Combine(AppDirectories.AppDataFolder, @"PSAViewer\");
    private const string RECENTFILES_FILE = "RECENTFILES";
    private void LoadRecentList()
    {
        Recents_MenuItem.IsEnabled = false;
        RFiles = new List<string>();
        string path = PSAViewerDataFolder + RECENTFILES_FILE;
        if (File.Exists(path))
        {
            string[] recents = File.ReadAllLines(path);
            foreach (string recent in recents)
            {
                if (File.Exists(recent))
                {
                    AddRecent(recent, true);
                }
            }
        }
    }

    private void SaveRecentList()
    {
        if (!Directory.Exists(PSAViewerDataFolder))
        {
            Directory.CreateDirectory(PSAViewerDataFolder);
        }
        string path = PSAViewerDataFolder + RECENTFILES_FILE;
        if (File.Exists(path))
            File.Delete(path);
        File.WriteAllLines(path, RFiles);
    }

    public void RefreshRecent()
    {
        Recents_MenuItem.Items.Clear();
        if (RFiles.Count <= 0)
        {
            Recents_MenuItem.IsEnabled = false;
            return;
        }
        Recents_MenuItem.IsEnabled = true;

        int i = 0;
        foreach (string filepath in RFiles)
        {
            var fr = new MenuItem
            {
                Header = filepath.Replace("_", "__"),
                Tag = filepath
            };
            fr.Click += RecentFile_click;
            Recents_MenuItem.Items.Add(fr);
            i++;
        }
    }

    private void RecentFile_click(object sender, EventArgs e)
    {
        string s = ((FrameworkElement)sender).Tag.ToString();
        if (File.Exists(s))
        {
            LoadFile(s);
        }
        else
        {
            MessageBox.Show("File does not exist: " + s);
        }
    }

    public void AddRecent(string s, bool loadingList)
    {
        RFiles = RFiles.Where(x => !x.Equals(s, StringComparison.InvariantCultureIgnoreCase)).ToList();
        if (loadingList)
        {
            RFiles.Add(s); //in order
        }
        else
        {
            RFiles.Insert(0, s); //put at front
        }
        if (RFiles.Count > 10)
        {
            RFiles.RemoveRange(10, RFiles.Count - 10);
        }
        Recents_MenuItem.IsEnabled = true;
    }

    #endregion

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            // Note that you can have more than one file.
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

            // Assuming you have one file that you care about, pass it off to whatever
            // handling code you have defined.
            LoadFile(files[0]);
        }
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
        }
    }
}