using System.Collections.Generic;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using static LegendaryExplorerCore.Unreal.BinaryConverters.BioQuestMap;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class BioQuestMap : ObjectBinary
    {
        public List<BioQuest> Quests;
        public List<BioStateTaskList> TaskEvals;
        public List<BioStateTaskList> IntTaskEvals;
        public List<BioStateTaskList> FloatTaskEvals;
        
        protected override void Serialize(SerializingContainer2 sc)
        {
            sc.Serialize(ref Quests, SCExt.Serialize);
            sc.Serialize(ref TaskEvals, SCExt.Serialize);
            sc.Serialize(ref IntTaskEvals, SCExt.Serialize);
            sc.Serialize(ref FloatTaskEvals, SCExt.Serialize);
        }

        public static BioQuestMap Create()
        {
            return new()
            {
                Quests = new List<BioQuest>(),
                TaskEvals = new List<BioStateTaskList>(),
                IntTaskEvals = new List<BioStateTaskList>(),
                FloatTaskEvals = new List<BioStateTaskList>()
            };
        }

        public override List<(NameReference, string)> GetNames(MEGame game)
        {
            var names = base.GetNames(game);

            for (int i = 0; i < Quests.Count; i++)
            {
                var q = Quests[i];
                for (int j = 0; j < q.Tasks.Count; j++)
                {
                    var task = q.Tasks[j];
                    names.Add(task.PlanetName, $"[{i}] Quest {q.ID}: [{j}] Task : PlanetName");
                }
            }

            return names;
        }

        public class BioQuest
        {
            public int ID;
            public int InstanceVersion;
            public bool IsMission;
            public List<BioQuestGoal> Goals;
            public List<BioQuestTask> Tasks;
            public List<BioQuestPlotItem> PlotItems;
        }

        public struct BioQuestGoal
        {
            public int InstanceVersion;
            public int Name;
            public int Description;
            public int Conditional;
            public int State;
        }

        public class BioQuestTask
        {
            public int InstanceVersion;
            public bool QuestCompleteTask;
            public int Name;
            public int Description;
            public List<int> PlotIndices;
            public NameReference PlanetName;
            public string WaypointRef;
        }

        public struct BioQuestPlotItem
        {
            public int InstanceVersion; 
            public int Name;
            public int IconIndex;
            public int Conditional; 
            public int State;
            public int TargetItems;
        }

        public class BioStateTaskList
        {
            public int ID;
            public int InstanceVersion;
            public List<BioTaskEval> TaskEvals;
        }

        public struct BioTaskEval
        {
            public int InstanceVersion;
            public int Task;
            public int Conditional;
            public int State;
            public int Quest;
        }
    }

    public static partial class SCExt
    {
        public static void Serialize(this SerializingContainer2 sc, ref BioQuest quest)
        {
            if (sc.IsLoading) quest = new BioQuest();
            sc.Serialize(ref quest.ID);
            sc.Serialize(ref quest.InstanceVersion);
            sc.Serialize(ref quest.IsMission);
            sc.Serialize(ref quest.Goals, Serialize);
            sc.Serialize(ref quest.Tasks, Serialize);
            sc.Serialize(ref quest.PlotItems, Serialize);
        }

        public static void Serialize(this SerializingContainer2 sc, ref BioQuestGoal goal)
        {
            if (sc.IsLoading) goal = new BioQuestGoal();
            sc.Serialize(ref goal.InstanceVersion);
            sc.Serialize(ref goal.Name);
            sc.Serialize(ref goal.Description);
            sc.Serialize(ref goal.Conditional);
            sc.Serialize(ref goal.State);
        }

        public static void Serialize(this SerializingContainer2 sc, ref BioQuestTask task)
        {
            if (sc.IsLoading) task = new BioQuestTask();
            sc.Serialize(ref task.InstanceVersion);
            sc.Serialize(ref task.QuestCompleteTask);
            sc.Serialize(ref task.Name);
            sc.Serialize(ref task.Description);
            sc.Serialize(ref task.PlotIndices, Serialize);
            sc.Serialize(ref task.PlanetName);
            sc.Serialize(ref task.WaypointRef);
        }

        public static void Serialize(this SerializingContainer2 sc, ref BioQuestPlotItem item)
        {
            if (sc.IsLoading) item = new BioQuestPlotItem();
            sc.Serialize(ref item.InstanceVersion);
            sc.Serialize(ref item.Name);
            sc.Serialize(ref item.IconIndex);
            sc.Serialize(ref item.Conditional);
            sc.Serialize(ref item.State);
            sc.Serialize(ref item.TargetItems);
        }

        public static void Serialize(this SerializingContainer2 sc, ref BioStateTaskList taskList)
        {
            if (sc.IsLoading) taskList = new BioStateTaskList();
            sc.Serialize(ref taskList.ID);
            sc.Serialize(ref taskList.InstanceVersion);
            sc.Serialize(ref taskList.TaskEvals, Serialize);
        }

        public static void Serialize(this SerializingContainer2 sc, ref BioTaskEval task)
        {
            if (sc.IsLoading) task = new BioTaskEval();
            sc.Serialize(ref task.InstanceVersion);
            sc.Serialize(ref task.Task);
            sc.Serialize(ref task.Conditional);
            sc.Serialize(ref task.State);
            sc.Serialize(ref task.Quest);
        }
    }
}
