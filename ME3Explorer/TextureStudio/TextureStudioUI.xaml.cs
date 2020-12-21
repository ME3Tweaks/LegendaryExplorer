using System;
using System.IO;
using System.Threading;
using System.Windows;
using ME3ExplorerCore.Compression;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.Packages;

namespace ME3Explorer.TextureStudio
{
    /// <summary>
    /// Interaction logic for TextureStudioUI.xaml
    /// </summary>
    public partial class TextureStudioUI : Window
    {
        public TextureStudioUI()
        {
            InitializeComponent();
        }

        private void LTM(object sender, RoutedEventArgs e)
        {
            MEMTextureMap.LoadTextureMap(MEGame.ME3);

        }
    }
}
