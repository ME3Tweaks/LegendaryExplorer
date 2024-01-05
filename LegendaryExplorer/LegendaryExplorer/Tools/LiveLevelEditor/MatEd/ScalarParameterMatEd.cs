using LegendaryExplorerCore.Packages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Unreal;

namespace LegendaryExplorer.Tools.LiveLevelEditor.MatEd
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
            te.ParameterValue = props.GetProp<FloatProperty>("DefaultValue").Value;
            te.ExpressionGUID = props.GetProp<StructProperty>("ExpressionGUID");
            te.IsDefaultParameter = true;
            return te;
        }
    }
}
