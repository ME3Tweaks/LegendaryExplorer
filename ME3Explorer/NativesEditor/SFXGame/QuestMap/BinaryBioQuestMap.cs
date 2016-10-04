using System;
using System.Collections.Generic;
using System.IO;
using Gammtek.Conduit.Extensions;
using Gammtek.Conduit.IO;

namespace Gammtek.Conduit.MassEffect3.SFXGame.QuestMap
{
	public class BinaryBioQuestMap : BioQuestMap
	{
		private long _questsOffset;

		public BinaryBioQuestMap(IDictionary<int, BioQuest> quests = null,
			IDictionary<int, BioStateTaskList> taskEvals = null,
			IDictionary<int, BioStateTaskList> intTaskEvals = null,
			IDictionary<int, BioStateTaskList> floatTaskEvals = null)
			: base(quests, taskEvals, intTaskEvals, floatTaskEvals)
		{}

		//public Stream BaseStream { get; protected set; }

		public long QuestsOffset
		{
			get { return _questsOffset; }
			set { SetProperty(ref _questsOffset, value); }
		}

		public static BioQuestMap Load(string path)
		{
			if (path.IsNullOrEmpty())
			{
				throw new ArgumentNullException(nameof(path));
			}

			return !File.Exists(path) ? null : Load(File.Open(path, FileMode.Open));
		}

		public static BioQuestMap Load(Stream stream)
		{
			if (stream == null)
			{
				throw new ArgumentNullException(nameof(stream));
			}

			using (var reader = new BioQuestMapReader(stream))
			{
				var map = new BinaryBioQuestMap();

				var questsCount = reader.ReadInt32();
				map.Quests = new Dictionary<int, BioQuest>();

				for (var i = 0; i < questsCount; i++)
				{
					var id = reader.ReadInt32();
					var quest = reader.ReadQuest();

					if (!map.Quests.ContainsKey(id))
					{
						map.Quests.Add(id, quest);
					}
					else
					{
						map.Quests[id] = quest;
					}
				}

				//
				var taskEvalsCount = reader.ReadInt32();
				map.BoolTaskEvals = new Dictionary<int, BioStateTaskList>();

				for (var i = 0; i < taskEvalsCount; i++)
				{
					var id = reader.ReadInt32();
					var taskList = reader.ReadStateTaskList();

					if (!map.BoolTaskEvals.ContainsKey(id))
					{
						map.BoolTaskEvals.Add(id, taskList);
					}
					else
					{
						map.BoolTaskEvals[id] = taskList;
					}
				}

				//
				var intTaskEvalsCount = reader.ReadInt32();
				map.IntTaskEvals = new Dictionary<int, BioStateTaskList>();

				for (var i = 0; i < intTaskEvalsCount; i++)
				{
					var id = reader.ReadInt32();
					var taskList = reader.ReadStateTaskList();

					if (!map.IntTaskEvals.ContainsKey(id))
					{
						map.IntTaskEvals.Add(id, taskList);
					}
					else
					{
						map.IntTaskEvals[id] = taskList;
					}
				}

				//
				var floatTaskEvalsCount = reader.ReadInt32();
				map.FloatTaskEvals = new Dictionary<int, BioStateTaskList>();

				for (var i = 0; i < floatTaskEvalsCount; i++)
				{
					var id = reader.ReadInt32();
					var taskList = reader.ReadStateTaskList();

					if (!map.FloatTaskEvals.ContainsKey(id))
					{
						map.FloatTaskEvals.Add(id, taskList);
					}
					else
					{
						map.FloatTaskEvals[id] = taskList;
					}
				}

				return map;
			}
		}

		public void Save(string path)
		{
			if (path.IsNullOrEmpty())
			{
				throw new ArgumentNullException(nameof(path));
			}

			Save(File.Open(path, FileMode.Create));
		}

		public void Save(Stream stream)
		{
			if (stream == null)
			{
				throw new ArgumentNullException(nameof(stream));
			}

			using (var writer = new BioQuestMapWriter(stream))
			{
				// Quests
				writer.Write(Quests.Count);

				foreach (var quest in Quests)
				{
					writer.Write(quest.Key);
					writer.Write(quest.Value);
				}

				//
				writer.Write(BoolTaskEvals.Count);

				foreach (var taskEval in BoolTaskEvals)
				{
					writer.Write(taskEval.Key);
					writer.Write(taskEval.Value);
				}

				//
				writer.Write(IntTaskEvals.Count);

				foreach (var taskEval in IntTaskEvals)
				{
					writer.Write(taskEval.Key);
					writer.Write(taskEval.Value);
				}

				//
				writer.Write(FloatTaskEvals.Count);

				foreach (var taskEval in FloatTaskEvals)
				{
					writer.Write(taskEval.Key);
					writer.Write(taskEval.Value);
				}
			}
		}

		public class BioQuestMapReader : DataReader
		{
			public BioQuestMapReader(Stream stream)
				: base(stream) { }

			public BioQuest ReadQuest()
			{
				var quest = new BioQuest
				{
					InstanceVersion = ReadInt32(),
					IsMission = ReadInt32().ToBoolean()
				};

				// Goals
				var goalsCount = ReadInt32();
				quest.Goals = new List<BioQuestGoal>();

				for (var i = 0; i < goalsCount; i++)
				{
					var goal = ReadQuestGoal();

					quest.Goals.Add(goal);
				}

				// Tasks
				var tasksCount = ReadInt32();
				quest.Tasks = new List<BioQuestTask>();

				for (var i = 0; i < tasksCount; i++)
				{
					var task = ReadQuestTask();

					quest.Tasks.Add(task);
				}

				// Plot Items
				var plotItemsCount = ReadInt32();
				quest.PlotItems = new List<BioQuestPlotItem>();

				for (var i = 0; i < plotItemsCount; i++)
				{
					var plotItem = ReadQuestPlotItem();

					quest.PlotItems.Add(plotItem);
				}

				return quest;
			}

			public BioQuestGoal ReadQuestGoal()
			{
				var goal = new BioQuestGoal
				{
					InstanceVersion = ReadInt32(),
					Name = ReadInt32(),
					Description = ReadInt32(),
					Conditional = ReadInt32(),
					State = ReadInt32()
				};

				return goal;
			}

			public BioQuestPlotItem ReadQuestPlotItem()
			{
				var plotItem = new BioQuestPlotItem
				{
					InstanceVersion = ReadInt32(),
					Name = ReadInt32(),
					IconIndex = ReadInt32(),
					Conditional = ReadInt32(),
					State = ReadInt32(),
					TargetItems = ReadInt32()
				};

				return plotItem;
			}

			public BioQuestTask ReadQuestTask()
			{
				var task = new BioQuestTask
				{
					InstanceVersion = ReadInt32(),
					QuestCompleteTask = ReadInt32().ToBoolean(),
					Name = ReadInt32(),
					Description = ReadInt32()
				};

				var plotItemIndicesCount = ReadInt32();
				task.PlotItemIndices = new List<int>();

				for (var i = 0; i < plotItemIndicesCount; i++)
				{
					task.PlotItemIndices.Add(ReadInt32());
				}

				task.PlanetNameFlags = ReadInt32();
				task.PlanetName = ReadInt32();

				var waypointTagSize = ReadInt32();

				if (waypointTagSize < 0)
				{
					return task;
				}

				var chars = ReadChars(waypointTagSize);
				task.WaypointTag = new string(chars);

				return task;
			}

			public BioStateTaskList ReadStateTaskList()
			{
				var list = new BioStateTaskList
				{
					InstanceVersion = ReadInt32()
				};

				var taskEvalsCount = ReadInt32();
				list.TaskEvals = new List<BioTaskEval>();

				for (var i = 0; i < taskEvalsCount; i++)
				{
					list.TaskEvals.Add(ReadTaskEval());
				}

				return list;
			}

			public BioTaskEval ReadTaskEval()
			{
				var taskEval = new BioTaskEval
				{
					InstanceVersion = ReadInt32(),
					Task = ReadInt32(),
					State = ReadInt32(),
					Conditional = ReadInt32(),
					Quest = ReadInt32()
				};

				return taskEval;
			}
		}

		public class BioQuestMapWriter : DataWriter
		{
			public new static readonly BioQuestMapWriter Null = new BioQuestMapWriter();

			protected BioQuestMapWriter() { }

			public BioQuestMapWriter(Stream output)
				: base(output) { }

			public void Write(BioQuest quest)
			{
				if (quest == null)
				{
					throw new ArgumentNullException(nameof(quest));
				}

				Write(quest.InstanceVersion);
				Write(quest.IsMission.ToInt32());

				// Goals
				Write(quest.Goals.Count);

				foreach (var goal in quest.Goals)
				{
					Write(goal);
				}

				// Tasks
				Write(quest.Tasks.Count);

				foreach (var task in quest.Tasks)
				{
					Write(task);
				}

				// Plot Items
				Write(quest.PlotItems.Count);

				foreach (var plotItem in quest.PlotItems)
				{
					Write(plotItem);
				}
			}

			public void Write(BioQuestGoal goal)
			{
				if (goal == null)
				{
					throw new ArgumentNullException(nameof(goal));
				}

				Write(goal.InstanceVersion);
				Write(goal.Name);
				Write(goal.Description);
				Write(goal.Conditional);
				Write(goal.State);
			}

			public void Write(BioQuestPlotItem questPlotItem)
			{
				if (questPlotItem == null)
				{
					throw new ArgumentNullException(nameof(questPlotItem));
				}

				Write(questPlotItem.InstanceVersion);
				Write(questPlotItem.Name);
				Write(questPlotItem.IconIndex);
				Write(questPlotItem.Conditional);
				Write(questPlotItem.State);
				Write(questPlotItem.TargetItems);
			}

			public void Write(BioQuestTask task)
			{
				if (task == null)
				{
					throw new ArgumentNullException(nameof(task));
				}

				Write(task.InstanceVersion);
				Write(task.QuestCompleteTask.ToInt32());
				Write(task.Name);
				Write(task.Description);

				Write(task.PlotItemIndices.Count);

				foreach (var itemIndex in task.PlotItemIndices)
				{
					Write(itemIndex);
				}

				Write(task.PlanetNameFlags);
				Write(task.PlanetName);

				// Waypoint Tag
				Write(task.WaypointTag.Length);

				if (task.WaypointTag.Length > 0)
				{
					Write(task.WaypointTag);
				}
			}

			public void Write(BioTaskEval taskEval)
			{
				Write(taskEval.InstanceVersion);
				Write(taskEval.Task);
				Write(taskEval.State);
				Write(taskEval.Conditional);
				Write(taskEval.Quest);
			}

			public void Write(BioStateTaskList taskList)
			{
				Write(taskList.InstanceVersion);
				Write(taskList.TaskEvals.Count);

				foreach (var taskEval in taskList.TaskEvals)
				{
					Write(taskEval);
				}
			}
		}
	}
}
