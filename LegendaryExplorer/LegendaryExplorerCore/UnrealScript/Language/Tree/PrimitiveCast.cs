using LegendaryExplorerCore.UnrealScript.Utilities;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public class PrimitiveCast : CastExpression
    {
        public readonly ECast Cast;

        public PrimitiveCast(ECast cast, VariableType castType, Expression expr, int start, int end) : base(castType, expr, start, end)
        {
            Cast = cast;
        }
    }
}
