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
        private bool _isDefaultParameter;
        /// <summary>
        ///  If this parameter is from the BaseMaterial expressions list.
        /// </summary>
        public bool IsDefaultParameter { get => _isDefaultParameter; set => SetProperty(ref _isDefaultParameter, value); }

        public static VectorParameterMatEd FromExpression(ExportEntry expression)
        {
            var te = new VectorParameterMatEd();
            var props = expression.GetProperties();
            te.ParameterName = props.GetProp<NameProperty>("ParameterName")?.Value.Instanced ?? "None";
            var defaultValue = props.GetProp<StructProperty>("DefaultValue");
            if (defaultValue != null)
            {
                te.ParameterValue = CFVector4.FromStructProperty(defaultValue, "R", "G", "B", "A");
            }
            else
            {
                // defaults to 0
                te.ParameterValue = new CFVector4();
            }
            te.ExpressionGUID = props.GetProp<StructProperty>("ExpressionGUID");
            te.IsDefaultParameter = true;
            return te;
        }
    }
}
