using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;

namespace NoteXMaybe.ViewModels;

public partial class StartViewModel : ContentViewModelBase
{
    /** Abstract Base **/

    public override string PageTitle => "Start Page";

    [ObservableProperty]
    public partial string Info { get; set; } = "Starting...";


    public bool setup {get; private set;} = false;
    public event EventHandler? RequestProgrammeListVM;

    public StartViewModel() {}

    public void Setup()
    {
        // initialize the DB by calling the instance
        var ress = ResService.Instance;
        var dbas = DatabaseService.Instance;

        Console.WriteLine("Start VM Setting Up");

        RequestProgrammeListVM?.Invoke(this, EventArgs.Empty);
        Console.WriteLine("Start VM Setup Done");

        setup = true;
    }


    // !  OVERRIDE METHODS
    public override void OnLeaveEvent()
    {
    }
    
    public override void OnFocusEvent()
    {
    }
}
