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
 * activity detail view model shows the details of an activity.
 * it contains the label, description and ...
 * TODO: list of the associated schedules.
 *
 * [glossary]
 * ! [...strutor]
 * ! [.axaml]
 * ! [code-behind]
 * ! [event handlers]
 * ! [class inheritance]
 * ! [records]
 * !
**/
public partial class ActivityViewModel : ContentViewModelBase
{
    /**
     * ! [...strutor]
    **/

    // constructor

    public ActivityViewModel(int activityId)
    {
        Console.WriteLine($"New Programme Detail View Model for Id: {activityId}");
        // Load values from the DB
        ActivityId = activityId;
        (bool status, string? details, var select) = DatabaseService.Instance.SelectActivity(activityId);
        if (! status)  throw new Exception ($"Failed to load from DB: {details}");
        if (select is null) throw new Exception ($"Select from DB is empty: {details}");

        ActivityProgrammeId = select.Value.Item2;
        ActivityLabel = select.Value.Item3;
        ActivityNotes = select.Value.Item4;

        setupdone = true;
    }



    /**
     * ! [.axaml]
     * ! show view properties
    **/

    [ObservableProperty]
    public partial string ActivityLabel {get; set;} = "";

    partial void OnActivityLabelChanged(string value)
    {
        if (! setupdone) return;

        DatabaseService.Instance.UpdateActivity(
            ActivityId,
            null,
            ActivityLabel,
            null
        );
    }

    [ObservableProperty]
    public partial string ActivityNotes {get; set;} = "";

    partial void OnActivityNotesChanged(string value)
    {
        if (! setupdone) return;

        DatabaseService.Instance.UpdateActivity(
            ActivityId,
            null,
            null,
            ActivityNotes
        );
    }



    /**
     * ! [code-behind]
     * ! the logic to the view
    **/

    //  prevents writing back to DB on detail changed during constructor
    private bool setupdone = false;

    private int ActivityId;
    private int ActivityProgrammeId;

    

    /**
     * ! [class inheritance]
     * ! properties of inherited class
    **/
    
    // title of the vm
    public override string PageTitle => "Activity Details";
    

    public override void OnLeaveEvent() {}

    public override void OnFocusEvent() {}
}
