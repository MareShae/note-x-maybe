using System;
using System.Collections;
using System.Collections.Generic;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NoteXMaybe.ViewModels;


/**
 * main view model contains navigation and context control.
 * [back] and [fore] buttons allow moving between controlled pages.
 * its context is various stacks on the [programme] and [schedule] vm.
 *
 * [glossary]
 * ! [...strutor]
 * ! [.axaml]
 * ! [code-behind]
 * ! [event handlers]
 * !
**/
public partial class MainViewModel : ViewModelBase
{   
    /**
     * ! [...strutor]
    **/

    // constructor

    public MainViewModel()
    {
        Console.WriteLine("Show platform...");
        #if DESKTOP
            Console.WriteLine("Platform is DESKTOP");
        #endif
        #if LINUX
            Console.WriteLine("Platform is LINUX");
        #endif

        SystemNotificationService.Instance.Send("Starting...", "This is a successful notification");

        RequestStartVM();
    }


    /**
     * ! [.axaml]
     * ! show view properties
    **/

    // backstack navigation

    // the stack behind the current page
    private Stack<ObservableObject> _backstack = new();

    [ObservableProperty]
    // visibility property of the [back] button
    public partial bool BackButtonVisible {get; set; } = false;

    [RelayCommand]
    // command on [back] button click
    public void CommandNavigateBackPage()
    {
        if (CurrentPage is not null)
        {
            // notify the page on leave
            ((ContentViewModelBase)CurrentPage).OnLeaveEvent();
            // insert into the [fore] stack
            _forestack.Push(CurrentPage);
        }

        // pluck the last in the [back] stack
        CurrentPage = _backstack.Pop();
        if (CurrentPage is not null) {
            // notify the page on focus
            ((ContentViewModelBase)CurrentPage).OnFocusEvent();
        }
    }


    
    // navigation title

    [ObservableProperty]
    public partial string NavigationPageTitle { get; set; } = "";



    // forestack navigation

    // the stack after the current page
    private Stack<ObservableObject> _forestack = new();

    [ObservableProperty]
    // visibility property of the [fore] button
    public partial bool ForeButtonVisible {get; set; } = false;

    [RelayCommand]
    // command on [fore] button click
    public void CommandNavigateForePage()
    {
        if (CurrentPage is not null)
        {   
            // notify the page on leave
            ((ContentViewModelBase)CurrentPage).OnLeaveEvent();
            // insert into the [back] stack
            _backstack.Push(CurrentPage);
        }

        // pluck the last in the [fore] stack
        CurrentPage = _forestack.Pop();
        if (CurrentPage is not null) {
            // notify the page on focus
            ((ContentViewModelBase)CurrentPage).OnFocusEvent();
        }
    }



    // current page navigation

    [ObservableProperty]
    public partial ObservableObject? CurrentPage { get; set; } = null;

    // current page on property changed
    partial void OnCurrentPageChanged(ObservableObject? value)
    {
        if (value is null) return;

        // modify the title of the navigated page
        PageTitleFromViewModel(value);

        // set visibility based on the available pages
        BackButtonVisible = _backstack.Count > 0;
        ForeButtonVisible = _forestack.Count > 0;
    }

    private void PageTitleFromViewModel(ObservableObject page)
    {
        // get the title from this specific vm
        NavigationPageTitle = ((ContentViewModelBase)page).PageTitle;
    }


    // TODO: one-hot encoded view


    // programme view

    [RelayCommand]
    // command on [programme] button click
    // TODO: a dedicated page for [programme]
    public void CommandRequestProgrammeVM()
    {
        OnRequestProgrammeListVM(this, EventArgs.Empty);
    }


    
    // schedule view

    [RelayCommand]
    // command on [schedule] button click
    public void CommandRequestScheduleVM()
    {
        RequestScheduleVM();
    }


    /**
     * ! [code-behind]
     * ! the logic to the view
    **/
    

    private void NavigateToNewPage(ObservableObject page)
    {
        if (CurrentPage is not null)
        {
            _backstack.Push(CurrentPage);
        }
        _forestack.Clear();

        CurrentPage = page;
        
    }

    private void RequestStartVM()
    {
        var vm = new StartViewModel();

        // register vm event for main view
        vm.RequestProgrammeListVM += OnRequestProgrammeListVM;

        NavigateToNewPage(vm);

        // setup is required, otherwise the event invokes before it can be registered
        vm.Setup();
    }

    private void RequestProgrammeVM()
    {
        ;
    }

    private void RequestScheduleVM()
    {
        /**
         * creates and navigates to schedule vm
        **/

        var vm = new ScheduleViewModel();

        NavigateToNewPage(vm);
    }



    /**
     * ! [event handlers]
     * ! events that the main view handles from other vm
    **/

    // programme list

    public void OnRequestProgrammeListVM(object? sender, EventArgs e)
    {
        /**
         * creates and navigates to programme list vm
        **/

        Console.WriteLine("Switching to Programme List View");

        var vm = new ProgrammeListViewModel();

        // register vm event for main view
        vm.RequestProgrammeDetailsVM += OnRequestProgrammeDetailsVM;

        NavigateToNewPage(vm);
         
    }

    // programme detail

    public void OnRequestProgrammeDetailsVM(object? sender, EventArgs e)
    {
        /**
         * creates and navigates to programme detail vm
        **/
        
        Console.WriteLine("Switching to Programme Details View");
        
        var vm = new ProgrammeDetailViewModel(
            ((ProgrammeListViewModel.MainViewSwitchEventArgs)e).ProgrammeId
        );

        // register vm event for main view
        vm.RequestActivityDetailsVM += OnRequestActivityDetailsVM;

        NavigateToNewPage(vm);
    }

    // activity detail

    public void OnRequestActivityDetailsVM(object? sender, EventArgs e)
    {
        /**
         * creates and navigates to activity detail vm
        **/
        
        Console.WriteLine("Switching to Activity Details View");

        var vm = new ActivityViewModel(
            ((ProgrammeDetailViewModel.MainViewSwitchEventArgs)e).ActivityId
        );

        NavigateToNewPage(vm);
    }
}



/**
 * this abstract class is used by all vms that are injected into contextcontrol.
 * it defines required properties that are used by the main view in switching context.
 *
 * since they are abstract c# requires that all its children overide the properties.
 * no defaults are given.
**/
public abstract class ContentViewModelBase : ViewModelBase
{
    // used by the navigation page title   
    abstract public string PageTitle {get;}

    // called when context control switches away
    abstract public void OnLeaveEvent();

    // called when context control switches to (not on new)
    abstract public void OnFocusEvent();
}