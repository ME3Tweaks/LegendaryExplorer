using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using LegendaryExplorer.Misc;
using LegendaryExplorer.UserControls.ExportLoaderControls.ScriptEditor.IDE;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;

//WIP

namespace LegendaryExplorer.UserControls.ExportLoaderControls.ScriptEditor
{
    /// <summary>
    /// Interaction logic for IdeThemePicker.xaml
    /// </summary>
    public partial class IdeThemePicker : NotifyPropertyChangedWindowBase
    {
        public ObservableCollection<ColorBinding> Colors { get; } = [];

        public IdeThemePicker(Window owner)
        {
            Colors.Add(new ColorBinding("Background", System.Windows.Media.Colors.Black));

            foreach (EF val in Enum.GetValues<EF>())
            {
                Colors.Add(new ColorBinding(val.ToString(), SyntaxInfo.ColorBrushes[val].Color));
            }

            InitializeComponent();
            Owner = owner;
        }


    }
    public class ColorBinding(string name, Color color) : NotifyPropertyChangedBase
    {
        public Color Color
        {
            get => color;
            set => SetProperty(ref color, value);
        }

        public string Name { get; } = name;
    }
}
