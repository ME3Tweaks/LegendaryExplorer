using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Utilities;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public class Enumeration : VariableType
    {
        public readonly List<EnumValue> Values;
        public Enumeration(string name, List<EnumValue> values,
            int start, int end)
            : base(name, start, end, EPropertyType.Byte)
        {
            Type = ASTNodeType.Enumeration;
            Values = values;
            foreach (EnumValue enumValue in values)
            {
                enumValue.Enum = this;
            }
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
        public override IEnumerable<ASTNode> ChildNodes => Values;

        public string GenerateMaxName()
        {
            string prefix = LongestCommonPrefix(Values.Select(ev => ev.Name).ToList());
            int underScoreIndex = prefix.LastIndexOf('_');
            if (underScoreIndex > 0)
            {
                return $"{prefix[..underScoreIndex]}_MAX";
            }

            return $"{Name}_MAX";

            static string LongestCommonPrefix(List<string> strings)
            {
                if (strings.Count is 0)
                {
                    return "";
                }
                string prefix = strings.MinBy(s => s.Length);
                int i = 0;
                for (; i < prefix.Length; i++)
                {
                    foreach (string name in strings)
                    {
                        if (name[i] != prefix[i])
                        {
                            return prefix[..i];
                        }
                    }
                }
                return prefix;
            }
        }
    }
}
