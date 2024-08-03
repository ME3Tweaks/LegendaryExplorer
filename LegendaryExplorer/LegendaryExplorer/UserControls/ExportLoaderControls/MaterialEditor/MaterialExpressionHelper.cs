using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using LegendaryExplorer.Misc;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;

// The majority of this class is from ME2R with some light edits.
// I don't want to have to reinvent the wheel parsing this out
// Also I don't like structs
// Mgamerz
namespace LegendaryExplorer.UserControls.ExportLoaderControls.MaterialEditor
{
    public abstract class ExpressionParameter : NotifyPropertyChangedBase
    {
        /// <summary>
        /// Whole property that was read in
        /// </summary>
        public StructProperty Property { get; set; }

        /// <summary>
        /// GUID expression
        /// </summary>
        public StructProperty ExpressionGUID { get; set; }

        /// <summary>
        /// Parameter name
        /// </summary>
        public string ParameterName { get; set; }
    }
    public class ScalarParameter : ExpressionParameter
    {
        public static List<ScalarParameter> GetScalarParameters(ExportEntry export, bool returnNullOnNotFound = false, Func<ScalarParameter> generator = null)
        {
            var scalars = export.GetProperty<ArrayProperty<StructProperty>>("ScalarParameterValues");
            if (scalars == null)
                return returnNullOnNotFound ? null : new List<ScalarParameter>();
            return scalars?.Select(x => FromStruct(x, generator)).ToList();
        }

        public static void WriteScalarParameters(ExportEntry export, List<ScalarParameter> parameters, string paramName = "ScalarParameterValues")
        {
            var arr = new ArrayProperty<StructProperty>(paramName);
            arr.AddRange(parameters.Select(x => x.ToStruct()));
            export.WriteProperty(arr);
        }

        public static ScalarParameter FromStruct(StructProperty sp, Func<ScalarParameter> objectGenerator = null)
        {
            var scalar = objectGenerator?.Invoke() ?? new ScalarParameter();
            scalar.Property = sp;
            scalar.ParameterName = sp.GetProp<NameProperty>("ParameterName")?.Value;
            scalar.ParameterValue = sp.GetProp<FloatProperty>(sp.StructType == "SMAScalarParameter" ? "Parameter" : "ParameterValue").Value;
            scalar.ExpressionGUID = sp.GetProp<StructProperty>("ExpressionGUID");
            scalar.Group = sp.GetProp<NameProperty>("Group");
            return scalar;
        }

        public StructProperty ToStruct()
        {
            PropertyCollection props = new PropertyCollection();
            props.Add(new NameProperty(ParameterName, "ParameterName"));

            if (Property is { StructType: "SMAScalarParameter" })
            {
                props.Add(new FloatProperty(ParameterValue, "Parameter"));
                props.Add(Group);
                return new StructProperty("SMAScalarParameter", props);
            }
            else
            {
                props.Add(new FloatProperty(ParameterValue, "ParameterValue"));
                props.Add(StructTools.ToFourPartIntStruct("Guid", true, 0, 0, 0, 0,
                    "A", "B", "C", "D", "ExpressionGUID"));
                return new StructProperty("ScalarParameterValue", props);
            }
        }

        public float ParameterValue { get; set; }

        /// <summary>
        /// SMAScalarParameter only
        /// </summary>
        public NameProperty Group { get; set; }

    }

    public class VectorParameter : ExpressionParameter
    {
        public static List<VectorParameter> GetVectorParameters(ExportEntry export, bool returnNullOnNotFound = false, Func<VectorParameter> objectGenerator = null)
        {
            var vectors = export.GetProperty<ArrayProperty<StructProperty>>("VectorParameterValues");
            if (vectors == null)
                return returnNullOnNotFound ? null : new List<VectorParameter>();
            return vectors?.Select(x => FromStruct(x, objectGenerator)).ToList();
        }

        public static void WriteVectorParameters(ExportEntry export, List<VectorParameter> parameters, string paramName = "VectorParameterValues")
        {
            var arr = new ArrayProperty<StructProperty>(paramName);
            arr.AddRange(parameters.Select(x => x.ToStruct()));
            export.WriteProperty(arr);
        }

        public static VectorParameter FromStruct(StructProperty sp, Func<VectorParameter> objectGenerator = null)
        {
            var vp = objectGenerator?.Invoke() ?? new VectorParameter();
            vp.Property = sp;
            vp.ParameterName = sp.GetProp<NameProperty>("ParameterName")?.Value;
            vp.ParameterValue = StructTools.FromLinearColorStructProperty(sp.GetProp<StructProperty>(sp.StructType == "SMAVectorParameter" ? "Parameter" : "ParameterValue"));
            vp.ExpressionGUID = sp.GetProp<StructProperty>("ExpressionGUID");
            vp.Group = sp.GetProp<NameProperty>("Group");
            return vp;
        }

        public StructProperty ToStruct()
        {
            PropertyCollection props = new PropertyCollection();
            props.Add(new NameProperty(ParameterName, "ParameterName"));

            if (Property != null && Property.StructType == "SMAVectorParameter")
            {
                props.Add(StructTools.ToFourPartFloatStruct("LinearColor", true, ParameterValue.W, ParameterValue.X, ParameterValue.Y, ParameterValue.Z,
                    "R", "G", "B", "A", "Parameter"));
                props.Add(Group);
                return new StructProperty("SMAVectorParameter", props);
            }
            else
            {
                props.Add(StructTools.ToFourPartFloatStruct("LinearColor", true, ParameterValue.W, ParameterValue.X, ParameterValue.Y, ParameterValue.Z,
                    "R", "G", "B", "A", "ParameterValue"));
                props.Add(StructTools.ToFourPartIntStruct("Guid", true, 0, 0, 0, 0,
                    "A", "B", "C", "D", "ExpressionGUID"));
                return new StructProperty("VectorParameterValue", props);
            }
        }
        public CFVector4 ParameterValue { get; set; }

        /// <summary>
        /// SMAVectorParameter only
        /// </summary>
        public NameProperty Group { get; set; }

    }

    public class TextureParameter : ExpressionParameter
    {
        public static List<TextureParameter> GetTextureParameters(ExportEntry export, bool returnNullOnNotFound = false, Func<TextureParameter> generator = null)
        {
            var textures = export.GetProperty<ArrayProperty<StructProperty>>("TextureParameterValues");
            if (textures == null)
                return returnNullOnNotFound ? null : new List<TextureParameter>();
            return textures?.Select(x => FromStruct(x, generator)).ToList();
        }

        public static TextureParameter FromStruct(StructProperty sp, Func<TextureParameter> objectGenerator = null)
        {
            TextureParameter tp = objectGenerator?.Invoke() ?? new TextureParameter();
            tp.Property = sp;
            tp.ParameterName = sp.GetProp<NameProperty>("ParameterName")?.Value;
            tp.ParameterValue = sp.GetProp<ObjectProperty>("ParameterValue").Value;
            tp.ExpressionGUID = sp.GetProp<StructProperty>("ExpressionGUID");
            return tp;
        }

        public StructProperty ToStruct()
        {
            PropertyCollection props = new PropertyCollection();
            props.Add(new NameProperty(ParameterName, "ParameterName"));
            props.Add(new ObjectProperty(ParameterValue, "ParameterValue"));
            props.Add(StructTools.ToFourPartIntStruct("Guid", true, 0, 0, 0, 0,
                "A", "B", "C", "D", "ExpressionGUID"));
            return new StructProperty("TextureParameterValue", props);
        }

        /// <summary>
        /// Object UIndex
        /// </summary>
        public int ParameterValue { get; set; }
    }

    public static class StructTools
    {
        public static StructProperty ToVectorStructProperty(this Vector3 vector, string propName = null)
        {
            var pc = new PropertyCollection();
            pc.Add(new FloatProperty(vector.X, "X"));
            pc.Add(new FloatProperty(vector.Y, "Y"));
            pc.Add(new FloatProperty(vector.Z, "Z"));
            return new StructProperty("Vector", pc, propName, true);
        }

        public static StructProperty ToRotatorStructProperty(this CIVector3 vector, string propName = null)
        {
            var pc = new PropertyCollection();
            pc.Add(new IntProperty(vector.X, "X"));
            pc.Add(new IntProperty(vector.Y, "Y"));
            pc.Add(new IntProperty(vector.Z, "Z"));
            return new StructProperty("Rotator", pc, propName, true);
        }

        /// <summary>
        /// Maps R => W, G => X, B => Y, A => Z
        /// </summary>
        /// <param name="getProp"></param>
        /// <returns></returns>
        public static CFVector4 FromLinearColorStructProperty(StructProperty getProp)
        {
            return new CFVector4()
            {
                W = getProp.GetProp<FloatProperty>("R"),
                X = getProp.GetProp<FloatProperty>("G"),
                Y = getProp.GetProp<FloatProperty>("B"),
                Z = getProp.GetProp<FloatProperty>("A"),
            };
        }

        public static StructProperty ToFourPartFloatStruct(string structType, bool isImmutable, float val1, float val2, float val3, float val4, string name1, string name2, string name3, string name4, string structname = null)
        {
            PropertyCollection props = new PropertyCollection();
            props.Add(new FloatProperty(val1, name1));
            props.Add(new FloatProperty(val2, name2));
            props.Add(new FloatProperty(val3, name3));
            props.Add(new FloatProperty(val4, name4));
            return new StructProperty(structType, props, structname, isImmutable);
        }

        public static StructProperty ToFourPartIntStruct(string structType, bool isImmutable, int val1, int val2, int val3, int val4, string name1, string name2, string name3, string name4, string structname = null)
        {
            PropertyCollection props = new PropertyCollection();
            props.Add(new IntProperty(val1, name1));
            props.Add(new IntProperty(val2, name2));
            props.Add(new IntProperty(val3, name3));
            props.Add(new IntProperty(val4, name4));
            return new StructProperty(structType, props, structname, isImmutable);
        }

    }
}
