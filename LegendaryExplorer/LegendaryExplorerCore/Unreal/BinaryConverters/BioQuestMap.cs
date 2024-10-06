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
        
        protected override void Serialize(SerializingContainer sc)
        {
            sc.Serialize(ref Quests, sc.Serialize);
            sc.Serialize(ref TaskEvals, sc.Serialize);
            sc.Serialize(ref IntTaskEvals, sc.Serialize);
            sc.Serialize(ref FloatTaskEvals, sc.Serialize);
        }

        public static BioQuestMap Create()
        {
            return new()
            {
                Quests = [],
                TaskEvals = [],
                IntTaskEvals = [],
                FloatTaskEvals = []
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

    public partial class SerializingContainer
    {
        public void Serialize(ref BioQuest quest)
        {
            if (IsLoading) quest = new BioQuest();
            Serialize(ref quest.ID);
            Serialize(ref quest.InstanceVersion);
            Serialize(ref quest.IsMission);
            Serialize(ref quest.Goals, Serialize);
            Serialize(ref quest.Tasks, Serialize);
            Serialize(ref quest.PlotItems, Serialize);
        }

        public void Serialize(ref BioQuestGoal goal)
        {
            if (IsLoading) goal = new BioQuestGoal();
            Serialize(ref goal.InstanceVersion);
            Serialize(ref goal.Name);
            Serialize(ref goal.Description);
            Serialize(ref goal.Conditional);
            Serialize(ref goal.State);
        }

        public void Serialize(ref BioQuestTask task)
        {
            if (IsLoading) task = new BioQuestTask();
            Serialize(ref task.InstanceVersion);
            Serialize(ref task.QuestCompleteTask);
            Serialize(ref task.Name);
            Serialize(ref task.Description);
            Serialize(ref task.PlotIndices, Serialize);
            Serialize(ref task.PlanetName);
            Serialize(ref task.WaypointRef);
        }

        public void Serialize(ref BioQuestPlotItem item)
        {
            if (IsLoading) item = new BioQuestPlotItem();
            Serialize(ref item.InstanceVersion);
            Serialize(ref item.Name);
            Serialize(ref item.IconIndex);
            Serialize(ref item.Conditional);
            Serialize(ref item.State);
            Serialize(ref item.TargetItems);
        }

        public void Serialize(ref BioStateTaskList taskList)
        {
            if (IsLoading) taskList = new BioStateTaskList();
            Serialize(ref taskList.ID);
            Serialize(ref taskList.InstanceVersion);
            Serialize(ref taskList.TaskEvals, Serialize);
        }

        public void Serialize(ref BioTaskEval task)
        {
            if (IsLoading) task = new BioTaskEval();
            Serialize(ref task.InstanceVersion);
            Serialize(ref task.Task);
            Serialize(ref task.Conditional);
            Serialize(ref task.State);
            Serialize(ref task.Quest);
        }
    }
}
