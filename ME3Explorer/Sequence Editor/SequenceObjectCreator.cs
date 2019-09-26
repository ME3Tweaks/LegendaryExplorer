using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Explorer.Packages;
using ME3Explorer.Unreal;

namespace ME3Explorer.Sequence_Editor
{
    public static class SequenceObjectCreator
    {
        public static List<string> GetSequenceVars(MEGame game) => UnrealObjectInfo.GetNonAbstractDerivedClassesOf("SequenceVariable", game);
        public static List<string> GetSequenceActions(MEGame game) => UnrealObjectInfo.GetNonAbstractDerivedClassesOf("SequenceAction", game);
        public static List<string> GetSequenceEvents(MEGame game) => UnrealObjectInfo.GetNonAbstractDerivedClassesOf("SequenceEvent", game);
        public static List<string> GetSequenceConditions(MEGame game) => UnrealObjectInfo.GetNonAbstractDerivedClassesOf("SequenceCondition", game);
    }
}
