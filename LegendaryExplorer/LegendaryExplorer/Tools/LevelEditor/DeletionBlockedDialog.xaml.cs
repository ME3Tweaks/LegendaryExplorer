using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using LegendaryExplorer.Libraries;
using LegendaryExplorer.Misc;
using LegendaryExplorer.SharedUI.Bases;
using LegendaryExplorer.Tools.Sequence_Editor;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>
/// What blocks an actor's deletion, with a way of opening each export whose references do: a Kismet object in the Sequence Editor, anything else in the
/// Package Editor. It shows the references as they were: deleting the actor again checks them again
/// </summary>
public partial class DeletionBlockedDialog : TrackingNotifyPropertyChangedWindowBase
{
    public string Message { get; }
    public IReadOnlyList<BlockingReferrer> Referrers { get; }
    public IReadOnlyList<string> OtherBlockers { get; }

    public DeletionBlockedDialog(ActorDeletionBlockedException blocked) : base("Deletion Blocked Dialog", false)
    {
        Message = $"{blocked.ActorName} can't be deleted:";
        Referrers = blocked.Referrers;
        OtherBlockers = blocked.OtherBlockers;
        DataContext = this;
        InitializeComponent();
    }

    /// <summary>
    /// Shows it over <paramref name="owner"/>, which it blocks until it's closed, as a modal dialog does. Unlike one, it leaves the other windows usable,
    /// so that what it lists can be changed in them. If something else had already disabled the owner (another tool's modal dialog), that's left to enable it
    /// </summary>
    public void ShowOver(Window owner)
    {
        Owner = owner;
        var handle = new WindowInteropHelper(owner).Handle;
        bool disabledHere = !WindowsAPI.EnableWindow(handle, false);
        //(as it goes, before the owner is activated, which a disabled window can't be)
        if (disabledHere) Closing += (_, _) => WindowsAPI.EnableWindow(handle, true);
        bool shown = false;
        try
        {
            Show();
            shown = true;
        }
        finally
        {
            if (disabledHere && !shown) WindowsAPI.EnableWindow(handle, true);
        }
    }

    /// <summary>The referrer a button is for, unless it's gone, which is said</summary>
    private BlockingReferrer ReferrerOf(object button)
    {
        var referrer = (BlockingReferrer)((FrameworkElement)button).DataContext;
        if (!referrer.IsGone) return referrer;
        MessageBox.Show(this, $"{referrer.Name} has been deleted since, which took its references with it. Delete the actor again to see what still blocks it.", "Delete actor");
        return null;
    }

    private void OpenInSequenceEditor_Click(object sender, RoutedEventArgs e)
    {
        if (ReferrerOf(sender) is not { } referrer) return;
        //one that already has its package, if one does: working through the list shouldn't leave an editor for each of it
        if (referrer.Export.FileRef.Users.OfType<SequenceEditorWPF>().FirstOrDefault() is { } editor)
        {
            editor.RestoreAndBringToFront();
            OfferPackageEditorUnless(editor.GoToExport(referrer.Export), referrer);
            return;
        }
        new SequenceEditorWPF(referrer.Export, shown => OfferPackageEditorUnless(shown, referrer)).Show();
    }

    /// <summary>If the Sequence Editor couldn't show <paramref name="referrer"/>, says so and offers the Package Editor</summary>
    private void OfferPackageEditorUnless(bool shown, BlockingReferrer referrer)
    {
        if (shown) return;
        if (MessageBox.Show(this, $"{referrer.Name} isn't in any sequence the Sequence Editor shows. Open it in the Package Editor instead?", "Delete actor",
                MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            LevelEditor.OpenInPackageEditor(referrer.Export);
    }

    private void OpenInPackageEditor_Click(object sender, RoutedEventArgs e)
    {
        if (ReferrerOf(sender) is { } referrer) LevelEditor.OpenInPackageEditor(referrer.Export);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
