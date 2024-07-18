using LegendaryExplorer.Tools.LiveLevelEditor.MatEd;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;

namespace LegendaryExplorer.UserControls.ExportLoaderControls.MaterialEditor
{
    internal class ScalarParameterMatEd : ScalarParameter
    {
        /// <summary>
        ///  If this parameter is from the BaseMaterial expressions list.
        /// </summary>
        public bool IsDefaultParameter { get; set; }

                /// <summary>
        /// Generates a <see cref="ScalarParameterMatEd"/> object from the given material expression export 
        /// </summary>
        /// <param name="expression"></param>
        /// <returns></returns>
        public static ScalarParameterMatEd FromExpression(ExportEntry expression)
        {
            ScalarParameterMatEd te = new ScalarParameterMatEd();
            var props = expression.GetProperties();
            te.ParameterName = props.GetProp<NameProperty>("ParameterName").Value.Instanced;
            te.ParameterValue = props.GetProp<FloatProperty>("DefaultValue")?.Value ?? 0f;
            te.ExpressionGUID = props.GetProp<StructProperty>("ExpressionGUID");
            te.IsDefaultParameter = true;
            return te;
        }
    }
}
