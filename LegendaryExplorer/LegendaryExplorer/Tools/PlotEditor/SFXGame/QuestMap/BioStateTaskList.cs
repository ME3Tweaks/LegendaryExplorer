using System.Collections.Generic;
using System.Linq;

namespace Gammtek.Conduit.MassEffect3.SFXGame.QuestMap
{
	/// <summary>
	/// </summary>
	public class BioStateTaskList : BioVersionedNativeObject
	{
		/// <summary>
		/// </summary>
		public new const int DefaultInstanceVersion = BioVersionedNativeObject.DefaultInstanceVersion;

		private IList<BioTaskEval> _taskEvals;

		/// <summary>
		/// </summary>
		/// <param name="taskEvals"></param>
		public BioStateTaskList(IList<BioTaskEval> taskEvals = null)
		{
			TaskEvals = taskEvals ?? new List<BioTaskEval>();
		}

		/// <summary>
		/// </summary>
		/// <param name="other"></param>
		public BioStateTaskList(BioStateTaskList other)
			: base(other)
		{
			TaskEvals = other.TaskEvals != null ? other.TaskEvals.Select(eval => new BioTaskEval(eval)).ToList() : new List<BioTaskEval>();
		}

		/// <summary>
		/// </summary>
		public IList<BioTaskEval> TaskEvals
		{
			get { return _taskEvals; }
			set { SetProperty(ref _taskEvals, value); }
		}

        public override string ToString()
        {
			return "BioStateTaskList";
        }
    }
}
