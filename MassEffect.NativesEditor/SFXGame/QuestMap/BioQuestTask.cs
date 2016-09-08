using System.Collections.Generic;
using System.Linq;

namespace Gammtek.Conduit.MassEffect3.SFXGame.QuestMap
{
	/// <summary>
	/// </summary>
	public class BioQuestTask : BioVersionedNativeObject
	{
		/// <summary>
		/// </summary>
		public const int DefaultDescription = -1;

		/// <summary>
		/// </summary>
		public new const int DefaultInstanceVersion = BioVersionedNativeObject.DefaultInstanceVersion;

		/// <summary>
		/// </summary>
		public const int DefaultName = -1;

		/// <summary>
		/// </summary>
		public const int DefaultPlanetName = -1;

		/// <summary>
		/// </summary>
		public const int DefaultPlanetNameFlags = 0;

		/// <summary>
		/// </summary>
		public const bool DefaultQuestCompleteTask = false;

		/// <summary>
		/// </summary>
		public const string DefaultWaypointTag = null;

		private int _description;
		private int _name;
		private int _planetName;
		private int _planetNameFlags;
		private IList<int> _plotItemIndices;
		private bool _questCompleteTask;
		private string _waypointTag;

		/// <summary>
		/// </summary>
		/// <param name="name"></param>
		/// <param name="description"></param>
		/// <param name="questCompleteTask"></param>
		/// <param name="plotItemIndices"></param>
		/// <param name="planetName"></param>
		/// <param name="planetNameFlags"></param>
		/// <param name="waypointTag"></param>
		/// <param name="instanceVersion"></param>
		public BioQuestTask(int name = DefaultName, int description = DefaultDescription, bool questCompleteTask = DefaultQuestCompleteTask,
			IList<int> plotItemIndices = null, int planetName = DefaultPlanetName, int planetNameFlags = DefaultPlanetNameFlags,
			string waypointTag = DefaultWaypointTag, int instanceVersion = DefaultInstanceVersion)
			: base(instanceVersion)
		{
			Description = description;
			Name = name;
			PlanetName = planetName;
			PlanetNameFlags = planetNameFlags;
			PlotItemIndices = plotItemIndices ?? new List<int>();
			QuestCompleteTask = questCompleteTask;
			WaypointTag = waypointTag;
		}

		/// <summary>
		/// </summary>
		/// <param name="other"></param>
		public BioQuestTask(BioQuestTask other)
			: base(other)
		{
			Description = other.Description;
			Name = other.Name;
			PlanetName = other.PlanetName;
			PlanetNameFlags = other.PlanetNameFlags;
			PlotItemIndices = other.PlotItemIndices != null ? other.PlotItemIndices.ToList() : new List<int>();
			QuestCompleteTask = other.QuestCompleteTask;
			WaypointTag = other.WaypointTag;
		}

		/// <summary>
		/// </summary>
		public int Description
		{
			get { return _description; }
			set { SetProperty(ref _description, value); }
		}

		/// <summary>
		/// </summary>
		public int Name
		{
			get { return _name; }
			set { SetProperty(ref _name, value); }
		}

		/// <summary>
		/// </summary>
		public int PlanetName
		{
			get { return _planetName; }
			set { SetProperty(ref _planetName, value); }
		}

		/// <summary>
		/// </summary>
		public int PlanetNameFlags
		{
			get { return _planetNameFlags; }
			set { SetProperty(ref _planetNameFlags, value); }
		}

		/// <summary>
		/// </summary>
		public IList<int> PlotItemIndices
		{
			get { return _plotItemIndices; }
			set { SetProperty(ref _plotItemIndices, value); }
		}

		/// <summary>
		/// </summary>
		public bool QuestCompleteTask
		{
			get { return _questCompleteTask; }
			set { SetProperty(ref _questCompleteTask, value); }
		}

		/// <summary>
		/// </summary>
		public string WaypointTag
		{
			get { return _waypointTag; }
			set { SetProperty(ref _waypointTag, value); }
		}
	}
}
