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
        public PlotUsageScanner(ConcurrentAssetDB db) : base(db)
        {

        }

        private readonly HashSet<string> classesWithPlotData = new()
        {
            "BioSeqAct_PMExecuteTransition", "BioSeqAct_PMExecuteConsequence", "BioSeqAct_PMCheckState",
            "BioSeqAct_PMCheckConditional", "BioSeqVar_StoryManagerInt",
            "BioSeqVar_StoryManagerFloat", "BioSeqVar_StoryManagerBool", "BioSeqVar_StoryManagerStateId",
            "SFXSceneShopNodePlotCheck", "BioWorldInfo", "BioStateEventMap", "BioCodexMap", "BioQuestMap"
        };

        public override void ScanExport(ExportEntry export, int FileKey, bool IsMod)
        {
            if (!classesWithPlotData.Contains(export.ClassName)) return;

            switch (export.ClassName)
            {
                case "BioSeqAct_PMExecuteTransition":
                case "BioSeqAct_PMExecuteConsequence":
                {
                    var transition = export.GetProperty<IntProperty>("m_nIndex")?.Value;
                    if(transition.HasValue) AddToTransitionRecord(transition.Value, new PlotUsage(FileKey, export.UIndex, IsMod, PlotUsageContext.Sequence));
                    break;
                }
                case "BioSeqAct_PMCheckState":
                case "BioSeqVar_StoryManagerBool":
                case "BioSeqVar_StoryManagerStateId":
                {
                    var boolId = export.GetProperty<IntProperty>("m_nIndex")?.Value;
                    if(boolId.HasValue) AddToBoolRecord(boolId.Value, new PlotUsage(FileKey, export.UIndex, IsMod, PlotUsageContext.Sequence));
                    break;
                }
                case "BioSeqVar_StoryManagerFloat":
                {
                    var floatId = export.GetProperty<IntProperty>("m_nIndex")?.Value;
                    if(floatId.HasValue) AddToFloatRecord(floatId.Value, new PlotUsage(FileKey, export.UIndex, IsMod, PlotUsageContext.Sequence));
                    break;
                }
                case "BioSeqVar_StoryManagerInt":
                {
                    var intId = export.GetProperty<IntProperty>("m_nIndex")?.Value;
                    if(intId.HasValue) AddToIntRecord(intId.Value, new PlotUsage(FileKey, export.UIndex, IsMod, PlotUsageContext.Sequence));
                    break;
                }
                case "BioSeqAct_PMCheckConditional":
                {
                    var condId = export.GetProperty<IntProperty>("m_nIndex")?.Value;
                    if(condId.HasValue) AddToConditionalRecord(condId.Value, new PlotUsage(FileKey, export.UIndex, IsMod, PlotUsageContext.Sequence));
                    break;
                }
                case "BioWorldInfo":
                {
                    var bioWorldCondId = export.GetProperty<IntProperty>("Conditional")?.Value;
                    if(bioWorldCondId.HasValue) AddToConditionalRecord(bioWorldCondId.Value, new PlotUsage(FileKey, export.UIndex, IsMod));
                    break;
                }
                case "SFXSceneShopNodePlotCheck":
                    var mnId = export.GetProperty<IntProperty>("m_nIndex")?.Value;
                    if (mnId.HasValue && Enum.TryParse(export.GetProperty<EnumProperty>("VarType")?.Value.Name,
                        out ESFXSSPlotVarType type))
                    {
                        switch (type)
                        {
                            case ESFXSSPlotVarType.PlotVar_Float:
                                AddToFloatRecord(mnId.Value, new PlotUsage(FileKey, export.UIndex, IsMod));
                                break;
                            case ESFXSSPlotVarType.PlotVar_Int:
                                AddToIntRecord(mnId.Value, new PlotUsage(FileKey, export.UIndex, IsMod));
                                break;
                            case ESFXSSPlotVarType.PlotVar_State:
                                AddToBoolRecord(mnId.Value, new PlotUsage(FileKey, export.UIndex, IsMod));
                                break;
                        }
                    }
                    break;
                case "BioStateEventMap":
                {
                    BioStateEventMap map = ObjectBinary.From<BioStateEventMap>(export);
                    foreach (var evt in map.StateEvents)
                    {
                        AddBaseUsageToTransition(evt.ID, new PlotUsage(FileKey, export.UIndex, IsMod, PlotUsageContext.Transition));
                        foreach (var el in evt.Elements)
                        {
                            switch (el)
                            {
                                case BioStateEventMap.BioStateEventElementBool b:
                                    AddToBoolRecord(b.GlobalBool, new PlotUsage(FileKey, export.UIndex, IsMod, PlotUsageContext.Transition));
                                    break;
                                case BioStateEventMap.BioStateEventElementConsequence c:
                                    AddToTransitionRecord(c.Consequence, new PlotUsage(FileKey, export.UIndex, IsMod, PlotUsageContext.Transition));
                                    break;
                                case BioStateEventMap.BioStateEventElementFloat f:
                                    AddToFloatRecord(f.GlobalFloat, new PlotUsage(FileKey, export.UIndex, IsMod, PlotUsageContext.Transition));
                                    break;
                                case BioStateEventMap.BioStateEventElementInt i:
                                    AddToIntRecord(i.GlobalInt, new PlotUsage(FileKey, export.UIndex, IsMod, PlotUsageContext.Transition));
                                    break;
                                case BioStateEventMap.BioStateEventElementSubstate s:
                                    AddToBoolRecord(s.GlobalBool, new PlotUsage(FileKey, export.UIndex, IsMod, PlotUsageContext.Transition));
                                    if(s.ParentIndex >= 0) AddToBoolRecord(s.ParentIndex, new PlotUsage(FileKey, export.UIndex, IsMod, PlotUsageContext.Transition));
                                    foreach (var sib in s.SiblingIndices)
                                    {
                                        AddToBoolRecord(sib, new PlotUsage(FileKey, export.UIndex, IsMod, PlotUsageContext.Transition));
                                    }
                                    break;
                            }
                        }
                    }
                }

                    break;
                case "BioCodexMap":
                {
                    BioCodexMap codexMap = ObjectBinary.From<BioCodexMap>(export);
                    foreach (var page in codexMap.Pages)
                    {
                        AddToBoolRecord(page.ID, new PlotUsage(FileKey, export.UIndex, IsMod, PlotUsageContext.Codex));
                    }
                    break;
                }
                case "BioQuestMap":
                {
                    BioQuestMap questMap = ObjectBinary.From<BioQuestMap>(export);
                    // Parse goals
                    foreach (var goal in questMap.Quests.SelectMany(q => q.Goals))
                    {
                        if(goal.Conditional >= 0) AddToConditionalRecord(goal.Conditional, new PlotUsage(FileKey, export.UIndex, IsMod, PlotUsageContext.Quest));
                        if(goal.State >= 0) AddToBoolRecord(goal.State, new PlotUsage(FileKey, export.UIndex, IsMod, PlotUsageContext.Quest));
                    }

                    // Parse plot items
                    foreach (var plotItem in questMap.Quests.SelectMany(q => q.PlotItems))
                    {
                        if(plotItem.Conditional >= 0) AddToConditionalRecord(plotItem.Conditional, new PlotUsage(FileKey, export.UIndex, IsMod, PlotUsageContext.Quest));
                        if(plotItem.State >= 0) AddToBoolRecord(plotItem.State, new PlotUsage(FileKey, export.UIndex, IsMod, PlotUsageContext.Quest));
                    }
                    
                    // Parse Task Evals
                    foreach (var task in questMap.TaskEvals.Concat(questMap.FloatTaskEvals).Concat(questMap.IntTaskEvals).SelectMany(t => t.TaskEvals))
                    {
                        if(task.Conditional >= 0) AddToConditionalRecord(task.Conditional, new PlotUsage(FileKey, export.UIndex, IsMod, PlotUsageContext.Quest));
                        if(task.State >= 0) AddToBoolRecord(task.State, new PlotUsage(FileKey, export.UIndex, IsMod, PlotUsageContext.Quest));
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
