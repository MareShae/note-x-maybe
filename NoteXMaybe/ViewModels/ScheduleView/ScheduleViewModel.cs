using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NoteXMaybe.ViewModels;


/**
 * schedule view model ...
 *
 * [glossary]
 * ! [...strutor]
 * ! [.axaml]
 *      ! {schedule insert overlay}
 * ! [code-behind]
 * ! [event handlers]
 * ! [class inheritance]
 * ! [records]
 * !
**/
public partial class ScheduleViewModel : ContentViewModelBase
{
    static string STROKE_UNCONFIRMED = "LightGray";
    static string STROKE_CONFIRMED = "Blue";
    static string TEXT_COLOUR_UNCONFIRMED = "LightGray";
    static string TEXT_COLOUR_CONFIRMED = "Black";


    
    /**
     * ! [...strutor]
    **/

    /** constructor **/

    public ScheduleViewModel()
    {
        focused = true;

        TimeScaleConvertionToSeconds.Add("second", 1);
        TimeScaleConvertionToSeconds.Add("minute", 60);
        TimeScaleConvertionToSeconds.Add("hour", 60 * 60);
        TimeScaleConvertionToSeconds.Add("day", 60 * 60 * 24);

        foreach (string timescale in TimeScaleConvertionToSeconds.Keys)
        {
            InsertPeriodTimeScale.Add(timescale);
        }

        SelectedPeriodTimeScale = InsertPeriodTimeScale[0];


        // Initialize the list from the database
        FreshActivityItems();

        FreshBackground();
        FreshScheduleItems();

        // Listen to DB for when something changed
        DatabaseService.Instance.NotifyScheduleInsert += OnDatabaseNotifyScheduleInserted;
        DatabaseService.Instance.NotifyScheduleUpdate += OnDatabaseNotifyScheduleUpdated;
        DatabaseService.Instance.NotifyScheduleDelete += OnDatabaseNotifyScheduleDeleted;

        DatabaseService.Instance.NotifyActivityInsert += OnDatabaseNotifyActivityInserted;
        DatabaseService.Instance.NotifyActivityUpdate += OnDatabaseNotifyActivityUpdated;
        DatabaseService.Instance.NotifyActivityDelete += OnDatabaseNotifyActivityDeleted;

        // Start OnTickTimer
        _ = OnTickTimer(cts.Token);
    }

    ~ScheduleViewModel()
    {
        // Start OnTickTimer
        cts.Cancel();
        cts.Dispose();
    }



    /**
     * ! [.axaml]
     * ! show view properties
    **/

    // position of the lines that represent hours of the day
    public ObservableCollection<Liner> Liners {get;} = new();
    // schedule items that are rendered on the calendar
    public ObservableCollection<ScheduleItem> ScheduleItems {get;} = new();

    [ObservableProperty]
    public partial string CurretTimeLabel {get; set;} = "00:00";

    [ObservableProperty]
    public partial int CurretTimeOverlineY {get; set;} = 0;

    [RelayCommand]
    // intended for the [-]  button to toggle [delete schedule item] mode
    public void ToggleCommandDropScheduleMode()
    {
        DropScheduleMode = !DropScheduleMode;
        Console.WriteLine($"Drop Activity Mode: {DropScheduleMode}");
    }

    [RelayCommand]
    public void OnScheduleItemClicked(int? sId)
    {
        if (sId is null) return;

        // TODO: display schedule item detail
        if (! DropScheduleMode) {}
        // delete programme item mode
        else
        {
            // request to delete the item
            DropScheduleItem((int)sId);
        }
        Console.WriteLine($"{sId} clicked");
    }

    [RelayCommand]
    public void OnScheduleItemConfirmClicked(int? sId)
    {
        if (sId is null) return;

        // find the associated sId
        var item = ScheduleItems.FirstOrDefault(p => p.Id == sId);

        if (item is null) throw new Exception("schedule item not found");

        // find the associated index
        var idx = ScheduleItems.IndexOf(item!);

        // modify sequentially
        item = item with {Confirmation = ! item.Confirmation};
        item = item with {Stroke = item.Confirmation ? STROKE_CONFIRMED : STROKE_UNCONFIRMED};
        item = item with {TextColour = item.Confirmation ? TEXT_COLOUR_CONFIRMED : TEXT_COLOUR_UNCONFIRMED};
        ScheduleItems[idx] = item;

        // grouping using the start and stop times
        if (item.Confirmation)
        {
            // remove from unconfirmed
            GroupedScheduleItems_Start_Unconfirmed[item.StartTime].Remove(item.Id);
            GroupedScheduleItems_Stop_Unconfirmed[item.StopTime].Remove(item.Id);

            // add to confirmed
            GroupedScheduleItems_Start_Confirmed[item.StartTime].Add(item.Id);
            GroupedScheduleItems_Stop_Confirmed[item.StopTime].Add(item.Id);

            // pruning
            _ = GroupedScheduleItemsModified(GroupedScheduleItems_Start_Unconfirmed, item.StartTime);
            _ = GroupedScheduleItemsModified(GroupedScheduleItems_Stop_Unconfirmed, item.StopTime);
        }
        else
        {
            // remove from confirmed
            GroupedScheduleItems_Start_Confirmed[item.StartTime].Remove(item.Id);
            GroupedScheduleItems_Stop_Confirmed[item.StopTime].Remove(item.Id);

            // add to unconfirmed
            GroupedScheduleItems_Start_Unconfirmed[item.StartTime].Add(item.Id);
            GroupedScheduleItems_Stop_Unconfirmed[item.StopTime].Add(item.Id);

            // pruning
            _ = GroupedScheduleItemsModified(GroupedScheduleItems_Start_Confirmed, item.StartTime);
            _ = GroupedScheduleItemsModified(GroupedScheduleItems_Stop_Confirmed, item.StopTime);
        }
    }


    /**
     * ! {schedule insert overlay}
    **/ 

    [ObservableProperty]
    // status on schedule insert operation in DB
    public partial string? ScheduleInsertStatusDetail {get; set;} = "";

    // items in the Activity ComboBox
    public ObservableCollection<ActivityItem> InsertActivityItems  {get;}= new();

    [ObservableProperty]
    // selected item from the InsertActivityItems collection
    public partial ActivityItem? SelectedActivityItem {get; set;} = null;

    // [ObservableProperty] auto-registers a [OnSelectedActivityItemChanged]
    // Manual linking not required.
    partial void OnSelectedActivityItemChanged(ActivityItem? value)
    {
        // nothing happens, except the ComboBox selecting it
    }

    /** start time, HH:MM:SS **/ 

    /**
     * checks that values are valid with the followinig rules:
     *      + DIGIT only
     *      + 2 DIGITS MAX
     *      + 0 <= VALUE <= 24
    **/ 
    
    // (hr) the time the scheduled item starts
    public string InsertStartTimeHr {
        get => field;
        set 
        {  
            string processed = value;
            // ensure digit or fallback to old value
            if (! value.All(char.IsDigit))
            {
                value = field;
            }
            // pad with leading 0s
            while (value.Length < 2)
            {
                value = $"0{value}";
            }
            // clip with updated numbers
            if (value.Length > 2)
            {
                value = value.Substring(value.Length - 2);
            }
            // limit to clock hr range
            if (int.Parse(value) < 0 || int.Parse(processed) > 23) {
                value = field;
            }
            
            // clear out to force UI update
            SetProperty (ref field, null!);
            // update with real value
            SetProperty (ref field, value);
        }
    } = "00";


    /**
     * checks that values are valid with the followinig rules:
     *      + DIGIT only
     *      + 2 DIGITS MAX
     *      + 0 <= VALUE < 59
    **/
    
    // (mn) the time the scheduled item starts
    public string InsertStartTimeMn  {
        get => field;
        set 
        {  
            string processed = value;
            // ensure digit or fallback to old value
            if (! value.All(char.IsDigit))
            {
                value = field;
            }
            // pad with leading 0s
            while (value.Length < 2)
            {
                value = $"0{value}";
            }
            // clip with updated numbers
            if (value.Length > 2)
            {
                value = value.Substring(value.Length - 2);
            }
            // limit to clock hr range
            if (int.Parse(value) < 0 || int.Parse(processed) > 59) {
                value = field;
            }
            
            // clear out to force UI update
            SetProperty (ref field, null!);
            // update with real value
            SetProperty (ref field, value);
        }
    } = "00";

    /** period **/ 

    [ObservableProperty]
    // how long the scheduled item runs in seconds
    public partial string InsertPeriodText {get; set;} = "01";

    // the time units for the period
    public ObservableCollection<string> InsertPeriodTimeScale {get;} = new();

    [ObservableProperty]
    // the selected time unit for period
    public partial string SelectedPeriodTimeScale {get; set;}

    // [ObservableProperty] auto-registers a [OnSelectedPeriodTimeScaleChanged]
    // Manual linking not required.
    partial void OnSelectedPeriodTimeScaleChanged(string value)
    {
        // nothing happens, except the ComboBox selecting it
    }

    /** repetition **/ 

    /**
     * checks that values are valid with the followinig rules:
     *      + DIGIT only
     *      + 4 DIGITS MAX
    **/ 
    
    // the day that the scheduled item starts, YYYY
    public string InsertStartYear {
        get => field;
        set 
        {  
            string processed = value;
            // ensure digit or fallback to old value
            if (! value.All(char.IsDigit))
            {
                value = field;
            }
            // pad with leading 0s
            while (value.Length < 4)
            {
                value = $"0{value}";
            }
            // clip with updated numbers
            if (value.Length > 4)
            {
                value = value.Substring(value.Length - 4);
            }
            
            // clear out to force UI update
            SetProperty (ref field, null!);
            // update with real value
            SetProperty (ref field, value);
        }
    } = "0000";

    /**
     * checks that values are valid with the followinig rules:
     *      + DIGIT only
     *      + 2 DIGITS MAX
    **/ 
    
    // the day that the scheduled item starts, MM
    public string InsertStartMonth {
        get => field;
        set 
        {  
            string processed = value;
            // ensure digit or fallback to old value
            if (! value.All(char.IsDigit))
            {
                value = field;
            }
            // pad with leading 0s
            while (value.Length < 2)
            {
                value = $"0{value}";
            }
            // clip with updated numbers
            if (value.Length > 2)
            {
                value = value.Substring(value.Length - 2);
            }
            
            // clear out to force UI update
            SetProperty (ref field, null!);
            // update with real value
            SetProperty (ref field, value);
        }
    } = "00";

    /**
     * checks that values are valid with the followinig rules:
     *      + DIGIT only
     *      + 2 DIGITS MAX
    **/ 
    
    // the day that the scheduled item starts, DD
    public string InsertStartDay {
        get => field;
        set 
        {  
            string processed = value;
            // ensure digit or fallback to old value
            if (! value.All(char.IsDigit))
            {
                value = field;
            }
            // pad with leading 0s
            while (value.Length < 2)
            {
                value = $"0{value}";
            }
            // clip with updated numbers
            if (value.Length > 2)
            {
                value = value.Substring(value.Length - 2);
            }
            
            // clear out to force UI update
            SetProperty (ref field, null!);
            // update with real value
            SetProperty (ref field, value);
        }
    } = "00";

    [ObservableProperty]
    // number of repetitions per week; wraps to next week
    // default is 0, which means that it is only scheduled for the selected day in that week
    public partial string InsertDayRepetition {get; set;} = "0";

    [ObservableProperty]
    // number of repetitions per month
    // default is 1, which means selected days reoccur weekly
    public partial string InsertWeekRepetition {get; set;} = "1";

    [ObservableProperty]
    // number of repetitions per year
    // default is 1, which means selected days reoccur monthly
    public partial string InsertMonthRepetition {get; set;} = "1";

    [ObservableProperty]
    // number of repetitions
    // default is 1, which means selected days occur yearly
    public partial string InsertYearRepetition {get; set;} = "1";

    [RelayCommand]
    // command on [save] button click
    async public Task CommandScheduleInsert()
    {
        // TODO: verify scheduavailability

        Console.WriteLine($"Make new Schedule");

        if (SelectedActivityItem is null) return;

        // hide the status temporarily
        HideScheduleInsertStatus?.Invoke(this, EventArgs.Empty);

        // attempt Insert and get status details
        // the DB sends an [insert] event
        (bool status, ScheduleInsertStatusDetail) = DatabaseService.Instance.InsertSchedule(
            SelectedActivityItem.Id,
            $"{InsertStartTimeHr}:{InsertStartTimeMn}:00",
            int.Parse(InsertPeriodText) * TimeScaleConvertionToSeconds[SelectedPeriodTimeScale],
            $"{InsertStartYear}-{InsertStartMonth}-{InsertStartDay}",
            int.Parse(InsertDayRepetition),
            int.Parse(InsertWeekRepetition),
            int.Parse(InsertMonthRepetition),
            int.Parse(InsertYearRepetition)
            );

        // show status ONLY if it fails
        if (! status) {
            ShowScheduleInsertStatus?.Invoke(this, EventArgs.Empty);
            return;
        }

        // send an event to View.cs to CloseScheduleInsertOverlay
        CloseScheduleInsertOverlay?.Invoke(this, EventArgs.Empty);
    }


    /**
     * ! [code-behind]
     * ! the logic to the view
    **/

    // factors for time unit conversion to seconds
    Dictionary<string, int> TimeScaleConvertionToSeconds = new();
    
    // when activated, selecting an item in the view deletes it
    // TODO: confirmation before delete
    private bool DropScheduleMode = false;

    // intended for the onFocus/onLeave function
    // when activated, indicates that vm is onfocus
    private bool focused;

    // intended for the onFocus/onLeave function
    // when activated, the vm waits for OnFocus to refresh its list
    private bool mark_schedule_for_refresh_on_focus = false;
    private bool mark_activity_for_refresh_on_focus = false;

    // space with hours on the calendar to show second(s) details
    static private int LINE_SPACE_HR = 60 * 60;
    static private int LINE_SPACE_MN = 60;
    static private int LINE_SPACE_SC = 1;
    static private int LINE_SPACE_FACTOR = 30;
    // size of the schedule item
    static private Size SCHEDULE_ITEM_DEFAULT_SIZE = new Size(200, 200);

    // this works along-side the ScheduleItems
    // it reduces the search space when checking for current items
    private Dictionary<string, List<int>> GroupedScheduleItems_Start_Unconfirmed = new();
    private Dictionary<string, List<int>> GroupedScheduleItems_Start_Confirmed = new();
    private Dictionary<string, List<int>> GroupedScheduleItems_Stop_Unconfirmed = new();
    private Dictionary<string, List<int>> GroupedScheduleItems_Stop_Confirmed = new();

    private async Task GroupedScheduleItemsModified(Dictionary<string, List<int>> gsi, string key)
    {
        if (gsi.ContainsKey(key) && gsi[key].Count == 0)
        {
            gsi.Remove(key);
        }
    }

    // the background of the schedule view
    private void FreshBackground()
    {
        /**
         * renders a label and a line for every hour
         * from 00:00 to 24:00
         *
        **/ 

        Console.WriteLine("Rendering Background");

        Liners.Clear();


        for(int hr = 0; hr <= 24; hr++)
        {   
            /**
             * `hr * LINE_SPACE` starts the liner at 0
             * `(hr < 10) ...` adds a 0 for single digits; result is double digit hours
            **/
            Liners.Add(new Liner(hr * LINE_SPACE_HR / LINE_SPACE_FACTOR, $"{((hr < 10)? "0":"")}{hr}:00:00"));
        }
    }

    private void FreshScheduleItems()
    {
        // TODO: DatabaseService can control both instances: DB and Var
        ScheduleItems.Clear();

        // copy ONLY the confirmed schedule items START, so user does not have to reconfirm
        // STOP is derived from START
        var cfrm_start = new Dictionary<string, List<int>>(GroupedScheduleItems_Start_Confirmed);
        GroupedScheduleItems_Start_Unconfirmed.Clear();
        GroupedScheduleItems_Start_Confirmed.Clear();
        GroupedScheduleItems_Stop_Unconfirmed.Clear();
        GroupedScheduleItems_Stop_Confirmed.Clear();

        // Pull the list from the database
        (bool status, string? details, var select) = DatabaseService.Instance.SelectSchedules(); 
        // verify operation status
        if (! status) throw new Exception ($"Failed to load from DB: {details}");
        // verify return value
        if (select is null) throw new Exception ($"Select from DB is empty: {details}");

        // use the hour and minute in Time to determine its positioning
        foreach (var sel in select) {
            string[] time = sel.Item3.Split(':');
            int hr = int.Parse(time[0]);
            int mn = int.Parse(time[1]);
            int sc = int.Parse(time[2]);

            // find the corresponding Activity
            var aitem = InsertActivityItems.FirstOrDefault(p => p.Id == sel.Item2);

            if (aitem is null) continue;

            
            // create the item
            
            string EndTime = "";
            {
                // convert to seconds
                string[] split = sel.Item3.Split(":");
                int end = int.Parse(split[0]) * 60 * 60 + int.Parse(split[1]) * 60 + int.Parse(split[2]);

                // include the duration
                end += sel.Item4;

                // max out at the end of the day
                end = Math.Min(end, (23 * 60 * 60) + (59 * 60) + 59);

                // division to hr:mn:sc
                int[] iET = {0, 0, 0};
                iET[0] = end / (60 * 60);
                end = end % (60 * 60);
                iET[1] = end / 60;
                end = end % 60;
                iET[2] = end;

                // convert to string
                EndTime = $"{iET[0]}:{iET[1]}:{iET[2]}";
            }
            
            
            var item = new ScheduleItem(
                /** location **/

                // X is 0 for now
                0, // TODO: what to do with X? X stacks for the same start time
                // hr, mn, sc sets the Y position
                (hr * LINE_SPACE_HR + mn * LINE_SPACE_MN +  + sc * LINE_SPACE_SC) / LINE_SPACE_FACTOR,
                // default W(idth)
                SCHEDULE_ITEM_DEFAULT_SIZE.Width,
                // duration determines H(eight), convert to minute first
                sel.Item4 / LINE_SPACE_FACTOR,

                /** schedule params **/

                // append schedule item
                sel.Item1,
                sel.Item2,
                sel.Item3,
                sel.Item4,
                sel.Item5,
                sel.Item6,
                sel.Item7,
                sel.Item8,
                sel.Item9,

                /** associated activity params **/

                aitem.Label,

                /** state **/

                EndTime,
                false,
                STROKE_UNCONFIRMED,
                TEXT_COLOUR_UNCONFIRMED
            );

            // insert in the Binding collection
            ScheduleItems.Add(item);

            
            /** grouping **/

            // first place in default Group
            if (! GroupedScheduleItems_Start_Unconfirmed.ContainsKey(item.StartTime))
            {
                GroupedScheduleItems_Start_Unconfirmed[item.StartTime] = new();
            }
            if (! GroupedScheduleItems_Stop_Unconfirmed.ContainsKey(item.StopTime))
            {
                GroupedScheduleItems_Stop_Unconfirmed[item.StopTime] = new();
            }
            GroupedScheduleItems_Start_Unconfirmed[item.StartTime].Add(item.Id);
            GroupedScheduleItems_Stop_Unconfirmed[item.StopTime].Add(item.Id);

            // check if the schedule item is already confirmed
            if (cfrm_start.ContainsKey(item.StartTime) && cfrm_start[item.StartTime].Contains(item.Id))
            {
                // this toggles it to Confirmed state 
                OnScheduleItemConfirmClicked(item.Id);
            }
        }
    }
    
    // refresh the list from the DB
    private void FreshActivityItems()
    {
        // TODO: DatabaseService can control both instances: DB and Var

        InsertActivityItems.Clear();

        // Pull the list from the database
        (bool status, string? details, List<(int, int, string, string)>? select) = DatabaseService.Instance.SelectActivities();
        // verify operation status
        if (! status) throw new Exception ($"Failed to load from DB: {details}");
        // verify return value
        if (select is null) throw new Exception ($"Select from DB is empty: {details}");

        foreach (var sel in select)
        {
            InsertActivityItems.Add(new ActivityItem(
                sel.Item1,  // Id
                sel.Item3   // Label
            ));
        }        
    }

    private void DropScheduleItem(int scheduleId)
    {
        Console.WriteLine($"Attempt to Schedule {scheduleId}");

        // request programme delete
        // the DB sends a [delete] event
        (bool status, string? details) = DatabaseService.Instance.DeleteSchedule(scheduleId);
        // verify operation status
        if (! status)
        {   
            Console.WriteLine ($"Failed to DELETE from DB: {details}");
        }
    }

    private void RefreshScheduleNowOrMarkForLater()
    {
        // refresh only when focused
        if (focused) {
            FreshScheduleItems();
        }
        // wait for focus to refresh
        else
        {
            mark_schedule_for_refresh_on_focus = true;
        }
    }

    private void RefreshActivityNowOrMarkForLater()
    {
        // refresh only when focused
        if (focused) {
            FreshActivityItems();
        }
        // wait for focus to refresh
        else
        {
            mark_activity_for_refresh_on_focus = true;
        }
    }

    /** OnTick **/

    // token to stop the timer
    private CancellationTokenSource cts {get; } = new();

    // ontick timer waits for tick event every second
    private async Task OnTickTimer(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            OnTick();
        }
    }

    private void OnTick()
    {
        // update UI-bound properties
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            // update time indicators
            var now = DateTime.Now;
            CurretTimeOverlineY = (now.Hour * LINE_SPACE_HR + now.Minute * LINE_SPACE_MN + now.Second * LINE_SPACE_SC) / LINE_SPACE_FACTOR;
            CurretTimeLabel = $"{((now.Hour < 10)? "0":"")}{now.Hour}:{now.Minute}:{now.Second}";

            // search for current task
            _ = SearchForConfirmedSchedulesAtCurrentTime();
        });
    }

    private async Task SearchForConfirmedSchedulesAtCurrentTime()
    {
        // notification for items on the schedule
        if (GroupedScheduleItems_Start_Confirmed.ContainsKey(CurretTimeLabel))
        {
            SystemNotificationService.Instance.Send("Schedule", $"{GroupedScheduleItems_Start_Confirmed[CurretTimeLabel].Count} items starting now");
        }

        // notification for items to be reschedules
        if (GroupedScheduleItems_Start_Unconfirmed.ContainsKey(CurretTimeLabel))
        {
            SystemNotificationService.Instance.Send("Schedule", $"Rescheduling {GroupedScheduleItems_Start_Unconfirmed[CurretTimeLabel].Count} items");
        }
    }

    private async Task RescheduleUnconfimed()
    {
        // move them down by 5 minutes

        // copy ONLY the unconfirmed schedule items START
        // STOP is derived from START
        // elements with the same START may not have the same STOP
        var uncfrm = new List<int>(GroupedScheduleItems_Start_Unconfirmed[CurretTimeLabel]);

        // from the unconfirmed START, remove START and STOP
        // also add the new elements
        foreach (int sId in uncfrm)
        {
            // find the associated sId
            var item = ScheduleItems.FirstOrDefault(p => p.Id == sId);

            if (item is null) continue;

            // find the associated index
            var idx = ScheduleItems.IndexOf(item!);

            // remove the unconfirmed START at current time
            GroupedScheduleItems_Start_Unconfirmed[item.StartTime].Remove(sId);
            // remove the unconfirmed START at current time
            GroupedScheduleItems_Stop_Unconfirmed[item.StopTime].Remove(sId);


            /** reschedule / regroup **/
            
            // convert to seconds
            int startTime = ConvertTimeStringToSecondsInteger(item.StartTime);
            // add 5 minutes
            startTime += 60 * 5;

            // include the duration
            int endTime = startTime + item.Duration;

            // cap time
            startTime = CapTimeToEndOfDay(startTime);
            endTime = CapTimeToEndOfDay(endTime);

            if (startTime == endTime) continue;

            item = item with {
                StartTime = ConvertSecondsIntegerToTimeString(startTime),
                StopTime =  ConvertSecondsIntegerToTimeString(endTime)
            };
            ScheduleItems[idx] = item;

            GroupedScheduleItems_Start_Unconfirmed[item.StartTime].Add(sId);
            GroupedScheduleItems_Stop_Unconfirmed[item.StopTime].Add(sId);
            

            // pruning
            _ = GroupedScheduleItemsModified(GroupedScheduleItems_Start_Unconfirmed, item.StartTime);
            _ = GroupedScheduleItemsModified(GroupedScheduleItems_Stop_Unconfirmed, item.StopTime);
        }
    }

    private int ConvertTimeStringToSecondsInteger(string time)
    {
        string[] split = time.Split(":");
        return (int.Parse(split[0]) * 60 * 60) + (int.Parse(split[1]) * 60) + int.Parse(split[2]);
    }

    private int CapTimeToEndOfDay(int time)
    {
        // max out at the end of the day
        return Math.Min(time, (23 * 60 * 60) + (59 * 60) + 59);
    }

    private string ConvertSecondsIntegerToTimeString(int time)
    {
        // division to hr:mn:sc
        int[] iET = {0, 0, 0};
        
        iET[0] = time / (60 * 60);
        
        time = time % (60 * 60);
        iET[1] = time / 60;

        time = time % 60;
        iET[2] = time;

        return $"{iET[0]}:{iET[1]}:{iET[2]}";
    }



    /**
     * ! [event handlers]
     * ! events that the vm invokes
     * ! events that the main view handles from other vm
    **/

    /** event for the `insert` overlay  **/

    public event EventHandler? CloseScheduleInsertOverlay;

    /** events for status  **/

    // show the operation status
    public event EventHandler? ShowScheduleInsertStatus;
    // hide the operation status
    public event EventHandler? HideScheduleInsertStatus;

    /** database notifications **/

    private void OnDatabaseNotifyScheduleInserted(object? sender, EventArgs e)
    {
        RefreshScheduleNowOrMarkForLater();
    }

    private void OnDatabaseNotifyScheduleUpdated(object? sender, EventArgs e)
    {
        RefreshScheduleNowOrMarkForLater();
    }

    private void OnDatabaseNotifyScheduleDeleted(object? sender, EventArgs e)
    {
        RefreshScheduleNowOrMarkForLater();
    }

    private void OnDatabaseNotifyActivityInserted(object? sender, EventArgs e)
    {
        RefreshActivityNowOrMarkForLater();
    }

    private void OnDatabaseNotifyActivityUpdated(object? sender, EventArgs e)
    {
        RefreshActivityNowOrMarkForLater();
    }

    private void OnDatabaseNotifyActivityDeleted(object? sender, EventArgs e)
    {
        RefreshActivityNowOrMarkForLater();
    }

    

    /**
     * ! [class inheritance]
     * ! properties of inherited class
    **/
    
    // title of the vm
    public override string PageTitle => "Schedule";

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
        if (mark_schedule_for_refresh_on_focus)
        {
            FreshScheduleItems();
            // unflag refresh
            mark_schedule_for_refresh_on_focus = false;
        }
        if (mark_activity_for_refresh_on_focus)
        {
            FreshActivityItems();
            // unflag refresh
            mark_activity_for_refresh_on_focus = false;
        }
    }


    /**
     * ! [records]
     * ! where they are meant to be
    **/

    public record Liner(int Y, string Label);
    public record ScheduleItem(
        // location
        int X,
        int Y,
        int W,
        int H,
        // schedule params
        int Id,
        int ActivityId,
        string StartTime,
        int Duration,
        string Date,
        int DailyInterval,
        int WeeklyInterval,
        int MonthlyInterval,
        int AnnualInterval,
        // associated activity params
        string ActivityLabel,
        // state
        string StopTime,
        bool Confirmation,
        string Stroke,
        string TextColour
    );

    public record ActivityItem(int Id, string Label);
}
