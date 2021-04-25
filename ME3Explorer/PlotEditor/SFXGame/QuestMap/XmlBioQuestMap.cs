using ME3ExplorerCore.Gammtek;
using ME3ExplorerCore.Gammtek.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Gammtek.Conduit.MassEffect3.SFXGame.QuestMap
{
	public class XmlBioQuestMap : BioQuestMap
	{
		public XmlBioQuestMap(IDictionary<int, BioQuest> quests = null)
			: base(quests) {}

		
		public static BioQuestMap Load( string path)
		{
			if (string.IsNullOrEmpty(path))
			{
				throw new ArgumentNullException(nameof(path));
			}

			var map = new BioQuestMap();

			if (!File.Exists(path))
			{
				return map;
			}

			var doc = XDocument.Load(path);
			var root = doc.Root;

			if (root == null)
			{
				return map;
			}

			if (!root.Name.LocalName.Equals("QuestMap"))
			{
				root = root.Element("QuestMap");

				if (root == null)
				{
					return map;
				}
			}

			//
			map.Quests = ReadQuests(root);

			return map;
		}

		public XElement CreateQuestMap()
		{
			var questMapElement = new XElement("QuestMap");

			// Quests
			questMapElement.Add(CreateQuests());

			// BoolTaskEvals
			questMapElement.Add(CreateTaskEvals("BoolTaskEvals", BoolTaskEvals));

			// FloatTaskEvals
			questMapElement.Add(CreateTaskEvals("FloatTaskEvals", FloatTaskEvals));

			// IntTaskEvals
			questMapElement.Add(CreateTaskEvals("IntTaskEvals", IntTaskEvals));

			//
			return questMapElement;
		}

		
		public XElement CreateQuests()
		{
			var questsElement = new XElement("Quests");

			// Quests
			foreach (var quest in Quests)
			{
				var questElement = new XElement("Quest",
					new XAttribute("Id", quest.Key),
					new XAttribute("IsMission", quest.Value.IsMission),
					new XAttribute("InstanceVersion", quest.Value.InstanceVersion));

				var questGoalsElement = new XElement("Goals");
				var plotItemsElement = new XElement("PlotItems");
				var tasksElement = new XElement("Tasks");

				// Quest Goals
				foreach (var questGoalElement in quest.Value.Goals
					.Select(questGoal => new XElement("Goal",
						new XAttribute("Name", questGoal.Name),
						new XAttribute("Description", questGoal.Description),
						new XAttribute("Conditional", questGoal.Conditional),
						new XAttribute("State", questGoal.State),
						new XAttribute("InstanceVersion", questGoal.InstanceVersion))))
				{
					questGoalsElement.Add(questGoalElement);
				}

				questElement.Add(questGoalsElement);

				// Quest Plot Items
				foreach (var questPlotItemElement in quest.Value.PlotItems
					.Select(plotItem => new XElement("PlotItem",
						new XAttribute("Name", plotItem.Name),
						new XAttribute("Conditional", plotItem.Conditional),
						new XAttribute("State", plotItem.State),
						new XAttribute("IconIndex", plotItem.IconIndex),
						new XAttribute("TargetItems", plotItem.TargetItems),
						new XAttribute("InstanceVersion", plotItem.InstanceVersion))))
				{
					plotItemsElement.Add(questPlotItemElement);
				}

				questElement.Add(plotItemsElement);

				// Quest Tasks
				foreach (var task in quest.Value.Tasks)
				{
					var questTaskElement = new XElement("PlotItem",
						new XAttribute("Name", task.Name),
						new XAttribute("Description", task.Description),
						new XAttribute("QuestCompleteTask", task.QuestCompleteTask),
						new XAttribute("PlanetName", task.PlanetName),
						new XAttribute("PlanetNameFlags", task.PlanetNameFlags),
						new XAttribute("WaypointTag", task.WaypointTag),
						new XAttribute("InstanceVersion", task.InstanceVersion));

					var plotItemIndicesElement = new XElement("PlotItemIndices");

					foreach (var plotItemIndex in task.PlotItemIndices)
					{
						plotItemIndicesElement.Add(new XElement("PlotItemIndex", plotItemIndex));
					}

					questTaskElement.Add(plotItemIndicesElement);

					tasksElement.Add(questTaskElement);
				}

				questElement.Add(tasksElement);

				//
				questsElement.Add(questElement);
			}

			return questsElement;
		}

		
		public static IDictionary<int, BioQuest> ReadQuests(XElement root)
		{
			if (root == null)
			{
				ThrowHelper.ThrowArgumentNullException(nameof(root));
			}

			var quests = new Dictionary<int, BioQuest>();
			var questsElement = root.Element("Quests");

			if (questsElement == null)
			{
				return quests;
			}

			var xQuests = from el in questsElement.Elements("Quest") select el;

			foreach (var xQuest in xQuests)
			{
				var id = (int?) xQuest.Attribute("Id");

				if (id == null)
				{
					continue;
				}

				var quest = new BioQuest
				{
					InstanceVersion = (int?) xQuest.Attribute("InstanceVersion") ?? BioQuest.DefaultInstanceVersion,
					IsMission = (bool?) xQuest.Attribute("IsMission") ?? BioQuest.DefaultIsMission
				};

				var questGoalsElement = questsElement.Element("Goals");
				var questPlotItemsElement = questsElement.Element("PlotItems");
				var questTasksElement = questsElement.Element("Tasks");

				if (questGoalsElement != null)
				{
					var xQuestGoals = from el in questsElement.Elements("Goal") select el;

					foreach (var questGoal in xQuestGoals
						.Select(xQuestGoal => new BioQuestGoal
						{
							Conditional = (int?) xQuestGoal.Attribute("Conditional") ?? BioQuestGoal.DefaultConditional,
							Description = (int?) xQuestGoal.Attribute("Description") ?? BioQuestGoal.DefaultDescription,
							InstanceVersion = (int?) xQuestGoal.Attribute("InstanceVersion") ?? BioQuestGoal.DefaultInstanceVersion,
							Name = (int?) xQuestGoal.Attribute("Name") ?? BioQuestGoal.DefaultName,
							State = (int?) xQuestGoal.Attribute("State") ?? BioQuestGoal.DefaultState
						}))
					{
						quest.Goals.Add(questGoal);
					}
				}

				if (questPlotItemsElement != null)
				{
					var xQuestPlotItems = from el in questsElement.Elements("PlotItem") select el;

					foreach (var questPlotItem in xQuestPlotItems
						.Select(xQuestPlotItem => new BioQuestPlotItem
						{
							Conditional = (int?) xQuestPlotItem.Attribute("Conditional") ?? BioQuestPlotItem.DefaultConditional,
							IconIndex = (int?) xQuestPlotItem.Attribute("IconIndex") ?? BioQuestPlotItem.DefaultIconIndex,
							InstanceVersion = (int?) xQuestPlotItem.Attribute("InstanceVersion") ?? BioQuestPlotItem.DefaultInstanceVersion,
							Name = (int?) xQuestPlotItem.Attribute("Name") ?? BioQuestPlotItem.DefaultName,
							State = (int?) xQuestPlotItem.Attribute("State") ?? BioQuestPlotItem.DefaultState,
							TargetItems = (int?) xQuestPlotItem.Attribute("TargetItems") ?? BioQuestPlotItem.DefaultTargetItems
						}))
					{
						quest.PlotItems.Add(questPlotItem);
					}
				}

				if (questTasksElement != null)
				{
					var xQuestTasks = from el in questsElement.Elements("Task") select el;

					foreach (var xQuestTask in xQuestTasks)
					{
						var questTask = new BioQuestTask
						{
							Description = (int?) xQuestTask.Attribute("Conditional") ?? BioQuestTask.DefaultDescription,
							Name = (int?) xQuestTask.Attribute("Name") ?? BioQuestTask.DefaultName,
							InstanceVersion = (int?) xQuestTask.Attribute("InstanceVersion") ?? BioQuestTask.DefaultInstanceVersion,
							PlanetName = (int?) xQuestTask.Attribute("IconIndex") ?? BioQuestTask.DefaultPlanetName,
							PlanetNameFlags = (int?) xQuestTask.Attribute("IconIndex") ?? BioQuestTask.DefaultPlanetNameFlags,
							QuestCompleteTask = (bool?) xQuestTask.Attribute("State") ?? BioQuestTask.DefaultQuestCompleteTask,

							// ReSharper disable once ConstantNullCoalescingCondition
							WaypointTag = (string) xQuestTask.Attribute("TargetItems") ?? BioQuestTask.DefaultWaypointTag
						};

						var questTaskPlotItemIndicesElement = questsElement.Element("PlotItemIndices");

						if (questTaskPlotItemIndicesElement != null)
						{
							var xQuestTaskPlotItemIndices = from el in questsElement.Elements("PlotItemIndex") select el;

							foreach (var xQuestTaskPlotItemIndex in
								xQuestTaskPlotItemIndices.Where(xQuestTaskPlotItemIndex => !string.IsNullOrEmpty(xQuestTaskPlotItemIndex.Value)))
							{
								questTask.PlotItemIndices.Add(xQuestTaskPlotItemIndex.Value.ToInt32());
							}
						}

						quest.Tasks.Add(questTask);
					}
				}

				quests.Add((int) id, quest);
			}

			return quests;
		}

		public void Save( string path)
		{
			if (string.IsNullOrEmpty(path))
			{
				throw new ArgumentNullException(nameof(path));
			}

			var doc = new XDocument();

			//
			doc.Add(CreateQuestMap());

			using (var writer = new XmlTextWriter(path, Encoding.UTF8))
			{
				writer.IndentChar = '\t';
				writer.Indentation = 1;
				writer.Formatting = Formatting.Indented;

				doc.Save(writer);
			}
		}

		
		public static XElement CreateTaskEvals(string name, IDictionary<int, BioStateTaskList> taskEvals)
		{
			var boolTaskEvalsElement = new XElement(name);

			// BoolTaskEvals
			foreach (var stateTaskList in taskEvals)
			{
				var stateTaskListElement = new XElement("StateTaskList",
					new XAttribute("Id", stateTaskList.Key),
					new XAttribute("InstanceVersion", stateTaskList.Value.InstanceVersion));

				var taskEvalsElement = new XElement("TaskEvals");

				foreach (var taskEval in stateTaskList.Value.TaskEvals)
				{
					taskEvalsElement.Add(new XElement("TaskEval",
						new XAttribute("Quest", taskEval.Quest),
						new XAttribute("Conditional", taskEval.Conditional),
						new XAttribute("State", taskEval.State),
						new XAttribute("Task", taskEval.Task),
						new XAttribute("InstanceVersion", taskEval.InstanceVersion)));
				}

				stateTaskListElement.Add(taskEvalsElement);

				boolTaskEvalsElement.Add(stateTaskListElement);
			}

			return boolTaskEvalsElement;
		}
	}
}
