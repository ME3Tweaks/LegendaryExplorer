using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace ME3Explorer
{
    public class Tool
    {
        public string name { get; set; }
        public ImageSource icon { get; set; }
        public Action open { get; set; }
        public List<string> tags;
    }

    public static class Tools
    {
        public static List<Tool> items;

        public static void InitializeTools()
        {
            List<Tool> list = new List<Tool>();

            //Utilities
            list.Add(new Tool
            {
                name = "Audio Extractor",
                icon = Application.Current.FindResource("iconAudioExtractor") as ImageSource,
                open = () =>
                {
                    (new AFCExtract()).Show();
                },
                tags = new List<string> { "utility", "afc", "audio"}
            });
            list.Add(new Tool
            {
                name = "Bik Extractor",
                icon = Application.Current.FindResource("iconBikExtractor") as ImageSource,
                open = () =>
                {
                    (new BIKExtract()).Show();
                },
                tags = new List<string> { "utility", "bik", "movie"}
            });

            //Create Mods
            list.Add(new Tool
            {
                name = "Package Editor",
                icon = Application.Current.FindResource("iconPackageEditor") as ImageSource,
                open = () =>
                {
                    (new PackageEditor()).Show();
                },
                tags = new List<string> { "developer", "pcc" }
            });
            list.Add(new Tool
            {
                name = "Sequence Editor",
                icon = Application.Current.FindResource("iconSequenceEditor") as ImageSource,
                open = () =>
                {
                    (new SequenceEditor()).Show();
                },
                tags = new List<string> { "developer", "kismet" }
            });

            items = list;
        }
    }
}