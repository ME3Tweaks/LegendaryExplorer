using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using LegendaryExplorer.Misc;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.Tools.LiveLevelEditor.MatEd;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Textures;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.Classes;
using Microsoft.Win32;

namespace LegendaryExplorer.UserControls.ExportLoaderControls.MaterialEditor
{
    /// <summary>
    /// Material Editor Texture Parameter subclass
    /// </summary>
    class TextureParameterMatEd : TextureParameter, IMatEdTexture, INotifyPropertyChanged
    {

        private string _displayString;
        public Guid HostingControlGuid { get; set; }

        /// <summary>
        /// String to show for this texture
        /// </summary>
        public string DisplayString { get => _displayString; set => SetProperty(ref _displayString, value); }

        private ExportEntry _textureExp;
        /// <summary>
        /// The current texture export (can be resolved from import, so it may be in another package)
        /// </summary>
        public ExportEntry TextureExp { get => _textureExp; set => SetProperty(ref _textureExp, value); }

        private BitmapSource _loadedBitMap;
        public BitmapSource LoadedBitMap { get => _loadedBitMap; set => SetProperty(ref _loadedBitMap, value); }
        /// <summary>
        /// Texture reference as import when loaded (if any)
        /// </summary>
        public ImportEntry TextureImp { get; set; }

        public ICommand ReplaceTextureCommand { get; set; }

        /// <summary>
        /// Package reference to the package that replacements should be put into
        /// </summary>
        public IMEPackage EditingPackage { get; set; }

        private bool _isDefaultParameter;
        /// <summary>
        ///  If this parameter is from the BaseMaterial expressions list.
        /// </summary>
        public bool IsDefaultParameter { get => _isDefaultParameter; set => SetProperty(ref _isDefaultParameter, value); }

        public void LoadData(IMEPackage package, PackageCache cache)
        {
            ReplaceTextureCommand = new GenericCommand(ReplaceTexture);
            if (ParameterValue != 0)
            {
                MatEditorTextureLoader.InitTexture(this, package, ParameterValue, cache);
            }
        }

        /// <summary>
        /// Generates a <see cref="TextureParameterMatEd"/> object from the given material expression export 
        /// </summary>
        /// <param name="expression"></param>
        /// <returns></returns>
        public static TextureParameterMatEd FromExpression(ExportEntry expression)
        {
            TextureParameterMatEd te = new TextureParameterMatEd();
            var props = expression.GetProperties();
            te.ParameterName = props.GetProp<NameProperty>("ParameterName")?.Value.Instanced ?? "None";
            te.ParameterValue = props.GetProp<ObjectProperty>("Texture").Value; // This is the Object reference
            te.ExpressionGUID = props.GetProp<StructProperty>("ExpressionGUID");
            te.IsDefaultParameter = true;
            return te;
        }

        private void ReplaceTexture()
        {
            // Todo: Dialog for destination export
            // Todo: If new texture, need to clone existing export first
            //MessageBox.Show(null, "Replace the existing texture export a new texture or replace the existing one?", "Texture destination", MessageBoxButton.YesNoCancel, MessageBoxImage.Question, MessageBoxResult.Cancel);

            OpenFileDialog selectDDS = new OpenFileDialog
            {
                Title = "Select texture file",
                Filter = "All supported types|*.png;*.dds;*.tga|PNG files (*.png)|*.png|DDS files (*.dds)|*.dds|TGA files (*.tga)|*.tga",
                CustomPlaces = AppDirectories.GameCustomPlaces
            };
            var result = selectDDS.ShowDialog();
            if (result.HasValue && result.Value)
            {
                Task.Run(() =>
                {
                    //Check aspect ratios
                    var props = TextureExp.GetProperties();
                    var listedWidth = props.GetProp<IntProperty>("SizeX")?.Value ?? 0;
                    var listedHeight = props.GetProp<IntProperty>("SizeY")?.Value ?? 0;

                    Image image;
                    try
                    {
#if WINDOWS
                        image = Image.LoadFromFile(selectDDS.FileName, LegendaryExplorerCore.Textures.PixelFormat.ARGB);
#else
                    image = new Image(selectDDS.FileName);
#endif
                    }
                    catch (TextureSizeNotPowerOf2Exception)
                    {
                        MessageBox.Show("The width and height of a texture must both be a power of 2\n" +
                                        "(1, 2, 4, 8, 16, 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192)", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return null;
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show($"Error: {e.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return null;
                    }

                    if (image.mipMaps[0].origWidth / image.mipMaps[0].origHeight != listedWidth / listedHeight)
                    {
                        MessageBox.Show("Cannot replace texture: Aspect ratios must be the same.");
                        return null;
                    }

                    if (TextureExp.FileRef != EditingPackage)
                    {
                        // Needs moved to editing package
                        EntryExporter.ExportExportToPackage(TextureExp, EditingPackage, out var newExp); // Maybe use cache here
                        if (newExp is ImportEntry)
                        {
                            // Create a blank texture
                            newExp = Texture2D.CreateTexture(EditingPackage, TextureExp.ObjectName, image.mipMaps[0].width, image.mipMaps[0].height, image.pixelFormat, image.mipMaps.Count > 1);
                        }
                        TextureExp = newExp as ExportEntry; // Todo: Consider renaming to avoid memory collisions on saved package
                        ParameterValue = TextureExp.UIndex; // Update the texture UIndex for loading
                    }

                    var texture = new Texture2D(TextureExp);
                    return texture.Replace(image, props, isPackageStored: true);
                }).ContinueWithOnUIThread(x =>
                {
                    // IsBusy = false;
                    IsDefaultParameter = false; // Mark modified
                    PackageCache cache = new PackageCache();
                    MatEditorTextureLoader.InitTexture(this, TextureExp.FileRef, ParameterValue, cache);
                });
            }
        }
        public void ReplaceTexture(IMatEdTexture other)
        {
            PackageCache cache = new PackageCache();
            if (TextureExp.ClassName.CaseInsensitiveEquals("TextureCube"))
            {
                var otherprops = other.TextureExp.GetProperties();
                var myprops = TextureExp.GetProperties();

                // Replace the faces, but don't replace the cube, as the textures must be nested under it (I think?)
                // Not sure how to handle this - would require porting in new object and linking to it. Might be what we have to do.
                foreach (var oProp in otherprops.OfType<ObjectProperty>())
                {
                    var myProp = myprops.OfType<ObjectProperty>().FirstOrDefault(x => x.Name == oProp.Name);
                    var otherFace = oProp.ResolveToExport(other.TextureExp.FileRef, cache);
                    var myFaceFace = myProp.ResolveToExport(TextureExp.FileRef, cache);
                    EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.ReplaceSingularWithRelink, otherFace, TextureExp.FileRef, myFaceFace, true, new RelinkerOptionsPackage(), out _);
                }
                // EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.ReplaceSingularWithRelink, other.TextureExp, TextureExp.FileRef, TextureExp, true, new RelinkerOptionsPackage(), out _);
            }
            else
            {
                EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.ReplaceSingularWithRelink, other.TextureExp, TextureExp.FileRef, TextureExp, true, new RelinkerOptionsPackage(), out _);
                TextureExp.ObjectName = new NameReference(TextureExp.ObjectName.Name + $"_LLE{new Random().Next(5000)}", 0);
            }
            MatEditorTextureLoader.InitTexture(this, TextureExp.FileRef, TextureExp.UIndex, cache); // Reload the texture
            IsDefaultParameter = false;
        }

        #region PropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Notifies listeners when given property is updated.
        /// </summary>
        /// <param name="propertyname">Name of property to give notification for. If called in property, argument can be ignored as it will be default.</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyname = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyname));
        }

        /// <summary>
        /// Sets given property and notifies listeners of its change. IGNORES setting the property to same value.
        /// Should be called in property setters.
        /// </summary>
        /// <typeparam name="T">Type of given property.</typeparam>
        /// <param name="field">Backing field to update.</param>
        /// <param name="value">New value of property.</param>
        /// <param name="propertyName">Name of property.</param>
        /// <returns>True if success, false if backing field and new value aren't compatible.</returns>
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
        #endregion
    }
}
