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
 * programme detail view model shows the details of a programme.
 * it contains the label, description and list of the associated activities.
 * [+] opens the `insert` overlay to add a new activity.
 * [-] deletes any activity when it is selected from the list.
 * [activity] item selected opens a new page that displays its details.
 *
 * [glossary]
 * ! [...strutor]
 * ! [.axaml]
 *      ! {activity insert overlay}
 * ! [code-behind]
 * ! [event handlers]
 * ! [class inheritance]
 * ! [records]
 * !
**/
public partial class ProgrammeDetailViewModel : ContentViewModelBase
{   
    /**
     * ! [...strutor]
    **/

    // constructor

    public ProgrammeDetailViewModel(int programmeId)
    {   
        focused = true;

        Console.WriteLine($"New Programme Detail View Model for Id: {programmeId}");
        // Load values from the DB
        ProgrammeId = programmeId;
        (bool status, string? details, var select) = DatabaseService.Instance.SelectProgramme(programmeId);
        if (! status)  throw new Exception ($"Failed to load from DB: {details}");
        if (select is null) throw new Exception ($"Select from DB is empty: {details}");

        ProgrammeLabel = select.Value.Item2;
        ProgrammeNotes = select.Value.Item3;

        Console.WriteLine($"New Programme Detail View Model for Id: {programmeId} Label: {ProgrammeLabel} Notes: {ProgrammeNotes}");

        // Initialize the list from the database
        FreshActivityItems(programmeId);

        // Listen to DB for when something changed
        DatabaseService.Instance.NotifyActivityInsert += OnDatabaseNotifyActivityInserted;
        DatabaseService.Instance.NotifyActivityUpdate += OnDatabaseNotifyActivityUpdated;
        DatabaseService.Instance.NotifyActivityDelete += OnDatabaseNotifyActivityDeleted;

        setupdone = true;
    }

    // destructor

    ~ProgrammeDetailViewModel()
    {
        // unListen to DB for when something changed
        DatabaseService.Instance.NotifyActivityInsert -= OnDatabaseNotifyActivityInserted;
        DatabaseService.Instance.NotifyActivityUpdate -= OnDatabaseNotifyActivityUpdated;
        DatabaseService.Instance.NotifyActivityDelete -= OnDatabaseNotifyActivityDeleted;
    }



    /**
     * ! [.axaml]
     * ! show view properties
    **/

    [ObservableProperty]
    // label for the programme
    public partial string ProgrammeLabel {get; set;}
    // label changed event

    partial void OnProgrammeLabelChanged(string value)
    {
        if (! setupdone) return;

        // request update in the DB
        // the DB sends an [update] event
        DatabaseService.Instance.UpdateProgramme(
            ProgrammeId,
            ProgrammeLabel,
            null
        );
    }

    [ObservableProperty]
    // notes for the programme
    public partial string ProgrammeNotes {get; set;}
    // notes changed event

    partial void OnProgrammeNotesChanged(string value)
    {
        if (! setupdone) return;

        // request update in the DB
        // the DB sends an [update] event
        DatabaseService.Instance.UpdateProgramme(
            ProgrammeId,
            null,
            ProgrammeNotes
        );
    }

    // items in the ListBox
    public ObservableCollection<ActivityItem> ActivityItems { get; }= new();

    [ObservableProperty]
    // selected item from the ActivityItems collection
    public partial ActivityItem? SelectedActivityItem {get; set;} = null;

    // [ObservableProperty] auto-registers a [OnSelectedActivityItemChanged]
    // Manual linking not required.
    partial void OnSelectedActivityItemChanged(ActivityItem? value)
    {
        if (value is null) return;
        // deselect so it can be clickable again
        Dispatcher.UIThread.Post(()=>
        {
            SelectedActivityItem = null;
        });

        // select activity item mode
        if (! DropActivityMode) {

            // request activity details vm
            RequestActivityDetailsVM?.Invoke(
                this,
                new MainViewSwitchEventArgs(value.Id)
            );
            Console.WriteLine("Switch to [Activity Details] View event");
        } 
        // delete programme item mode
        else
        {   
            // request to delete the item
            DropActivityItem(value);
        }
    }

    [RelayCommand]
    // intended for the [-]  button to toggle [delete programme item] mode
    async public Task ToggleCommandDropActivityMode()
    {
        DropActivityMode = !DropActivityMode;
        Console.WriteLine($"Drop Activity Mode: {DropActivityMode}");
    }


    /**
     * ! {activity insert overlay}
    **/ 


    [ObservableProperty]
    // status on activity insert operation in DB
    public partial string? InsertActivityStatusDetail { get; set; } = "";

    [ObservableProperty]
    // label for new activity
    public partial string ActivityLabelText { get; set; } = "";

    [ObservableProperty]
    // notes for new activity
    public partial string ActivityNotesText { get; set; } = "";

    [RelayCommand]
    // command on [save] button click
    async public Task CommandSaveNewActivity()
    {
        Console.WriteLine($"Make new Activity with Label:{ActivityLabelText} and Notes:{ActivityNotesText}");

        // hide the status temporarily
        HideInsertActivityStatus?.Invoke(this, EventArgs.Empty);

        // attempt Insert and get status details
        // the DB sends an [insert] event
        (bool status, InsertActivityStatusDetail) = DatabaseService.Instance.InsertActivity(ProgrammeId, ActivityLabelText, ActivityNotesText);

        // show status ONLY if it fails
        if (! status) {
            ShowInsertActivityStatus?.Invoke(this, EventArgs.Empty);
            return;
        }

        // send an event to View.cs to CloseActivityInsertOverlay
        CloseActivityInsertOverlay?.Invoke(this, EventArgs.Empty);
    }



    /**
     * ! [code-behind]
     * ! the logic to the view
    **/

    //  prevents writing back to DB on detail changed during constructor
    private bool setupdone = false;

    private int ProgrammeId;
    
    // when activated, selecting an item in the list deletes it
    // TODO: confirmation before delete
    private bool DropActivityMode = false;

    // intended for the onFocus/onLeave function
    // when activated, indicates that vm is onfocus
    private bool focused;

    // intended for the onFocus/onLeave function
    // when activated, the vm waits for OnFocus to refresh its list
    private bool mark_for_refresh_on_focus = false;

    // refresh the list from the DB
    private void FreshActivityItems(int ProgrammeId)
    {
        // TODO: DatabaseService can control both instances: DB and Var

        ActivityItems.Clear();

        // Pull the list from the database
        (bool status, string? details, List<(int, int, string, string)>? select) = DatabaseService.Instance.SelectActivities(ProgrammeId);
        // verify operation status
        if (! status) throw new Exception ($"Failed to load from DB: {details}");
        // verify return value
        if (select is null) throw new Exception ($"Select from DB is empty: {details}");

        foreach (var sel in select)
        {
            ActivityItems.Add(new ActivityItem(
                sel.Item1,  // Id
                sel.Item2,  // PId
                sel.Item3,  // Label
                sel.Item4   // Notes
            ));
        }
    }

    private void DropActivityItem(ActivityItem? value)
    {
        if (value is null) return;

        Console.WriteLine($"Attempt to Delete {value}");

        // request activity delete
        // the DB sends a [delete] event
        (bool status, string? details) = DatabaseService.Instance.DeleteActivity(value.Id);
        // verify operation status
        if (! status)
        {   
            Console.WriteLine ($"Failed to DELETE from DB: {details}");
        }
        
        FreshActivityItems(value.ProgrammeId);
    }

    private void RefreshNowOrMarkForLater()
    {
        // refresh only when focused
        if (focused) {
            FreshActivityItems(ProgrammeId);
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

    // includes ActivityId as a member in EventArgs
    public class MainViewSwitchEventArgs: EventArgs
    {   
        // the unique Id of the selected activity
        public int ActivityId;

        // constructor
        public MainViewSwitchEventArgs (int activityId)
        {
            ActivityId = activityId;
        }
    }

    /** event to request vm **/

    public event EventHandler<MainViewSwitchEventArgs>? RequestActivityDetailsVM;

    /** event for the `insert` overlay  **/

    public event EventHandler? CloseActivityInsertOverlay;

    /** events for status  **/

    // show the operation status
    public event EventHandler? ShowInsertActivityStatus;
    // hide the operation status
    public event EventHandler? HideInsertActivityStatus;

    /** database notifications **/

    private void OnDatabaseNotifyActivityInserted(object? sender, EventArgs e)
    {
        RefreshNowOrMarkForLater();
    }

    private void OnDatabaseNotifyActivityUpdated(object? sender, EventArgs e)
    {
        RefreshNowOrMarkForLater();
    }

    private void OnDatabaseNotifyActivityDeleted(object? sender, EventArgs e)
    {
        RefreshNowOrMarkForLater();
    }

    

    /**
     * ! [class inheritance]
     * ! properties of inherited class
    **/

    // title of the vm
    public override string PageTitle => "Programme Details";

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
            FreshActivityItems(ProgrammeId);
            // unflag refresh
            mark_for_refresh_on_focus = false;
        }
    }


    /**
     * ! [records]
     * ! where they are meant to be
    **/
    
    public record ActivityItem (int Id, int ProgrammeId, string Label, string Notes);
}
