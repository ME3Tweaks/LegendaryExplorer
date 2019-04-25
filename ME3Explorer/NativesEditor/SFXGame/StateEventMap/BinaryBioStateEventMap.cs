using System;
using System.Collections.Generic;
using System.IO;
using Gammtek.Conduit.Extensions;
using Gammtek.Conduit.IO;

namespace Gammtek.Conduit.MassEffect3.SFXGame.StateEventMap
{
	public class BinaryBioStateEventMap : BioStateEventMap
	{
		private long _stateEventsOffset;

		public BinaryBioStateEventMap(IDictionary<int, BioStateEvent> events = null)
			: base(events) {}

		public long StateEventsOffset
		{
			get => _stateEventsOffset;
		    set => SetProperty(ref _stateEventsOffset, value);
		}

		public static BinaryBioStateEventMap Load(string path)
		{
			if (string.IsNullOrEmpty(path))
			{
				throw new ArgumentNullException(nameof(path));
			}

			return !File.Exists(path) ? null : Load(File.Open(path, FileMode.Open));
		}

		public static BinaryBioStateEventMap Load(Stream stream)
		{
			if (stream == null)
			{
				throw new ArgumentNullException(nameof(stream));
			}

			using (var reader = new BioStateEventMapReader(stream))
			{
				var map = new BinaryBioStateEventMap();

				var eventsCount = reader.ReadInt32();
				map.StateEvents = new Dictionary<int, BioStateEvent>();

				for (var i = 0; i < eventsCount; i++)
				{
					var id = reader.ReadInt32();
					var stateEvent = reader.ReadStateEvent();

					if (!map.StateEvents.ContainsKey(id))
					{
						map.StateEvents.Add(id, stateEvent);
					}
					else
					{
						map.StateEvents[id] = stateEvent;
					}
				}

				return map;
			}
		}

		public void Save(string path)
		{
			if (string.IsNullOrEmpty(path))
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

			using (var writer = new BioStateEventMapWriter(stream))
			{
				// Sections
				writer.Write(StateEvents.Count);

				foreach (var stateEvent in StateEvents)
				{
					writer.Write(stateEvent.Key);
					writer.Write(stateEvent.Value);
				}
			}
		}

		public class BioStateEventMapReader : DataReader
		{
			public BioStateEventMapReader(Stream stream)
				: base(stream) { }

			public BioStateEvent ReadStateEvent()
			{
				var stateEvent = new BioStateEvent
				{
					InstanceVersion = ReadInt32()
				};

				var eventElementsCount = ReadInt32();
				stateEvent.Elements = new List<BioStateEventElement>();

				for (var i = 0; i < eventElementsCount; i++)
				{
					var elementType = (BioStateEventElementType)ReadInt32();
					BioStateEventElement element;

					switch (elementType)
					{
						case BioStateEventElementType.Bool:
							{
								element = ReadEventElementBool();

								break;
							}
						case BioStateEventElementType.Consequence:
							{
								element = ReadEventElementConsequence();

								break;
							}
						case BioStateEventElementType.Float:
							{
								element = ReadEventElementFloat();

								break;
							}
						case BioStateEventElementType.Function:
							{
								element = ReadEventElementFunction();

								break;
							}
						case BioStateEventElementType.Int:
							{
								element = ReadEventElementInt();

								break;
							}
						case BioStateEventElementType.LocalBool:
							{
								element = ReadEventElementLocalBool();

								break;
							}
						case BioStateEventElementType.LocalFloat:
							{
								element = ReadEventElementLocalFloat();

								break;
							}
						case BioStateEventElementType.LocalInt:
							{
								element = ReadEventElementLocalInt();

								break;
							}
						case BioStateEventElementType.Substate:
							{
								element = ReadEventElementSubstate();

								break;
							}
						default:
							{
								throw new ArgumentOutOfRangeException();
							}
					}

					stateEvent.Elements.Add(element);
				}

				return stateEvent;
			}

			protected void ReadBioStateEventElement(BioStateEventElement element)
			{
				if (element == null)
				{
					throw new ArgumentNullException(nameof(element));
				}

				element.InstanceVersion = ReadInt32();
			}

			public BioStateEventElementBool ReadEventElementBool()
			{
				var element = new BioStateEventElementBool();

				ReadBioStateEventElement(element);

				element.GlobalBool = ReadInt32();
				element.NewState = ReadInt32().ToBoolean();
				element.UseParam = ReadInt32().ToBoolean();

				return element;
			}

			public BioStateEventElementConsequence ReadEventElementConsequence()
			{
				var element = new BioStateEventElementConsequence();

				ReadBioStateEventElement(element);

				element.Consequence = ReadInt32();

				return element;
			}

			public BioStateEventElementFloat ReadEventElementFloat()
			{
				var element = new BioStateEventElementFloat();

				ReadBioStateEventElement(element);

				element.GlobalFloat = ReadInt32();
				element.NewValue = ReadSingle();
				element.UseParam = ReadInt32().ToBoolean();
				element.Increment = ReadInt32().ToBoolean();

				return element;
			}

			public BioStateEventElementFunction ReadEventElementFunction()
			{
				var element = new BioStateEventElementFunction();

				ReadBioStateEventElement(element);

				element.FunctionPackageNameFlags = ReadInt32();
				element.FunctionPackageName = ReadInt32();

				element.FunctionClassNameFlags = ReadInt32();
				element.FunctionClassName = ReadInt32();

				element.FunctionNameFlags = ReadInt32();
				element.FunctionName = ReadInt32();

				element.Parameter = ReadInt32();

				return element;
			}

			public BioStateEventElementInt ReadEventElementInt()
			{
				var element = new BioStateEventElementInt();

				ReadBioStateEventElement(element);

				element.GlobalInt = ReadInt32();
				element.NewValue = ReadInt32();
				element.UseParam = ReadInt32().ToBoolean();
				element.Increment = ReadInt32().ToBoolean();

				return element;
			}

			protected void ReadBioStateEventElementLocal(BioStateEventElementLocal element)
			{
				if (element == null)
				{
					throw new ArgumentNullException(nameof(element));
				}

				ReadBioStateEventElement(element);

				element.ObjectTagFlags = ReadInt32();
				element.ObjectTag = ReadInt32();

				element.FunctionNameFlags = ReadInt32();
				element.FunctionName = ReadInt32();

				element.ObjectType = ReadInt32();
				element.UseParam = ReadInt32().ToBoolean();
			}

			public BioStateEventElementLocalBool ReadEventElementLocalBool()
			{
				var element = new BioStateEventElementLocalBool();

				ReadBioStateEventElementLocal(element);

				element.NewValue = ReadInt32().ToBoolean();

				return element;
			}

			public BioStateEventElementLocalFloat ReadEventElementLocalFloat()
			{
				var element = new BioStateEventElementLocalFloat();

				ReadBioStateEventElementLocal(element);

				element.NewValue = ReadSingle();

				return element;
			}

			public BioStateEventElementLocalInt ReadEventElementLocalInt()
			{
				var element = new BioStateEventElementLocalInt();

				ReadBioStateEventElementLocal(element);

				element.NewValue = ReadInt32();

				return element;
			}

			public BioStateEventElementSubstate ReadEventElementSubstate()
			{
				var element = new BioStateEventElementSubstate();

				ReadBioStateEventElement(element);

				element.GlobalBool = ReadInt32();
				element.NewState = ReadInt32().ToBoolean();
				element.UseParam = ReadInt32().ToBoolean();
				element.ParentTypeOr = ReadInt32().ToBoolean();
				element.ParentIndex = ReadInt32();

				var siblingIndicesCount = ReadInt32();
				element.SiblingIndices = new List<int>();

				for (var i = 0; i < siblingIndicesCount; i++)
				{
					element.SiblingIndices.Add(ReadInt32());
				}

				return element;
			}
		}

		public class BioStateEventMapWriter : DataWriter
		{
			public new static readonly BioStateEventMapWriter Null = new BioStateEventMapWriter();

			protected BioStateEventMapWriter() { }

			public BioStateEventMapWriter(Stream output)
				: base(output) { }

			public void Write(BioStateEvent stateEvent)
			{
				if (stateEvent == null)
				{
					throw new ArgumentNullException(nameof(stateEvent));
				}

				//
				Write(stateEvent.InstanceVersion);

				//
				Write(stateEvent.Elements.Count);

				foreach (var element in stateEvent.Elements)
				{
					Write((int)element.ElementType);

					switch (element.ElementType)
					{
						case BioStateEventElementType.Bool:
							{
								Write(element as BioStateEventElementBool);

								break;
							}
						case BioStateEventElementType.Consequence:
							{
								Write(element as BioStateEventElementConsequence);

								break;
							}
						case BioStateEventElementType.Float:
							{
								Write(element as BioStateEventElementFloat);

								break;
							}
						case BioStateEventElementType.Function:
							{
								Write(element as BioStateEventElementFunction);

								break;
							}
						case BioStateEventElementType.Int:
							{
								Write(element as BioStateEventElementInt);

								break;
							}
						case BioStateEventElementType.LocalBool:
							{
								Write(element as BioStateEventElementLocalBool);

								break;
							}
						case BioStateEventElementType.LocalFloat:
							{
								Write(element as BioStateEventElementLocalFloat);

								break;
							}
						case BioStateEventElementType.LocalInt:
							{
								Write(element as BioStateEventElementLocalInt);

								break;
							}
						case BioStateEventElementType.Substate:
							{
								Write(element as BioStateEventElementSubstate);

								break;
							}
						default:
							{
								throw new ArgumentOutOfRangeException();
							}
					}
				}
			}

			protected void WriteEventElement(BioStateEventElement element)
			{
				if (element == null)
				{
					throw new ArgumentNullException(nameof(element));
				}

				Write(element.InstanceVersion);
			}

			public void Write(BioStateEventElementBool element)
			{
				WriteEventElement(element);

				Write(element.GlobalBool);
				Write(element.NewState.ToInt32());
				Write(element.UseParam.ToInt32());
			}

			public void Write(BioStateEventElementConsequence element)
			{
				WriteEventElement(element);

				Write(element.Consequence);
			}

			public void Write(BioStateEventElementFloat element)
			{
				WriteEventElement(element);

				Write(element.GlobalFloat);
				Write(element.NewValue);
				Write(element.UseParam.ToInt32());
				Write(element.Increment.ToInt32());
			}

			public void Write(BioStateEventElementFunction element)
			{
				WriteEventElement(element);

				Write(element.FunctionPackageNameFlags);
				Write(element.FunctionPackageName);

				Write(element.FunctionClassNameFlags);
				Write(element.FunctionClassName);

				Write(element.FunctionNameFlags);
				Write(element.FunctionName);

				Write(element.Parameter);
			}

			public void Write(BioStateEventElementInt element)
			{
				WriteEventElement(element);

				Write(element.GlobalInt);
				Write(element.NewValue);
				Write(element.UseParam.ToInt32());
				Write(element.Increment.ToInt32());
			}

			protected void WriteEventElementLocal(BioStateEventElementLocal element)
			{
				if (element == null)
				{
					throw new ArgumentNullException(nameof(element));
				}

				WriteEventElement(element);

				Write(element.ObjectTagFlags);
				Write(element.ObjectTag);

				Write(element.FunctionNameFlags);
				Write(element.FunctionName);

				Write(element.ObjectType);
				Write(element.UseParam.ToInt32());
			}

			public void Write(BioStateEventElementLocalBool element)
			{
				WriteEventElementLocal(element);

				Write(element.NewValue.ToInt32());
			}

			public void Write(BioStateEventElementLocalFloat element)
			{
				WriteEventElementLocal(element);

				Write(element.NewValue);
			}

			public void Write(BioStateEventElementLocalInt element)
			{
				WriteEventElementLocal(element);

				Write(element.NewValue);
			}

			public void Write(BioStateEventElementSubstate element)
			{
				WriteEventElement(element);

				Write(element.GlobalBool);
				Write(element.NewState.ToInt32());
				Write(element.UseParam.ToInt32());
				Write(element.ParentTypeOr.ToInt32());
				Write(element.ParentIndex);

				Write(element.SiblingIndices.Count);

				foreach (var index in element.SiblingIndices)
				{
					Write(index);
				}
			}
		}
	}
}
