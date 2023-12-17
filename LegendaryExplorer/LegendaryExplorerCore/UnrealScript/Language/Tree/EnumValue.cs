﻿using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Utilities;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public class EnumValue : ASTNode
    {
        public string Name;
        public byte IntVal;
        public Enumeration Enum;

        public EnumValue(string name, byte intVal, int start = -1, int end = -1)
            : base(ASTNodeType.VariableIdentifier, start, end)
        {
            Name = name;
            IntVal = intVal;
        }

        public override bool AcceptVisitor(IASTVisitor visitor, UnrealScriptOptionsPackage usop)
        {
            return visitor.VisitNode(this, usop);
        }
    }
}
