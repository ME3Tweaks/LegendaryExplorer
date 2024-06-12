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
        protected override void Serialize(SerializingContainer2 sc)
        {
            sc.Serialize(ref StateEvents, SCExt.Serialize);
        }

        public static BioStateEventMap Create()
        {
            return new()
            {
                StateEvents = new List<BioStateEvent>()
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

    public static partial class SCExt
    {
        public static void Serialize(this SerializingContainer2 sc, ref BioStateEvent stateEvent)
        {
            if (sc.IsLoading) stateEvent = new BioStateEvent();
            sc.Serialize(ref stateEvent.ID);
            sc.Serialize(ref stateEvent.InstanceVersion);
            sc.Serialize(ref stateEvent.Elements, Serialize);
        }

        public static void Serialize(this SerializingContainer2 sc, ref BioStateEventElement element)
        {
            if (sc.IsLoading)
            {
                var type = (BioStateEventElementType)sc.ms.ReadInt32();
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
            else sc.ms.Writer.WriteInt32((int)element.Type);

            sc.Serialize(ref element.InstanceVersion);

            switch (element)
            {
                case BioStateEventElementBool elementBool:
                    sc.Serialize(ref elementBool);
                    break;
                case BioStateEventElementConsequence elementConsequence:
                    sc.Serialize(ref elementConsequence);
                    break;
                case BioStateEventElementFloat elementFloat:
                    sc.Serialize(ref elementFloat);
                    break;
                case BioStateEventElementFunction elementFunction:
                    sc.Serialize(ref elementFunction);
                    break;
                case BioStateEventElementInt elementInt:
                    sc.Serialize(ref elementInt);
                    break;
                case BioStateEventElementLocalBool elementLBool:
                    sc.Serialize(ref elementLBool);
                    break;
                case BioStateEventElementLocalFloat elementLFloat:
                    sc.Serialize(ref elementLFloat);
                    break;
                case BioStateEventElementLocalInt elementLInt:
                    sc.Serialize(ref elementLInt);
                    break;
                case BioStateEventElementSubstate elementSubstate:
                    sc.Serialize(ref elementSubstate);
                    break;
            }
        }

        public static void Serialize(this SerializingContainer2 sc, ref BioStateEventElementBool element)
        {
            sc.Serialize(ref element.GlobalBool);
            sc.Serialize(ref element.NewState);
            sc.Serialize(ref element.UseParam);
        }

        public static void Serialize(this SerializingContainer2 sc, ref BioStateEventElementConsequence element)
        {
            sc.Serialize(ref element.Consequence);
        }

        public static void Serialize(this SerializingContainer2 sc, ref BioStateEventElementFloat element)
        {
            if (sc.Game.IsGame2())
            {
                sc.Serialize(ref element.Increment);
                sc.Serialize(ref element.GlobalFloat);
                sc.Serialize(ref element.NewValue);
                sc.Serialize(ref element.UseParam);
            }
            else
            {
                sc.Serialize(ref element.GlobalFloat);
                sc.Serialize(ref element.NewValue);
                sc.Serialize(ref element.UseParam);
                sc.Serialize(ref element.Increment);
            }
        }

        public static void Serialize(this SerializingContainer2 sc, ref BioStateEventElementFunction element)
        {
            sc.Serialize(ref element.PackageName);
            sc.Serialize(ref element.ClassName);
            sc.Serialize(ref element.FunctionName);
            sc.Serialize(ref element.Parameter);
        }

        public static void Serialize(this SerializingContainer2 sc, ref BioStateEventElementInt element)
        {
            sc.Serialize(ref element.GlobalInt);
            sc.Serialize(ref element.NewValue);
            sc.Serialize(ref element.UseParam);
            sc.Serialize(ref element.Increment);
        }

        public static void Serialize(this SerializingContainer2 sc, ref BioStateEventElementLocalBool element)
        {
            sc.Serialize(ref element.ObjectTag);
            sc.Serialize(ref element.FunctionName);
            sc.Serialize(ref element.ObjectType);
            sc.Serialize(ref element.UseParam);
            sc.Serialize(ref element.NewValue);
        }

        public static void Serialize(this SerializingContainer2 sc, ref BioStateEventElementLocalFloat element)
        {
            sc.Serialize(ref element.ObjectTag);
            sc.Serialize(ref element.FunctionName);
            sc.Serialize(ref element.ObjectType);
            sc.Serialize(ref element.UseParam);
            sc.Serialize(ref element.NewValue);
        }

        public static void Serialize(this SerializingContainer2 sc, ref BioStateEventElementLocalInt element)
        {
            sc.Serialize(ref element.ObjectTag);
            sc.Serialize(ref element.FunctionName);
            sc.Serialize(ref element.ObjectType);
            sc.Serialize(ref element.UseParam);
            sc.Serialize(ref element.NewValue);
        }

        public static void Serialize(this SerializingContainer2 sc, ref BioStateEventElementSubstate element)
        {
            sc.Serialize(ref element.GlobalBool);
            sc.Serialize(ref element.NewState);
            sc.Serialize(ref element.UseParam);
            sc.Serialize(ref element.ParentTypeOr);
            sc.Serialize(ref element.ParentIndex);
            sc.Serialize(ref element.SiblingIndices, Serialize);
        }
    }
}