using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME2Explorer.Unreal;
using ME3Explorer.Packages;
using ME3Explorer.Unreal;

namespace ME3Explorer.Sequence_Editor
{
    public static class SequenceObjectCreator
    {
        public static List<ClassInfo> GetSequenceVariables(MEGame game)
        {
            List<ClassInfo> classes = UnrealObjectInfo.GetNonAbstractDerivedClassesOf("SequenceVariable", game);

            if (game == MEGame.ME2)
            {
                return classes.Where(info => EntryImporter.CanImport(info, MEGame.ME2)).ToList();
            }

            return classes;
        }

        public static List<ClassInfo> GetSequenceActions(MEGame game)
        {
            List<ClassInfo> classes = UnrealObjectInfo.GetNonAbstractDerivedClassesOf("SequenceAction", game);

            if (game == MEGame.ME2)
            {
                return classes.Where(info => EntryImporter.CanImport(info, MEGame.ME2)).ToList();
            }

            return classes;
        }

        public static List<ClassInfo> GetSequenceEvents(MEGame game)
        {
            List<ClassInfo> classes = UnrealObjectInfo.GetNonAbstractDerivedClassesOf("SequenceEvent", game);

            if (game == MEGame.ME2)
            {
                return classes.Where(info => EntryImporter.CanImport(info, MEGame.ME2)).ToList();
            }

            return classes;
        }

        public static List<ClassInfo> GetSequenceConditions(MEGame game)
        {
            List<ClassInfo> classes = UnrealObjectInfo.GetNonAbstractDerivedClassesOf("SequenceCondition", game);

            if (game == MEGame.ME2)
            {
                return classes.Where(info => EntryImporter.CanImport(info, MEGame.ME2)).ToList();
            }

            return classes;
        }
    }
}
