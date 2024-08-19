using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;

namespace LegendaryExplorerCore.Pathing
{
    public static class PathTools
    {
        /// <summary>
        /// Modifies the incoming properties collection to update the spec size
        /// </summary>
        /// <param name="specProperties"></param>
        /// <param name="radius"></param>
        /// <param name="height"></param>
        public static void SetReachSpecSize(PropertyCollection specProperties, int radius, int height)
        {
            IntProperty radiusProp = specProperties.GetProp<IntProperty>("CollisionRadius");
            IntProperty heightProp = specProperties.GetProp<IntProperty>("CollisionHeight");
            if (radiusProp != null)
            {
                radiusProp.Value = radius;
            }
            if (heightProp != null)
            {
                heightProp.Value = height;
            }
        }

        /// <summary>
        /// Sets the reach spec size and commits the results back to the export
        /// </summary>
        /// <param name="spec"></param>
        /// <param name="radius"></param>
        /// <param name="height"></param>
        public static void SetReachSpecSize(ExportEntry spec, int radius, int height)
        {
            PropertyCollection specProperties = spec.GetProperties();
            SetReachSpecSize(specProperties, radius, height);
            spec.WriteProperties(specProperties); //write it back.
        }

        public static void CreateReachSpec(ExportEntry startNode, bool createTwoWay, ExportEntry destinationNode, string reachSpecClass, float radius, float height, Guid? destinationReference = null, Point3D destinationPosition = null, PackageCache cache = null)
        {
            IMEPackage Pcc = startNode.FileRef;
            ExportEntry reachSpec = ExportCreator.CreateExport(startNode.FileRef, reachSpecClass, reachSpecClass, startNode.FileRef.FindEntry("TheWorld.PersistentLevel"), cache: cache);
            var props = reachSpec.GetProperties();
            if (destinationNode == null && destinationPosition != null) //EXTERNAL
            {
                //external node

                ////Debug.WriteLine("Num Exports: " + pcc.Exports.Count);
                //if (reachSpec != null)
                //{
                //    ExportEntry outgoingSpec = EntryCloner.CloneEntry(reachSpec);

                //    IEntry reachSpecClassImp = GetEntryOrAddImport(Pcc, reachSpecClass); //new class type.

                //    outgoingSpec.Class = reachSpecClassImp;
                //    outgoingSpec.ObjectName = reachSpecClassImp.ObjectName;

                //    var properties = outgoingSpec.GetProperties();
                //    ObjectProperty outgoingSpecStartProp = properties.GetProp<ObjectProperty>("Start"); //START
                //    StructProperty outgoingEndStructProp = properties.GetProp<StructProperty>("End"); //Embeds END
                //    ObjectProperty outgoingSpecEndProp = outgoingEndStructProp.Properties.GetProp<ObjectProperty>(PathEdUtils.GetReachSpecEndName(outgoingSpec)); //END
                //    outgoingSpecStartProp.Value = startNode.UIndex;
                //    outgoingSpecEndProp.Value = 0;
                //    var endGuid = outgoingEndStructProp.GetProp<StructProperty>("Guid");
                //    endGuid.Properties = externalGUIDProperties; //set the other guid values to our guid values

                //    //Add to source node prop
                //    ArrayProperty<ObjectProperty> PathList = startNode.GetProperty<ArrayProperty<ObjectProperty>>("PathList");
                //    PathList.Add(new ObjectProperty(outgoingSpec.UIndex));
                //    startNode.WriteProperty(PathList);
                //    outgoingSpec.WriteProperties(properties);

                //    //Write Spec Size
                //    PathEdUtils.SetReachSpecSize(outgoingSpec, size.SpecRadius, size.SpecHeight);

                //    //Reindex reachspecs.
                //    PathEdUtils.ReindexMatchingObjects(outgoingSpec);
                //}
            }
            else if (destinationNode != null)
            {
                //Debug.WriteLine("Source Node: " + startNode.Index);

                //Debug.WriteLine("Num Exports: " + pcc.Exports.Count);
                //int outgoingSpec = pcc.ExportCount;
                //int incomingSpec = pcc.ExportCount + 1;

                if (reachSpecClass == "SlotToSlotReachSpec")
                {
                    // When creating two way for coverslots (since they will always be linked) the initial call is TwoWay (right) and the follow up call is not TwoWay (left)
                    props.AddOrReplaceProp(new ByteProperty(createTwoWay ? (byte)2 : (byte)1, "SpecDirection"));
                }

                props.AddOrReplaceProp(CreateActorReference(Guid.Empty, destinationNode, "End"));

                var distance = GetLocation(startNode).GetDistanceToOtherPoint(GetLocation(destinationNode));
                var direction = GetLocation(startNode).GetDirectionToOtherPoint(GetLocation(destinationNode), distance);
                props.AddOrReplaceProp(CommonStructs.Vector3Prop(direction, "Direction"));
                props.AddOrReplaceProp(new ObjectProperty(startNode, "Start"));
                props.AddOrReplaceProp(new IntProperty((int)distance, "Distance"));
                props.AddOrReplaceProp(new IntProperty((int)radius, "CollisionRadius"));
                props.AddOrReplaceProp(new IntProperty((int)height, "CollisionHeight"));
                props.AddOrReplaceProp(new IntProperty(1, "reachFlags"));

                reachSpec.WriteProperties(props);

                if (createTwoWay)
                {
                    // Generate return reachspec
                    CreateReachSpec(destinationNode, false, startNode, reachSpecClass, radius, height, cache: cache);
                }






                //if (reachSpec != null)
                //{
                //    ExportEntry outgoingSpec = EntryCloner.CloneEntry(reachSpec);
                //    ExportEntry incomingSpec = null;
                //    if (createTwoWay)
                //    {
                //        incomingSpec = EntryCloner.CloneEntry(reachSpec);
                //    }

                //    IEntry reachSpecClassImp = GetEntryOrAddImport(Pcc, reachSpecClass); //new class type.

                //    outgoingSpec.Class = reachSpecClassImp;
                //    outgoingSpec.ObjectName = reachSpecClassImp.ObjectName;

                //    var outgoingSpecProperties = outgoingSpec.GetProperties();
                //    if (reachSpecClass == "Engine.SlotToSlotReachSpec")
                //    {
                //        outgoingSpecProperties.Add(new ByteProperty(1, "SpecDirection")); //We might need to find a way to support this edit
                //    }

                //    //Debug.WriteLine("Outgoing UIndex: " + outgoingSpecExp.UIndex);

                //    ObjectProperty outgoingSpecStartProp = outgoingSpecProperties.GetProp<ObjectProperty>("Start"); //START
                //    StructProperty outgoingEndStructProp = outgoingSpecProperties.GetProp<StructProperty>("End"); //Embeds END
                //    ObjectProperty outgoingSpecEndProp = outgoingEndStructProp.Properties.GetProp<ObjectProperty>(PathEdUtils.GetReachSpecEndName(outgoingSpec)); //END
                //    outgoingSpecStartProp.Value = startNode.UIndex;
                //    outgoingSpecEndProp.Value = destinationNode.UIndex;

                //    //Add to source node prop
                //    var PathList = startNode.GetProperty<ArrayProperty<ObjectProperty>>("PathList");
                //    PathList.Add(new ObjectProperty(outgoingSpec.UIndex));
                //    startNode.WriteProperty(PathList);

                //    //Write Spec Size
                //    SetReachSpecSize(outgoingSpecProperties, size.SpecRadius, size.SpecHeight);
                //    outgoingSpec.WriteProperties(outgoingSpecProperties);

                //    if (createTwoWay)
                //    {
                //        incomingSpec.Class = reachSpecClassImp;
                //        incomingSpec.ObjectName = reachSpecClassImp.ObjectName;
                //        var incomingSpecProperties = incomingSpec.GetProperties();
                //        if (reachSpecClass == "Engine.SlotToSlotReachSpec")
                //        {
                //            incomingSpecProperties.Add(new ByteProperty(2, "SpecDirection"));
                //        }

                //        ObjectProperty incomingSpecStartProp = incomingSpecProperties.GetProp<ObjectProperty>("Start"); //START
                //        StructProperty incomingEndStructProp = incomingSpecProperties.GetProp<StructProperty>("End"); //Embeds END
                //        ObjectProperty incomingSpecEndProp = incomingEndStructProp.Properties.GetProp<ObjectProperty>(PathEdUtils.GetReachSpecEndName(incomingSpec)); //END

                //        incomingSpecStartProp.Value = destinationNode.UIndex; //Uindex
                //        incomingSpecEndProp.Value = startNode.UIndex;

                //        //Add reachspec to destination node's path list (returning)
                //        var DestPathList = destinationNode.GetProperty<ArrayProperty<ObjectProperty>>("PathList");
                //        DestPathList.Add(new ObjectProperty(incomingSpec.UIndex));
                //        destinationNode.WriteProperty(DestPathList);

                //        //destNode.WriteProperty(DestPathList);
                //        SetReachSpecSize(incomingSpecProperties, size.SpecRadius, size.SpecHeight);

                //        incomingSpec.WriteProperties(incomingSpecProperties);
                //    }
                //}
            }
            else
            {
                throw new Exception("Improper setup for CreateReachSpec()");
            }
        }

        private static Property CreateActorReference(Guid guid, ExportEntry actor, NameReference name)
        {
            PropertyCollection pc = new PropertyCollection();
            pc.AddOrReplaceProp(CommonStructs.GuidProp(guid, "Guid"));
            pc.AddOrReplaceProp(new ObjectProperty(actor?.UIndex ?? 0, "Actor"));
            return new StructProperty("ActorReference", pc, name, true);
        }

        /// <summary>
        /// Gets the end name of a ReachSpec for property parsing. ME1 and ME2 use Nav, while ME3 and above use Actor.
        /// </summary>
        /// <param name="export">export used to determine which game is being parsed</param>
        /// <returns>Actor for ME2/ME3/LE, Nav for ME1</returns>
        public static string GetReachSpecEndName(ExportEntry export) => export.FileRef.Game < MEGame.ME3 && export.FileRef.Platform != MEPackage.GamePlatform.PS3 ? "Nav" : "Actor";

        public static Point3D GetLocation(ExportEntry export)
        {
            float x = 0, y = 0, z = int.MinValue;
            if (export.ClassName.Contains("Component") && export.HasParent && export.Parent.ClassName.Contains("CollectionActor"))  //Collection component
            {
                var actorCollection = export.Parent as ExportEntry;
                var collection = GetCollectionItems(actorCollection);

                if (!(collection?.IsEmpty() ?? true))
                {
                    var positions = GetCollectionLocationData(actorCollection);
                    var idx = collection.FindIndex(o => o != null && o.UIndex == export.UIndex);
                    if (idx >= 0)
                    {
                        return new Point3D(positions[idx].X, positions[idx].Y, positions[idx].Z);
                    }
                }
            }
            else
            {
                var prop = export.GetProperty<StructProperty>("location");
                if (prop != null)
                {
                    foreach (var locprop in prop.Properties)
                    {
                        switch (locprop)
                        {
                            case FloatProperty fltProp when fltProp.Name == "X":
                                x = fltProp;
                                break;
                            case FloatProperty fltProp when fltProp.Name == "Y":
                                y = fltProp;
                                break;
                            case FloatProperty fltProp when fltProp.Name == "Z":
                                z = fltProp;
                                break;
                        }
                    }
                    return new Point3D(x, y, z);
                }
            }
            return new Point3D(0, 0, 0);
        }

        public static List<Point3D> GetCollectionLocationData(ExportEntry collectionactor)
        {
            if (!collectionactor.ClassName.Contains("CollectionActor"))
                return null;

            return ((StaticCollectionActor)ObjectBinary.From(collectionactor)).LocalToWorldTransforms
                .Select(localToWorldTransform => (Point3D)localToWorldTransform.Translation).ToList();
        }

        public static List<ExportEntry> GetCollectionItems(ExportEntry smac)
        {
            var collectionItems = new List<ExportEntry>();
            var smacItems = smac.GetProperty<ArrayProperty<ObjectProperty>>(smac.ClassName == "StaticMeshCollectionActor" ? "StaticMeshComponents" : "LightComponents");
            if (smacItems != null)
            {
                //Read exports...
                foreach (ObjectProperty obj in smacItems)
                {
                    if (obj.Value > 0)
                    {
                        ExportEntry item = smac.FileRef.GetUExport(obj.Value);
                        collectionItems.Add(item);
                    }
                    else
                    {
                        //this is a blank entry, or an import, somehow.
                        collectionItems.Add(null);
                    }
                }
                return collectionItems;
            }
            return null;
        }
    }
}
