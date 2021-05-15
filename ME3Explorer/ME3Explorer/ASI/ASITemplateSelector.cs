using System.Windows;
using System.Windows.Controls;
using static ME3Explorer.ASI.ASIManager;

namespace ME3Explorer.ASI
{
    public class ASIDisplayTemplateSelector : DataTemplateSelector
    {
        public DataTemplate ASITemplate { get; set; }
        public DataTemplate NonManifestASITemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item is ASIMod)
                return ASITemplate;
            if (item is InstalledASIMod)
                return NonManifestASITemplate;

            return null;
        }
    }
}
