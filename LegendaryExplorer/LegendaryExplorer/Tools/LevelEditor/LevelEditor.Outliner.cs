using LegendaryExplorer.Tools.AssetDatabase;
using LegendaryExplorer.Lightmaps.Lightmass;
using LegendaryExplorer.Misc;
using LegendaryExplorer.Misc.AppSettings;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.SharedUI.Bases;
using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.SharpDX;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using Microsoft.Win32;
using LegendaryExplorer.Tools.PackageEditor;
using LegendaryExplorer.UserControls.ExportLoaderControls;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

namespace LegendaryExplorer.Tools.LevelEditor;

public partial class LevelEditor
{
    private string _actorFilterText = "";

    // Store expansion by group identity, not its recycled visual container.
    // A level subgroup has independent state within each category.
    private readonly Dictionary<(string Category, object Name), OutlinerGroupState> _outlinerGroupStates = [];

    internal OutlinerGroupState GetOutlinerGroupState(CollectionViewGroup group)
    {
        string category = null;
        if (GroupActorsByCategory && group.IsBottomLevel)
        {
            category = ActorsView.Groups.Cast<CollectionViewGroup>()
                .FirstOrDefault(parent => parent.Items.Contains(group))?.Name as string;
        }
        var key = (category, group.Name);
        if (!_outlinerGroupStates.TryGetValue(key, out var state))
        {
            // Categories start collapsed; levels, including those inside a category, start expanded.
            _outlinerGroupStates[key] = state = new OutlinerGroupState
            {
                Category = group.Name is string name ? RenderContext.GetCategoryState(name) : null,
                IsExpanded = group.Name is not string
            };
        }
        return state;
    }

    private void CollapseOutlinerGroups_Clicked(object sender, RoutedEventArgs e)
    {
        // Include cached groups hidden by the current search as well as unrealized groups.
        foreach (var state in _outlinerGroupStates.Values) state.IsExpanded = false;
        foreach (CollectionViewGroup group in ActorsView.Groups) CollapseGroup(group);

        void CollapseGroup(CollectionViewGroup group)
        {
            GetOutlinerGroupState(group).IsExpanded = false;
            if (!group.IsBottomLevel)
            {
                foreach (CollectionViewGroup child in group.Items) CollapseGroup(child);
            }
        }
    }

    private void RevealActorInOutliner(ActorProxy actor)
    {
        foreach (CollectionViewGroup group in ActorsView.Groups)
        {
            if (group.IsBottomLevel)
            {
                if (group.Name == actor.OwningFile) GetOutlinerGroupState(group).IsExpanded = true;
            }
            else if (Equals(group.Name, actor.Category))
            {
                GetOutlinerGroupState(group).IsExpanded = true;
                foreach (CollectionViewGroup level in group.Items)
                {
                    if (level.Name == actor.OwningFile) GetOutlinerGroupState(level).IsExpanded = true;
                }
            }
        }
        // A hidden outliner (another tab is in front) has no rows to scroll to: scroll when it's shown, if the actor is still selected then.
        if (!MeshExportsList.IsVisible)
        {
            _revealWhenOutlinerShown = actor;
            return;
        }
        _revealWhenOutlinerShown = null;
        MeshExportsList.UpdateLayout();
        // A group that just expanded has its rows placed by estimated heights, so the first scroll can land
        // up to a group's worth off. Its rows are measured by then, so scrolling again lands on the actor.
        for (int attempt = 0; attempt < 3 && !IsOutlinerRowInView(actor); attempt++)
        {
            MeshExportsList.ScrollIntoView(actor);
            MeshExportsList.UpdateLayout();
        }
    }

    //the actor revealed while the outliner was hidden
    private ActorProxy _revealWhenOutlinerShown;

    private void MeshExportsList_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (!MeshExportsList.IsVisible || _revealWhenOutlinerShown is null) return;
        // Once the tab's content is laid out, so the rows can be measured.
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            var actor = _revealWhenOutlinerShown;
            _revealWhenOutlinerShown = null;
            if (actor is not null && actor == SelectedActor && MeshExportsList.IsVisible) RevealActorInOutliner(actor);
        });
    }

    private bool IsOutlinerRowInView(ActorProxy actor)
    {
        if (MeshExportsList.ItemContainerGenerator.ContainerFromItem(actor) is not ListBoxItem { IsVisible: true } row) return false;
        // The list's scroll viewer fills it: no border, padding or horizontal scroll bar.
        Rect bounds = row.TransformToAncestor(MeshExportsList).TransformBounds(new Rect(row.RenderSize));
        return bounds.Top >= -0.5 && bounds.Bottom <= MeshExportsList.ActualHeight + 0.5;
    }

    private bool _groupActorsByCategory = true;
    public bool GroupActorsByCategory
    {
        get => _groupActorsByCategory;
        set
        {
            if (SetProperty(ref _groupActorsByCategory, value))
            {
                OnPropertyChanged(nameof(GroupActorsByLevel));
                UpdateOutlinerGrouping();
            }
        }
    }

    public bool GroupActorsByLevel
    {
        get => !GroupActorsByCategory;
        set { if (value) GroupActorsByCategory = false; }
    }

    private void UpdateOutlinerGrouping()
    {
        var selection = SelectedActor;
        using (ActorsView.DeferRefresh())
        {
            ActorsView.GroupDescriptions.Clear();
            ActorsView.SortDescriptions.Clear();
            if (GroupActorsByCategory)
            {
                ActorsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ActorProxy.Category)));
                ActorsView.SortDescriptions.Add(new SortDescription(nameof(ActorProxy.Category), ListSortDirection.Ascending));
            }
            // Keep package identity and package actions available in both modes.
            ActorsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ActorProxy.OwningFile)));
            ActorsView.SortDescriptions.Add(new SortDescription(nameof(ActorProxy.OwningFileName), ListSortDirection.Ascending));
            ActorsView.SortDescriptions.Add(new SortDescription(nameof(ActorProxy.DisplayText), ListSortDirection.Ascending));
            ActorsView.SortDescriptions.Add(new SortDescription("Export.UIndex", ListSortDirection.Ascending));
        }
        // Refresh can temporarily clear ListBox selection. Restore without moving the camera.
        SelectActor(selection, false);
        if (selection is not null) MeshExportsList?.ScrollIntoView(selection);
    }

    private bool ActorFilter(object obj)
    {
        if (string.IsNullOrEmpty(_actorFilterText)) return true;
        return obj is ActorProxy actor &&
               (actor.DisplayText.Contains(_actorFilterText, StringComparison.OrdinalIgnoreCase)
                || actor.ClassName.Contains(_actorFilterText, StringComparison.OrdinalIgnoreCase)
                || actor.CoverageLabel.Contains(_actorFilterText, StringComparison.OrdinalIgnoreCase)
                || actor.Category.Contains(_actorFilterText, StringComparison.OrdinalIgnoreCase)
                || (int.TryParse(_actorFilterText.TrimStart('#'), out int index) && actor.Export?.UIndex == index));
    }

    private void ActorFilter_TextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _actorFilterText = ((TextBox)sender).Text.Trim();
        ActorsView.Refresh();
    }

    private void SelectAndRevealActor(ActorProxy actor, bool focus = true)
    {
        if (!ActorFilter(actor)) ActorFilter_TextBox.Clear();
        SelectActor(actor, focus);
        RevealActorInOutliner(actor);
    }

    private void Goto_TextBox_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Return && !e.IsRepeat)
        {
            GotoButton_Clicked(null, null);
        }
    }

    private void GotoButton_Clicked(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(Goto_TextBox.Text, out int uIdx)
            && Actors.FirstOrDefault(a => a.Export?.UIndex == uIdx) is ActorProxy actor)
        {
            SelectAndRevealActor(actor);
        }
    }
}
