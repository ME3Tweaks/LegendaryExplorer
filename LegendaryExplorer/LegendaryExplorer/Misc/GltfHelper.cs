using LegendaryExplorer.Dialogs;
using LegendaryExplorer.SharedUI.Bases;
using LegendaryExplorer.UserControls.ExportLoaderControls;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using Microsoft.Win32;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace LegendaryExplorer.Misc
{
    /// <summary>
    /// Wraps the GLTF class in LEC to make the functionality available to various parts of the user interface
    /// </summary>
    public static class GltfHelper
    {
        public static bool CanExportMeshToGltf(WPFBase owningWindow, IMEPackage package, IEntry selectedEntry)
        {
            // TODO is the selectedEntry a valid type?
            // are experiments enabled?
            return true;
        }
        public static void ExportMeshToGltf(WPFBase owningWindow, MeshRenderer meshRenderer, IMEPackage package, IEntry selectedEntry, GLTF.MaterialExportLevel materialExportLevel = GLTF.MaterialExportLevel.NameOnly)
        {
            if (package == null)
            {
                return;
            }
            if (package.Game == MEGame.ME1)
            {
                ShowError("This experiment does not yet support OT1; if you must do this, port it to another game first");
                return;
            }
            if (package.Game == MEGame.UDK)
            {
                ShowError("This experiment does not support UDK files;");
                return;
            }
            if (FilterSelectedItem(selectedEntry, ["SkeletalMesh", "StaticMesh", "SkeletalMeshComponent", "BioPawn", "SFXStuntActor", "SkeletalMeshActor"], out var export))
            {
                if (export.ClassName == "StaticMesh" && !(package.Game.IsGame3() || package.Game.IsLEGame()))
                {
                    ShowError("This experiment does not yet support OT1 or OT2 for static meshes.");
                    return;
                }
                var d = new SaveFileDialog { Filter = "glTF binary|*.glb|glTF|*.glTF", FileName = $"{selectedEntry.ObjectName.Instanced}.glb" };
                if (d.ShowDialog() == true)
                {
                    Task.Run(() =>
                    {
                        if (owningWindow != null)
                        {
                            owningWindow.BusyText = "Exporting to glTF...";
                            owningWindow.IsBusy = true;
                        }
                        else
                        {
                            meshRenderer?.BusyText = "Exporting to glTF...";
                            meshRenderer?.IsBusy = true;
                        }
                        GLTF.ExportMeshToGltf(export, d.FileName, materialExportLevel, $"Legendary Explorer {AppVersion.DisplayedVersion}");
                    }).ContinueWithOnUIThread(x =>
                    {
                        if (owningWindow != null)
                        {
                            owningWindow?.IsBusy = false;
                        }
                        else
                        {
                            meshRenderer?.IsBusy = false;
                        }
                        if (x.Exception != null)
                        {
                            ShowError(x.Exception.FlattenException());
                        }
                    });
                }
            }
            else
            {
                ShowError("You must select a skeletal mesh, static mesh, SkeletalMeshComponent, SFXStuntActor, SkeletalMeshActor, or BioPawn");
            }
        }

        public static void ReplaceFromGltf(WPFBase window, IEntry selectedEntry)
        {
            if (window.Pcc == null)
            {
                return;
            }
            if (window.Pcc.Game == MEGame.ME1)
            {
                ShowError("This experiment does not yet support OT1; if you must do this, import it into another game and port it to OT1");
            }
            if (window.Pcc.Game == MEGame.UDK)
            {
                ShowError("This experiment does not support UDK files;");
            }
            if (GetGltfFromFile(out var gltf, out string _))
            {
                FilterSelectedItem(selectedEntry, ["SkeletalMesh", "StaticMesh"], out ExportEntry selectedMeshToReplace);
                GLTF.QueryMeshes(gltf, out var skeletalMeshes, out var staticMeshes);
                string specificMesh = null;
                if (selectedMeshToReplace.ClassName == "SkeletalMesh")
                {
                    var meshCount = skeletalMeshes.Count();
                    if (meshCount == 0)
                    {
                        ShowError("You are trying to replace a skeletal mesh but the glTF file does not contain any skeletal meshes.");
                        return;
                    }
                    else if (meshCount > 1)
                    {
                        var prompt = new DropdownPromptDialog("Select which mesh to use to replace your mesh.",
                            "Select mesh", "Mesh", skeletalMeshes, window);
                        prompt.ShowDialog();
                        if (prompt.DialogResult == true)
                        {
                            specificMesh = prompt.Response;
                        }
                        else { return; }
                    }
                }
                else if (selectedMeshToReplace.ClassName == "StaticMesh")
                {
                    var meshCount = staticMeshes.Count();
                    if (meshCount == 0)
                    {
                        ShowError("You are trying to replace a static mesh but the glTF file does not contain any static meshes.");
                        return;
                    }
                    else if (meshCount > 1)
                    {
                        var prompt = new DropdownPromptDialog("Select which mesh to use to replace your mesh.",
                        "Select mesh", "Mesh", staticMeshes, window);
                        prompt.ShowDialog();
                        if (prompt.DialogResult == true)
                        {
                            specificMesh = prompt.Response;
                        }
                        else { return; }
                    }
                }
                GLTF.ConvertGltfToMesh(gltf, window.Pcc, selectedMeshToReplace, specificMesh);
            }
        }

        public static void ImportNewFromGltf(WPFBase window)
        {
            if (window.Pcc == null)
            {
                return;
            }
            if (window.Pcc.Game == MEGame.ME1)
            {
                ShowError("This experiment does not yet support OT1; if you must do this, import it into another game and port it to OT1");
            }
            if (window.Pcc.Game == MEGame.UDK)
            {
                ShowError("This experiment does not support UDK files;");
            }
            if (GetGltfFromFile(out var gltf, out string _))
            {
                GLTF.QueryMeshes(gltf, out var skeletalMeshes, out var staticMeshes);
                if (!skeletalMeshes.Any() && !staticMeshes.Any())
                {
                    ShowError("The gltf you are trying to import does not contain any meshes.");
                    return;
                }
                GLTF.ConvertGltfToMesh(gltf, window.Pcc);
            }
        }

        private static bool FilterSelectedItem(IEntry selectedItem, string[] expectedTypes, out ExportEntry entry)
        {
            entry = null;
            if (selectedItem == null)
            {
                return false;
            }

            foreach (var expectedType in expectedTypes)
            {
                if (selectedItem.IsA(expectedType))
                {
                    entry = (ExportEntry)selectedItem;
                    return entry != null;
                }
            }
            return false;
        }

        private static bool GetGltfFromFile(out SharpGLTF.Schema2.ModelRoot gltf, out string filePath)
        {
            var d = new OpenFileDialog
            {
                Filter = "gLTF|*.gltf;*.glb",
                Title = "Select a gLTF or glb file"
            };
            if (d.ShowDialog() == true)
            {
                filePath = d.FileName;
                gltf = SharpGLTF.Schema2.ModelRoot.Load(filePath, SharpGLTF.Validation.ValidationMode.Skip);
                return true;
            }

            gltf = null;
            filePath = null;
            return false;
        }

        private static void ShowError(string errMsg)
        {
            MessageBox.Show(errMsg, "Warning", MessageBoxButton.OK);
        }
    }
}
