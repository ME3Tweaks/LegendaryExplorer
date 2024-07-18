using LegendaryExplorer.Tools.LiveLevelEditor.MatEd;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;

namespace LegendaryExplorer.UserControls.ExportLoaderControls.MaterialEditor
{
    /// <summary>
    /// Material Editor Vector Parameter subclass
    /// </summary>
    public class VectorParameterMatEd : VectorParameter
    {
        public bool IsDefaultParameter { get; set; }

        public static VectorParameterMatEd FromExpression(ExportEntry expression)
        {
            var te = new VectorParameterMatEd();
            var props = expression.GetProperties();
            te.ParameterName = props.GetProp<NameProperty>("ParameterName").Value.Instanced;
            te.ParameterValue = CFVector4.FromStructProperty(props.GetProp<StructProperty>("DefaultValue"), "R", "G", "B", "A");
            te.ExpressionGUID = props.GetProp<StructProperty>("ExpressionGUID");
            te.IsDefaultParameter = true;
            return te;
        }
    }
}
