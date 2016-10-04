using System.Collections.Generic;
using System.Linq;

namespace Gammtek.Conduit.MassEffect3.SFXGame.StateEventMap
{
	/// <summary>
	/// </summary>
	public class BioStateEvent : BioVersionedNativeObject
	{
		/// <summary>
		/// </summary>
		public new const int DefaultInstanceVersion = BioVersionedNativeObject.DefaultInstanceVersion;

		private IList<BioStateEventElement> _elements;

		/// <summary>
		/// </summary>
		/// <param name="elements"></param>
		/// <param name="instanceVersion"></param>
		public BioStateEvent(IList<BioStateEventElement> elements = null, int instanceVersion = DefaultInstanceVersion)
			: base(instanceVersion)
		{
			Elements = elements ?? new List<BioStateEventElement>();
		}

		/// <summary>
		/// </summary>
		/// <param name="other"></param>
		public BioStateEvent(BioStateEvent other)
			: base(other)
		{
			Elements = new List<BioStateEventElement>();

			if (other.Elements == null)
			{
				return;
			}

			foreach (var element in other.Elements)
			{
				switch (element.ElementType)
				{
					case BioStateEventElementType.Bool:
					{
						Elements.Add(new BioStateEventElementBool(element as BioStateEventElementBool));

						break;
					}
					case BioStateEventElementType.Consequence:
					{
						Elements.Add(new BioStateEventElementConsequence(element as BioStateEventElementConsequence));

						break;
					}
					case BioStateEventElementType.Float:
					{
						Elements.Add(new BioStateEventElementFloat(element as BioStateEventElementFloat));

						break;
					}
					case BioStateEventElementType.Function:
					{
						Elements.Add(new BioStateEventElementFunction(element as BioStateEventElementFunction));

						break;
					}
					case BioStateEventElementType.Int:
					{
						Elements.Add(new BioStateEventElementInt(element as BioStateEventElementInt));

						break;
					}
					case BioStateEventElementType.LocalBool:
					{
						Elements.Add(new BioStateEventElementLocalBool(element as BioStateEventElementLocalBool));

						break;
					}
					case BioStateEventElementType.LocalFloat:
					{
						Elements.Add(new BioStateEventElementLocalFloat(element as BioStateEventElementLocalFloat));

						break;
					}
					case BioStateEventElementType.LocalInt:
					{
						Elements.Add(new BioStateEventElementLocalInt(element as BioStateEventElementLocalInt));

						break;
					}
					case BioStateEventElementType.Substate:
					{
						Elements.Add(new BioStateEventElementSubstate(element as BioStateEventElementSubstate));

						break;
					}
				}
			}
		}

		/// <summary>
		/// </summary>
		public IList<BioStateEventElement> Elements
		{
			get { return _elements; }
			set { SetProperty(ref _elements, value); }
		}

		/// <summary>
		/// </summary>
		public bool HasElements
		{
			get { return Elements.Any(); }
		}

        public override string ToString()
        {
            return "StateEvent";
        }
    }
}
