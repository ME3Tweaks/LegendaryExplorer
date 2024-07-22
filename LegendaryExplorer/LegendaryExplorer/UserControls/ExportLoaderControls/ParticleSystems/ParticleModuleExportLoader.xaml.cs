using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using System.Numerics;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using Color = System.Windows.Media.Color;

namespace LegendaryExplorer.UserControls.ExportLoaderControls
{
    /// <summary>
    /// Interaction logic for ParticleModuleExportLoader.xaml
    /// </summary>
    public partial class ParticleModuleExportLoader : ExportLoaderControl
    {
        public ParticleModuleExportLoader() : base("Particle Module Viewer")
        {
            DataContext = this;
            LoadCommands();
            InitializeComponent();
        }

        private void LoadCommands()
        {
        }

        public override bool CanParse(ExportEntry exportEntry) => exportEntry.IsA("ParticleModule");

        public override void LoadExport(ExportEntry exportEntry)
        {
            CurrentLoadedExport = exportEntry;
            DistributionVectors.ClearEx();
            DistributionFloats.ClearEx();
            var props = exportEntry.GetProperties();

            var structs = props.Where(x => x is StructProperty sp && (sp.StructType is "RawDistributionVector" or "RawDistributionFloat" or "BioRawDistributionRwVector3")).Select(x => x as StructProperty);

            foreach (var sp in structs)
            {
                if (sp.StructType == "RawDistributionVector")
                {
                    var prop = sp.Name.Name; // e.g. ColorOverLife
                    var lookupTable = sp.GetProp<ArrayProperty<FloatProperty>>("LookupTable");
                    if (lookupTable != null && lookupTable.Any())
                    {
                        float min = lookupTable[0];
                        float max = lookupTable[1];

                        int index = 2;
                        List<Vector3> vectors = new List<Vector3>();
                        while (index < lookupTable.Count)
                        {
                            Vector3 v = new Vector3(lookupTable[index], lookupTable[index + 1], lookupTable[index + 2]);
                            vectors.Add(v);
                            index += 3;
                        }

                        DistributionVector dv = new DistributionVector
                        {
                            HasLookupTable = true,
                            MinValue = min,
                            MaxValue = max,
                            Property = sp,
                            PropertyName = sp.Name.Name,
                        };
                        dv.Vectors.ReplaceAll(vectors.Select(v => new UIVector { Vector = v }));
                        dv.SetupUIProps();
                        DistributionVectors.Add(dv);
                    }
                }
                if (sp.StructType == "RawDistributionFloat")
                {
                    var prop = sp.Name.Name; // e.g. ColorOverLife
                    var lookupTable = sp.GetProp<ArrayProperty<FloatProperty>>("LookupTable");
                    if (lookupTable != null && lookupTable.Any())
                    {
                        float min = lookupTable[0];
                        float max = lookupTable[1];

                        DistributionFloat df = new DistributionFloat
                        {
                            HasLookupTable = true,
                            MinValue = min,
                            MaxValue = max,
                            Property = sp,
                            PropertyName = sp.Name.Name,
                        };
                        //i'm lazy
                        lookupTable.RemoveAt(0);
                        lookupTable.RemoveAt(0);

                        df.Floats.ReplaceAll(lookupTable.Select(x => x.Value));
                        DistributionFloats.Add(df);
                    }
                }

                // LE uses these
                if (sp.StructType == "BioRawDistributionRwVector3")
                {
                    var prop = sp.Name.Name; // e.g. ColorOverLife
                    var lookupTable = sp.GetProp<ArrayProperty<StructProperty>>("LookupTable");
                    if (lookupTable != null && lookupTable.Any())
                    {
                        List<Vector3> vectors = new List<Vector3>();
                        float min = sp.Properties.GetProp<FloatProperty>("LookupTableMinOut");
                        float max = sp.Properties.GetProp<FloatProperty>("LookupTableMaxOut");

                        foreach (var vprop in lookupTable)
                        {
                            Vector3 v = new Vector3(vprop.Properties.GetProp<FloatProperty>("X"),
                                vprop.Properties.GetProp<FloatProperty>("Y"),
                                vprop.Properties.GetProp<FloatProperty>("Z"));
                            vectors.Add(v);
                        }

                        DistributionVector dv = new DistributionVector
                        {
                            HasLookupTable = true,
                            MinValue = min,
                            MaxValue = max,
                            Property = sp,
                            PropertyName = sp.Name.Name,
                            IsRwType = true
                        };
                        dv.Vectors.ReplaceAll(vectors.Select(v => new UIVector { Vector = v }));
                        dv.SetupUIProps();
                        DistributionVectors.Add(dv);
                    }
                }
            }
        }

        public override void UnloadExport()
        {
        }

        public override void PopOut()
        {
            if (CurrentLoadedExport != null)
            {
                ExportLoaderHostedWindow elhw = new ExportLoaderHostedWindow(new ParticleModuleExportLoader(), CurrentLoadedExport)
                {
                    Title = $"Particle Module Viewer - {CurrentLoadedExport.UIndex} {CurrentLoadedExport.InstancedFullPath} - {CurrentLoadedExport.FileRef.FilePath}"
                };
                elhw.Show();
            }
        }

        public override void Dispose()
        {
        }

        public ObservableCollectionExtended<DistributionVector> DistributionVectors { get; } = new ObservableCollectionExtended<DistributionVector>();
        public class DistributionVector
        {
            public string PropertyName { get; set; }
            public StructProperty Property { get; set; }
            public bool HasLookupTable { get; set; }
            public float MinValue { get; set; }
            public float MaxValue { get; set; }
            public ObservableCollectionExtended<UIVector> Vectors { get; } = new ObservableCollectionExtended<UIVector>();
            /// <summary>
            /// LE uses RwTypes, sometimes
            /// </summary>
            public bool IsRwType { get; set; }

            public void SetupUIProps()
            {
                if (PropertyName.Contains("Color"))
                {
                    foreach (var v in Vectors)
                    {
                        v.IsColor = true;

                        var colorR = v.Vector.X;
                        var colorG = v.Vector.Y;
                        var colorB = v.Vector.Z;
                        if (MaxValue > 1)
                        {
                            colorR = colorR * 1 / MaxValue;
                            colorG = colorG * 1 / MaxValue;
                            colorB = colorB * 1 / MaxValue;
                        }
                        colorR = Math.Min(colorR * 255, 255);
                        colorG = Math.Min(colorG * 255, 255);
                        colorB = Math.Min(colorB * 255, 255);
                        v.DisplayedColor = new SolidColorBrush(Color.FromRgb((byte)colorR, (byte)colorG, (byte)colorB));
                    }
                }
            }
        }

        public class UIVector
        {
            public Vector3 Vector { get; set; }
            public bool IsColor { get; set; }
            public Brush DisplayedColor { get; set; }
            public string XText
            {
                get
                {
                    if (IsColor) return "R: " + Vector.X;
                    return "X: " + Vector.X;
                }
            }
            public string YText
            {
                get
                {
                    if (IsColor) return "G: " + Vector.Y;
                    return "Y: " + Vector.Y;
                }
            }
            public string ZText
            {
                get
                {
                    if (IsColor) return "B: " + Vector.Z;
                    return "Z: " + Vector.X;
                }
            }
        }

        public ObservableCollectionExtended<DistributionFloat> DistributionFloats { get; } = new ObservableCollectionExtended<DistributionFloat>();
        public class DistributionFloat
        {
            public string PropertyName { get; set; }
            public StructProperty Property { get; set; }
            public bool HasLookupTable { get; set; }
            public float MinValue { get; set; }
            public float MaxValue { get; set; }
            public ObservableCollectionExtended<float> Floats { get; } = new ObservableCollectionExtended<float>();
        }
    }
}
