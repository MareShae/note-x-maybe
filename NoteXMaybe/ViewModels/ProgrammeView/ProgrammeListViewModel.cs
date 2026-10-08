using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Interactivity;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NoteXMaybe.ViewModels;


/**
 * programme list view model contains a list of programmes in the DB.
 * [+] opens the `insert` overlay to add a new programme.
 * [-] deletes any programme when it is selected from the list.
 * [programme] item selected opens a new page that displays its details.
 *
 * [glossary]
 * ! [...strutor]
 * ! [.axaml]
 *      ! {programme insert overlay}
 * ! [code-behind]
 * ! [event handlers]
 * ! [class inheritance]
 * ! [records]
 * !
**/
public partial class ProgrammeListViewModel : ContentViewModelBase
{
    /**
     * ! [...strutor]
    **/

    // constructor

    public ProgrammeListViewModel()
    {
        // indicate focus on new
        focused = true;

        // Initialize the list from the database
        FreshProgrammeItems();

        // register notify on DB changed
        DatabaseService.Instance.NotifyProgrammeInsert += OnDatabaseNotifyProgrammeInserted;
        DatabaseService.Instance.NotifyProgrammeUpdate += OnDatabaseNotifyProgrammeUpdated;
        DatabaseService.Instance.NotifyProgrammeDelete += OnDatabaseNotifyProgrammeDeleted;
    }

    // destructor

    ~ProgrammeListViewModel()
    {
        // unregister notify on DB changed
        DatabaseService.Instance.NotifyProgrammeInsert -= OnDatabaseNotifyProgrammeInserted;
        DatabaseService.Instance.NotifyProgrammeUpdate -= OnDatabaseNotifyProgrammeUpdated;
        DatabaseService.Instance.NotifyProgrammeDelete -= OnDatabaseNotifyProgrammeDeleted;
    }



    /**
     * ! [.axaml]
     * ! show view properties
    **/
    
    [ObservableProperty]
    // functional tip
    public partial string Tip { get; set; } = """Use the [+] button to add a new Entry/Activity in the Checklist""";

    // items in the ListBox
    public ObservableCollection<ProgrammeItem> ProgrammeItems { get; } = new();

    [ObservableProperty]
    // creates a public property called SelectedProgrammeItem
    public partial ProgrammeItem? SelectedProgrammeItem {get; set;} = null;

    // [ObservableProperty] auto-registers a [OnSelectedProgrammeItemChanged]
    // Manual linking not required.
    partial void OnSelectedProgrammeItemChanged(ProgrammeItem? value)
    {
        if (value is null) return;
        // deselect so it can be clickable again
        Dispatcher.UIThread.Post(() =>
        {
            SelectedProgrammeItem = null;
        });

        // select programme item mode
        if (! DropProgrammeMode) {
            
            // request programme details vm
            RequestProgrammeDetailsVM?.Invoke(
                this,
                new MainViewSwitchEventArgs(value.Id)
            );
            Console.WriteLine("Switch to [Programme Details] View event");
        }
        // delete programme item mode
        else
        {
            // request to delete the item
            DropProgrammeItem(value);
        }
    }

    [RelayCommand]
    // intended for the [-]  button to toggle [delete programme item] mode
    public void ToggleCommandDropProgrammeMode()
    {
        DropProgrammeMode = !DropProgrammeMode;
        Console.WriteLine($"Drop Activity Mode: {DropProgrammeMode}");
    }

    /**
     * ! {programme insert overlay}
    **/ 

    /** Bindings **/

    [ObservableProperty]
    // status on programme insert operation in DB
    public partial string? InsertProgrammeStatusDetail { get; set; } = "";

    [ObservableProperty]
    // label for new programme
    public partial string LabelText { get; set; } = "";

    [ObservableProperty]
    // notes for new programme
    public partial string NotesText { get; set; } = "";

    [RelayCommand]
    // command on [save] button click
    async public Task CommandSaveNewProgramme()
    {
        Console.WriteLine($"Make new Programme with Label:{LabelText} and Notes:{NotesText}");

        // hide the status temporarily
        HideInsertProgrammeStatus?.Invoke(this, EventArgs.Empty);

        // attempt Insert and get status details
        // the DB sends a [delete] event
        (bool status, InsertProgrammeStatusDetail) = DatabaseService.Instance.InsertProgramme(LabelText, NotesText);

        // show status ONLY if it fails
        if (! status) {
            ShowInsertProgrammeStatus?.Invoke(this, EventArgs.Empty);
            return;
        }

        // send an event to View.cs to CloseProgrammeInsertOverlay
        CloseProgrammeInsertOverlay?.Invoke(this, EventArgs.Empty);

        // clear the parameters
        LabelText = "";
        NotesText = "";
    }



    /**
     * ! [code-behind]
     * ! the logic to the view
    **/

    // when activated, selecting an item in the list deletes it
    // TODO: confirmation before delete
    private bool DropProgrammeMode = false;

    // intended for the onFocus/onLeave function
    // when activated, indicates that vm is onfocus
    private bool focused;

    // intended for the onFocus/onLeave function
    // when activated, the vm waits for OnFocus to refresh its list
    private bool mark_for_refresh_on_focus = false;


    // refresh the list from the DB
    private void FreshProgrammeItems()
    {
        // TODO: DatabaseService can control both instances: DB and Var

        ProgrammeItems.Clear();

        // Pull the list from the database
        (bool status, string? details, List<(int, string, string)>? select) = DatabaseService.Instance.SelectProgrammes();
        // verify operation status
        if (! status) throw new Exception ($"Failed to load from DB: {details}");
        // verify return value
        if (select is null) throw new Exception ($"Select from DB is empty: {details}");

        foreach (var sel in select)
        {
            ProgrammeItems.Add(new ProgrammeItem(
                sel.Item1,  // Id
                sel.Item2,  // Label
                sel.Item3   // Notes
            ));
        }
    }

    private void DropProgrammeItem(ProgrammeItem? value)
    {
        if (value is null) return;

        Console.WriteLine($"Attempt to Delete {value}");

        // request programme delete
        // the DB sends a [delete] event
        (bool status, string? details) = DatabaseService.Instance.DeleteProgramme(value.Id);
        // verify operation status
        if (! status)
        {   
            Console.WriteLine ($"Failed to DELETE from DB: {details}");
        }
    }

    private void RefreshNowOrMarkForLater()
    {
        // refresh only when focused
        if (focused) {
            FreshProgrammeItems();
        }
        // wait for focus to refresh
        else
        {
            mark_for_refresh_on_focus = true;
        }
    }



    /**
     * ! [event handlers]
     * ! events that the vm invokes
     * ! events that the main view handles from other vm
    **/

    /** event class with custom parameter **/

    // includes ProgrammeId as a member in EventArgs
    public class MainViewSwitchEventArgs: EventArgs
    {   
        // the unique Id of the selected programme
        public int ProgrammeId;

        // constructor
        public MainViewSwitchEventArgs (int programmeId)
        {
            ProgrammeId = programmeId;
        }
    }

    /** event to request vm **/
    public event EventHandler<MainViewSwitchEventArgs>? RequestProgrammeDetailsVM;

    /** event for the `insert` overlay  **/

    public event EventHandler? CloseProgrammeInsertOverlay;

    /** events for status  **/

    // show the operation status
    public event EventHandler? ShowInsertProgrammeStatus;
    // hide the operation status
    public event EventHandler? HideInsertProgrammeStatus;


    /** database notifications **/


    private void OnDatabaseNotifyProgrammeInserted(object? sender, EventArgs e)
    {
        RefreshNowOrMarkForLater();
    }

    private void OnDatabaseNotifyProgrammeUpdated(object? sender, EventArgs e)
    {
        RefreshNowOrMarkForLater();
    }

    private void OnDatabaseNotifyProgrammeDeleted(object? sender, EventArgs e)
    {
        RefreshNowOrMarkForLater();
    }



    /**
     * ! [class inheritance]
     * ! properties of inherited class
    **/

    // title of the vm
    public override string PageTitle => "Programmes";

    public override void OnLeaveEvent()
    {
        // mark as unfocused
        focused = false;
    }

    public override void OnFocusEvent()
    {
        // mark as focused
        focused = true;

        // refresh if flagged
        if (mark_for_refresh_on_focus)
        {
            FreshProgrammeItems();
            // unflag refresh
            mark_for_refresh_on_focus = false;
        }
    }


    /**
     * ! [records]
     * ! where they are meant to be
    **/
    public record ProgrammeItem (int Id, string Label, string Notes);
}
