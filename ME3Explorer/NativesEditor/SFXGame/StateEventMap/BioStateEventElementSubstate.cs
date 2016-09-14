using System.Collections.Generic;

namespace Gammtek.Conduit.MassEffect3.SFXGame.StateEventMap
{
	/// <summary>
	/// </summary>
	public class BioStateEventElementSubstate : BioStateEventElement
	{
		/// <summary>
		/// </summary>
		public const int DefaultGlobalBool = 0;

		/// <summary>
		/// </summary>
		public new const int DefaultInstanceVersion = 0;

		/// <summary>
		/// </summary>
		public const bool DefaultNewState = false;

		/// <summary>
		/// </summary>
		public const int DefaultParentIndex = 0;

		/// <summary>
		/// </summary>
		public const bool DefaultParentTypeOr = false;

		/// <summary>
		/// </summary>
		public const bool DefaultUseParam = false;

		private int _globalBool;
		private bool _newState;
		private int _parentIndex;
		private bool _parentTypeOr;
		private IList<int> _siblingIndices;
		private bool _useParam;

		/// <summary>
		/// </summary>
		/// <param name="globalBool"></param>
		/// <param name="newState"></param>
		/// <param name="parentIndex"></param>
		/// <param name="parentTypeOr"></param>
		/// <param name="useParam"></param>
		/// <param name="siblingIndices"></param>
		/// <param name="instanceVersion"></param>
		public BioStateEventElementSubstate(int globalBool = DefaultGlobalBool, bool newState = DefaultNewState, int parentIndex = DefaultParentIndex,
			bool parentTypeOr = DefaultParentTypeOr, bool useParam = DefaultUseParam, IList<int> siblingIndices = null,
			int instanceVersion = DefaultInstanceVersion)
			: base(instanceVersion)
		{
			GlobalBool = globalBool;
			NewState = newState;
			ParentIndex = parentIndex;
			ParentTypeOr = parentTypeOr;
			SiblingIndices = siblingIndices ?? new List<int>();
			UseParam = useParam;
		}

		/// <summary>
		/// </summary>
		/// <param name="other"></param>
		public BioStateEventElementSubstate(BioStateEventElementSubstate other)
			: base(other)
		{
			GlobalBool = other.GlobalBool;
			NewState = other.NewState;
			ParentIndex = other.ParentIndex;
			ParentTypeOr = other.ParentTypeOr;
			SiblingIndices = other.SiblingIndices ?? new List<int>();
			UseParam = other.UseParam;
		}

		/// <summary>
		/// </summary>
		public override BioStateEventElementType ElementType
		{
			get { return BioStateEventElementType.Substate; }
		}

		/// <summary>
		/// </summary>
		public int GlobalBool
		{
			get { return _globalBool; }
			set { SetProperty(ref _globalBool, value); }
		}

		/// <summary>
		/// </summary>
		public bool NewState
		{
			get { return _newState; }
			set { SetProperty(ref _newState, value); }
		}

		/// <summary>
		/// </summary>
		public int ParentIndex
		{
			get { return _parentIndex; }
			set { SetProperty(ref _parentIndex, value); }
		}

		/// <summary>
		/// </summary>
		public bool ParentTypeOr
		{
			get { return _parentTypeOr; }
			set { SetProperty(ref _parentTypeOr, value); }
		}

		/// <summary>
		/// </summary>
		public IList<int> SiblingIndices
		{
			get { return _siblingIndices; }
			set { SetProperty(ref _siblingIndices, value); }
		}

		/// <summary>
		/// </summary>
		public bool UseParam
		{
			get { return _useParam; }
			set { SetProperty(ref _useParam, value); }
		}
	}
}
