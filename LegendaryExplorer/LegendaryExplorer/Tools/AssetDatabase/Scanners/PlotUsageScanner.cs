using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;

namespace LegendaryExplorer.Tools.AssetDatabase.Scanners
{ 
    internal class PlotUsageScanner : AssetScanner
    {
        public PlotUsageScanner() : base()
        {
        }

        private readonly HashSet<string> classesWithPlotData = new()
        {
            "BioSeqAct_PMExecuteTransition", "BioSeqAct_PMExecuteConsequence", "BioSeqAct_PMCheckState",
            "BioSeqAct_PMCheckConditional", "BioSeqVar_StoryManagerInt",
            "BioSeqVar_StoryManagerFloat", "BioSeqVar_StoryManagerBool", "BioSeqVar_StoryManagerStateId",
            "SFXSceneShopNodePlotCheck", "BioWorldInfo", "BioStateEventMap", "BioCodexMap", "BioQuestMap", "BioConversation"
        };

        private ConcurrentAssetDB db;

        public override void ScanExport(ExportScanInfo e, ConcurrentAssetDB db, AssetDBScanOptions options)
        {
            if (!options.ScanPlotUsages || !classesWithPlotData.Contains(e.ClassName) || e.IsDefault) return;
            this.db = db;

            switch (e.ClassName)
            {
                case "BioSeqAct_PMExecuteTransition":
                case "BioSeqAct_PMExecuteConsequence":
                {
                    var transition = e.Properties.GetProp<IntProperty>("m_nIndex")?.Value;
                    if(transition.HasValue) AddToTransitionRecord(transition.Value, new PlotUsage(e.FileKey, e.Export.UIndex, e.IsMod, PlotUsageContext.Sequence));
                    break;
                }
                case "BioSeqAct_PMCheckState":
                case "BioSeqVar_StoryManagerBool":
                case "BioSeqVar_StoryManagerStateId":
                {
                    var boolId = e.Properties.GetProp<IntProperty>("m_nIndex")?.Value;
                    if(boolId.HasValue) AddToBoolRecord(boolId.Value, new PlotUsage(e.FileKey, e.Export.UIndex, e.IsMod, PlotUsageContext.Sequence));
                    break;
                }
                case "BioSeqVar_StoryManagerFloat":
                {
                    var floatId = e.Properties.GetProp<IntProperty>("m_nIndex")?.Value;
                    if(floatId.HasValue) AddToFloatRecord(floatId.Value, new PlotUsage(e.FileKey, e.Export.UIndex, e.IsMod, PlotUsageContext.Sequence));
                    break;
                }
                case "BioSeqVar_StoryManagerInt":
                {
                    var intId = e.Properties.GetProp<IntProperty>("m_nIndex")?.Value;
                    if(intId.HasValue) AddToIntRecord(intId.Value, new PlotUsage(e.FileKey, e.Export.UIndex, e.IsMod, PlotUsageContext.Sequence));
                    break;
                }
                case "BioSeqAct_PMCheckConditional":
                {
                    var condId = e.Properties.GetProp<IntProperty>("m_nIndex")?.Value;
                    if(condId.HasValue) AddToConditionalRecord(condId.Value, new PlotUsage(e.FileKey, e.Export.UIndex, e.IsMod, PlotUsageContext.Sequence));
                    break;
                }
                case "BioWorldInfo":
                {
                    var bioWorldCondId = e.Properties.GetProp<IntProperty>("Conditional")?.Value;
                    if(bioWorldCondId.HasValue) AddToConditionalRecord(bioWorldCondId.Value, new PlotUsage(e.FileKey, e.Export.UIndex, e.IsMod));
                    break;
                }
                case "SFXSceneShopNodePlotCheck":
                    var mnId = e.Properties.GetProp<IntProperty>("m_nIndex")?.Value;
                    if (mnId.HasValue && Enum.TryParse(e.Properties.GetProp<EnumProperty>("VarType")?.Value.Name,
                        out ESFXSSPlotVarType type))
                    {
                        switch (type)
                        {
                            case ESFXSSPlotVarType.PlotVar_Float:
                                AddToFloatRecord(mnId.Value, new PlotUsage(e.FileKey, e.Export.UIndex, e.IsMod));
                                break;
                            case ESFXSSPlotVarType.PlotVar_Int:
                                AddToIntRecord(mnId.Value, new PlotUsage(e.FileKey, e.Export.UIndex, e.IsMod));
                                break;
                            case ESFXSSPlotVarType.PlotVar_State:
                                AddToBoolRecord(mnId.Value, new PlotUsage(e.FileKey, e.Export.UIndex, e.IsMod));
                                break;
                        }
                    }
                    break;
                case "BioStateEventMap":
                {
                    BioStateEventMap map = ObjectBinary.From<BioStateEventMap>(e.Export);
                    foreach (var evt in map.StateEvents)
                    {
                        AddBaseUsageToTransition(evt.ID, new PlotUsageWithID(e.FileKey, e.Export.UIndex, e.IsMod, evt.ID, PlotUsageContext.Transition));
                        foreach (var el in evt.Elements)
                        {
                            switch (el)
                            {
                                case BioStateEventMap.BioStateEventElementBool b:
                                    AddToBoolRecord(b.GlobalBool, new PlotUsageWithID(e.FileKey, e.Export.UIndex, e.IsMod, evt.ID, PlotUsageContext.Transition));
                                    break;
                                case BioStateEventMap.BioStateEventElementConsequence c:
                                    AddToTransitionRecord(c.Consequence, new PlotUsageWithID(e.FileKey, e.Export.UIndex, e.IsMod, evt.ID, PlotUsageContext.Transition));
                                    break;
                                case BioStateEventMap.BioStateEventElementFloat f:
                                    AddToFloatRecord(f.GlobalFloat, new PlotUsageWithID(e.FileKey, e.Export.UIndex, e.IsMod, evt.ID, PlotUsageContext.Transition));
                                    break;
                                case BioStateEventMap.BioStateEventElementInt i:
                                    AddToIntRecord(i.GlobalInt, new PlotUsageWithID(e.FileKey, e.Export.UIndex, e.IsMod, evt.ID, PlotUsageContext.Transition));
                                    break;
                                case BioStateEventMap.BioStateEventElementSubstate s:
                                    AddToBoolRecord(s.GlobalBool, new PlotUsageWithID(e.FileKey, e.Export.UIndex, e.IsMod, evt.ID, PlotUsageContext.Transition));
                                    if(s.ParentIndex >= 0) AddToBoolRecord(s.ParentIndex, new PlotUsageWithID(e.FileKey, e.Export.UIndex, e.IsMod, evt.ID, PlotUsageContext.Transition));
                                    foreach (var sib in s.SiblingIndices)
                                    {
                                        AddToBoolRecord(sib, new PlotUsageWithID(e.FileKey, e.Export.UIndex, e.IsMod, evt.ID, PlotUsageContext.Transition));
                                    }
                                    break;
                            }
                        }
                    }
                }

                    break;
                case "BioCodexMap":
                {
                    // The data in this codex map does not seem to be used in game. IDs are not in bool table
                    if (e.Export.ObjectName.Name == "DataManualMap") break;

                    BioCodexMap codexMap = ObjectBinary.From<BioCodexMap>(e.Export);
                    foreach (var page in codexMap.Pages)
                    {
                        AddToBoolRecord(page.ID, new PlotUsage(e.FileKey, e.Export.UIndex, e.IsMod, PlotUsageContext.Codex));
                    }
                    break;
                }
                case "BioQuestMap":
                {
                    BioQuestMap questMap = ObjectBinary.From<BioQuestMap>(e.Export);
                    foreach (var quest in questMap.Quests)
                    {
                        // Parse goals
                        foreach (var goal in quest.Goals)
                        {
                            if(goal.Conditional >= 0) AddToConditionalRecord(goal.Conditional, new PlotUsageWithID(e.FileKey, e.Export.UIndex, e.IsMod, quest.ID, PlotUsageContext.Quest));
                            if(goal.State >= 0) AddToBoolRecord(goal.State, new PlotUsageWithID(e.FileKey, e.Export.UIndex, e.IsMod, quest.ID, PlotUsageContext.Quest));
                        }

                        // Parse plot items
                        foreach (var plotItem in quest.PlotItems)
                        {
                            if(plotItem.Conditional >= 0) AddToConditionalRecord(plotItem.Conditional, new PlotUsageWithID(e.FileKey, e.Export.UIndex, e.IsMod, quest.ID, PlotUsageContext.Quest));
                            // This is not a bool, I don't think
                            //if(plotItem.State >= 0) AddToBoolRecord(plotItem.State, new PlotUsageWithID(FileKey, export.UIndex, IsMod, quest.ID, PlotUsageContext.Quest));
                        }
                    }

                    // Parse Task Evals
                    foreach (var task in questMap.TaskEvals.Concat(questMap.FloatTaskEvals).Concat(questMap.IntTaskEvals).SelectMany(t => t.TaskEvals))
                    {
                        if(task.Conditional >= 0) AddToConditionalRecord(task.Conditional, new PlotUsage(e.FileKey, e.Export.UIndex, e.IsMod, PlotUsageContext.TaskEval));
                        if(task.State >= 0) AddToBoolRecord(task.State, new PlotUsage(e.FileKey, e.Export.UIndex, e.IsMod, PlotUsageContext.TaskEval));
                    }

                    break;
                }
                case "BioConversation":
                {
                    var entries = e.Properties.GetProp<ArrayProperty<StructProperty>>("m_EntryList") ?? new ArrayProperty<StructProperty>("m_EntryList");
                    var replies = e.Properties.GetProp<ArrayProperty<StructProperty>>("m_ReplyList") ?? new ArrayProperty<StructProperty>("m_ReplyList");
                    foreach (var node in entries.Values.Concat(replies.Values))
                    {
                        var strRef = node.GetProp<StringRefProperty>("srText")?.Value ?? -1;
                        var conditional = node.GetProp<IntProperty>("nConditionalFunc")?.Value ?? -1;
                        var transition = node.GetProp<IntProperty>("nStateTransition")?.Value ?? -1;
                        bool isCond = node.GetProp<BoolProperty>("bFireConditional")?.Value ?? true;

                        if (conditional > 0)
                        {
                            if (isCond)
                            {
                                AddToConditionalRecord(conditional, new PlotUsageWithID(e.FileKey, e.Export.UIndex, e.IsMod, strRef, PlotUsageContext.Dialogue));
                            }
                            else
                            {
                                AddToBoolRecord(conditional, new PlotUsageWithID(e.FileKey, e.Export.UIndex, e.IsMod, strRef, PlotUsageContext.Dialogue));
                            }
                        }

                        if (transition > 0)
                        {
                            AddToTransitionRecord(transition, new PlotUsageWithID(e.FileKey, e.Export.UIndex, e.IsMod, strRef, PlotUsageContext.Dialogue));
                        }
                    }
                    break;
                }
            }
        }

        private void AddToBoolRecord(int id, PlotUsage usage)
        {
            if (db.GeneratedBoolRecords.ContainsKey(id))
            {
                var boolrecord = db.GeneratedBoolRecords[id];
                lock (boolrecord) boolrecord.Usages.Add(usage);
            }
            else
            {
                var newBoolRecord = new PlotRecord(PlotRecordType.Bool, id);
                if (db.GeneratedBoolRecords.TryAdd(id, newBoolRecord))
                {
                    var boolrecord = db.GeneratedBoolRecords[id];
                    lock (boolrecord) boolrecord.Usages.Add(usage);
                }
            }
        }

        private void AddToIntRecord(int id, PlotUsage usage)
        {
            if (db.GeneratedIntRecords.ContainsKey(id))
            {
                var intrecord = db.GeneratedIntRecords[id];
                lock (intrecord) intrecord.Usages.Add(usage);
            }
            else
            {
                var newIntRecord = new PlotRecord(PlotRecordType.Int, id);
                if (db.GeneratedIntRecords.TryAdd(id, newIntRecord))
                {
                    var intrecord = db.GeneratedIntRecords[id];
                    lock (intrecord) intrecord.Usages.Add(usage);
                }
            }
        }

        private void AddToFloatRecord(int id, PlotUsage usage)
        {
            if (db.GeneratedFloatRecords.ContainsKey(id))
            {
                var floatrecord = db.GeneratedFloatRecords[id];
                lock (floatrecord) floatrecord.Usages.Add(usage);
            }
            else
            {
                var newFloatRecord = new PlotRecord(PlotRecordType.Float, id);
                if (db.GeneratedFloatRecords.TryAdd(id, newFloatRecord))
                {
                    var floatrecord = db.GeneratedFloatRecords[id];
                    lock (floatrecord) floatrecord.Usages.Add(usage);
                }
            }
        }

        private void AddToConditionalRecord(int id, PlotUsage usage)
        {
            if (db.GeneratedConditionalRecords.ContainsKey(id))
            {
                var conditionalrecord = db.GeneratedConditionalRecords[id];
                lock (conditionalrecord) conditionalrecord.Usages.Add(usage);
            }
            else
            {
                var newConditionalRecord = new PlotRecord(PlotRecordType.Conditional, id);
                if (db.GeneratedConditionalRecords.TryAdd(id, newConditionalRecord))
                {
                    var conditionalrecord = db.GeneratedConditionalRecords[id];
                    lock (conditionalrecord) conditionalrecord.Usages.Add(usage);
                }
            }
        }

        private void AddToTransitionRecord(int id, PlotUsage usage)
        {
            if (db.GeneratedTransitionRecords.ContainsKey(id))
            {
                var transitionrecord = db.GeneratedTransitionRecords[id];
                lock (transitionrecord) transitionrecord.Usages.Add(usage);
            }
            else
            {
                var newTransitionRecord = new PlotRecord(PlotRecordType.Transition, id);
                if (db.GeneratedTransitionRecords.TryAdd(id, newTransitionRecord))
                {
                    var transitionrecord = db.GeneratedTransitionRecords[id];
                    lock (transitionrecord) transitionrecord.Usages.Add(usage);
                }
            }
        }

        private void AddBaseUsageToConditional(int id, PlotUsage usage)
        {
            if (db.GeneratedConditionalRecords.ContainsKey(id))
            {
                var conditionalrecord = db.GeneratedConditionalRecords[id];
                lock (conditionalrecord) conditionalrecord.BaseUsage = usage;
            }
            else
            {
                var newConditionalRecord = new PlotRecord(PlotRecordType.Conditional, id);
                if (db.GeneratedConditionalRecords.TryAdd(id, newConditionalRecord))
                {
                    var conditionalrecord = db.GeneratedConditionalRecords[id];
                    lock (conditionalrecord) conditionalrecord.BaseUsage = usage;
                }
            }
        }

        private void AddBaseUsageToTransition(int id, PlotUsage usage)
        {
            if (db.GeneratedTransitionRecords.ContainsKey(id))
            {
                var transitionrecord = db.GeneratedTransitionRecords[id];
                lock (transitionrecord) transitionrecord.BaseUsage = usage;
            }
            else
            {
                var newTransitionRecord = new PlotRecord(PlotRecordType.Transition, id);
                if (db.GeneratedTransitionRecords.TryAdd(id, newTransitionRecord))
                {
                    var transitionrecord = db.GeneratedTransitionRecords[id];
                    lock (transitionrecord) transitionrecord.BaseUsage = usage;
                }
            }
        }
    }
}
