using System;
using System.Collections.Generic;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using static LegendaryExplorerCore.Unreal.BinaryConverters.BioStateEventMap;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class BioStateEventMap : ObjectBinary
    {
        public List<BioStateEvent> StateEvents;
        protected override void Serialize(SerializingContainer sc)
        {
            sc.Serialize(ref StateEvents, sc.Serialize);
        }

        public static BioStateEventMap Create()
        {
            return new()
            {
                StateEvents = []
            };
        }

        public override List<(NameReference, string)> GetNames(MEGame game)
        {
            var names = base.GetNames(game);

            for (int i = 0; i < StateEvents.Count; i++)
            {
                BioStateEvent stateEvent = StateEvents[i];
                for (int j = 0; j < stateEvent.Elements.Count; j++)
                {
                    BioStateEventElement stateEventElement = stateEvent.Elements[j];
                    switch (stateEventElement)
                    {
                        case BioStateEventElementFunction bioStateEventElementFunction:
                            names.Add(bioStateEventElementFunction.PackageName, $"[{i}] State Transition: [{j}] Transition : Package Name");
                            names.Add(bioStateEventElementFunction.ClassName, $"[{i}] State Transition: [{j}] Transition : Class Name");
                            names.Add(bioStateEventElementFunction.FunctionName, $"[{i}] State Transition: [{j}] Transition : Function Name");
                            break;
                        case BioStateEventElementLocalBool bioStateEventElementLocalBool:
                            names.Add(bioStateEventElementLocalBool.ObjectTag, $"[{i}] State Transition: [{j}] Transition : Object Tag");
                            names.Add(bioStateEventElementLocalBool.FunctionName, $"[{i}] State Transition: [{j}] Transition : Function Name");
                            break;
                        case BioStateEventElementLocalFloat bioStateEventElementLocalFloat:
                            names.Add(bioStateEventElementLocalFloat.ObjectTag, $"[{i}] State Transition: [{j}] Transition : Object Tag");
                            names.Add(bioStateEventElementLocalFloat.FunctionName, $"[{i}] State Transition: [{j}] Transition : Function Name");
                            break;
                        case BioStateEventElementLocalInt bioStateEventElementLocalInt:
                            names.Add(bioStateEventElementLocalInt.ObjectTag, $"[{i}] State Transition: [{j}] Transition : Object Tag");
                            names.Add(bioStateEventElementLocalInt.FunctionName, $"[{i}] State Transition: [{j}] Transition : Function Name");
                            break;
                    }
                }
            }

            return names;
        }

        public class BioStateEvent
        {
            public int ID;
            public int InstanceVersion;
            public List<BioStateEventElement> Elements;
        }

        public enum BioStateEventElementType
        {
            Bool = 0,
            Consequence = 1,
            Float = 2,
            Function = 3,
            Int = 4,
            LocalBool = 5,
            LocalFloat = 6,
            LocalInt = 7,
            Substate = 8,
            Max = 9
        }

        public class BioStateEventElement
        {
            public int InstanceVersion;
            public BioStateEventElementType Type;
        }

        public class BioStateEventElementBool : BioStateEventElement
        {
            public int GlobalBool;
            public bool NewState;
            public bool UseParam;
        }

        public class BioStateEventElementConsequence : BioStateEventElement
        {
            public int Consequence;
        }

        public class BioStateEventElementFloat : BioStateEventElement
        {
            public int GlobalFloat;
            public float NewValue;
            public bool UseParam;
            public bool Increment;
        }

        public class BioStateEventElementFunction : BioStateEventElement
        {
            public NameReference PackageName;
            public NameReference ClassName;
            public NameReference FunctionName;
            public int Parameter;
        }

        public class BioStateEventElementInt : BioStateEventElement
        {
            public int GlobalInt;
            public int NewValue;
            public bool UseParam;
            public bool Increment;
        }

        public class BioStateEventElementLocalBool : BioStateEventElement
        {
            public NameReference ObjectTag;
            public NameReference FunctionName;
            public int ObjectType;
            public bool UseParam;
            public bool NewValue;
        }

        public class BioStateEventElementLocalFloat : BioStateEventElement
        {
            public NameReference ObjectTag;
            public NameReference FunctionName;
            public int ObjectType;
            public bool UseParam;
            public float NewValue;
        }

        public class BioStateEventElementLocalInt : BioStateEventElement
        {
            public NameReference ObjectTag;
            public NameReference FunctionName;
            public int ObjectType;
            public bool UseParam;
            public int NewValue;
        }

        public class BioStateEventElementSubstate : BioStateEventElement
        {
            public int GlobalBool;
            public bool NewState;
            public bool UseParam;
            public bool ParentTypeOr;
            public int ParentIndex;
            public List<int> SiblingIndices;
        }
    }

    public partial class SerializingContainer
    {
        public void Serialize(ref BioStateEvent stateEvent)
        {
            if (IsLoading) stateEvent = new BioStateEvent();
            Serialize(ref stateEvent.ID);
            Serialize(ref stateEvent.InstanceVersion);
            Serialize(ref stateEvent.Elements, Serialize);
        }

        public void Serialize(ref BioStateEventElement element)
        {
            if (IsLoading)
            {
                var type = (BioStateEventElementType)ms.ReadInt32();
                switch (type)
                {
                    case BioStateEventElementType.Bool:
                        element = new BioStateEventElementBool();
                        break;
                    case BioStateEventElementType.Consequence:
                        element = new BioStateEventElementConsequence();
                        break;
                    case BioStateEventElementType.Float:
                        element = new BioStateEventElementFloat();
                        break;
                    case BioStateEventElementType.Function:
                        element = new BioStateEventElementFunction();
                        break;
                    case BioStateEventElementType.Int:
                        element = new BioStateEventElementInt();
                        break;
                    case BioStateEventElementType.LocalBool:
                        element = new BioStateEventElementLocalBool();
                        break;
                    case BioStateEventElementType.LocalFloat:
                        element = new BioStateEventElementLocalFloat();
                        break;
                    case BioStateEventElementType.LocalInt:
                        element = new BioStateEventElementLocalInt();
                        break;
                    case BioStateEventElementType.Substate:
                        element = new BioStateEventElementSubstate();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();

                }
                element.Type = type;
            }
            else ms.Writer.WriteInt32((int)element.Type);

            Serialize(ref element.InstanceVersion);

            switch (element)
            {
                case BioStateEventElementBool elementBool:
                    Serialize(ref elementBool);
                    break;
                case BioStateEventElementConsequence elementConsequence:
                    Serialize(ref elementConsequence);
                    break;
                case BioStateEventElementFloat elementFloat:
                    Serialize(ref elementFloat);
                    break;
                case BioStateEventElementFunction elementFunction:
                    Serialize(ref elementFunction);
                    break;
                case BioStateEventElementInt elementInt:
                    Serialize(ref elementInt);
                    break;
                case BioStateEventElementLocalBool elementLBool:
                    Serialize(ref elementLBool);
                    break;
                case BioStateEventElementLocalFloat elementLFloat:
                    Serialize(ref elementLFloat);
                    break;
                case BioStateEventElementLocalInt elementLInt:
                    Serialize(ref elementLInt);
                    break;
                case BioStateEventElementSubstate elementSubstate:
                    Serialize(ref elementSubstate);
                    break;
            }
        }

        public void Serialize(ref BioStateEventElementBool element)
        {
            Serialize(ref element.GlobalBool);
            Serialize(ref element.NewState);
            Serialize(ref element.UseParam);
        }

        public void Serialize(ref BioStateEventElementConsequence element)
        {
            Serialize(ref element.Consequence);
        }

        public void Serialize(ref BioStateEventElementFloat element)
        {
            if (Game.IsGame2())
            {
                Serialize(ref element.Increment);
                Serialize(ref element.GlobalFloat);
                Serialize(ref element.NewValue);
                Serialize(ref element.UseParam);
            }
            else
            {
                Serialize(ref element.GlobalFloat);
                Serialize(ref element.NewValue);
                Serialize(ref element.UseParam);
                Serialize(ref element.Increment);
            }
        }

        public void Serialize(ref BioStateEventElementFunction element)
        {
            Serialize(ref element.PackageName);
            Serialize(ref element.ClassName);
            Serialize(ref element.FunctionName);
            Serialize(ref element.Parameter);
        }

        public void Serialize(ref BioStateEventElementInt element)
        {
            Serialize(ref element.GlobalInt);
            Serialize(ref element.NewValue);
            Serialize(ref element.UseParam);
            Serialize(ref element.Increment);
        }

        public void Serialize(ref BioStateEventElementLocalBool element)
        {
            Serialize(ref element.ObjectTag);
            Serialize(ref element.FunctionName);
            Serialize(ref element.ObjectType);
            Serialize(ref element.UseParam);
            Serialize(ref element.NewValue);
        }

        public void Serialize(ref BioStateEventElementLocalFloat element)
        {
            Serialize(ref element.ObjectTag);
            Serialize(ref element.FunctionName);
            Serialize(ref element.ObjectType);
            Serialize(ref element.UseParam);
            Serialize(ref element.NewValue);
        }

        public void Serialize(ref BioStateEventElementLocalInt element)
        {
            Serialize(ref element.ObjectTag);
            Serialize(ref element.FunctionName);
            Serialize(ref element.ObjectType);
            Serialize(ref element.UseParam);
            Serialize(ref element.NewValue);
        }

        public void Serialize(ref BioStateEventElementSubstate element)
        {
            Serialize(ref element.GlobalBool);
            Serialize(ref element.NewState);
            Serialize(ref element.UseParam);
            Serialize(ref element.ParentTypeOr);
            Serialize(ref element.ParentIndex);
            Serialize(ref element.SiblingIndices, Serialize);
        }
    }
}