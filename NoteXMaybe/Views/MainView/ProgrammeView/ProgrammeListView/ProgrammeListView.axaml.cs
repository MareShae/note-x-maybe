
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using NoteXMaybe.ViewModels;

namespace NoteXMaybe.Views;

public partial class ProgrammeListView : UserControl
{
    public ProgrammeListView()
    {
        InitializeComponent();

        // Register events from ViewModel
        DataContextChanged += (_, _) => {
            if (DataContext is ProgrammeListViewModel cvm) {
                cvm.CloseProgrammeInsertOverlay += CloseProgrammeInsertOverlay;
                cvm.ShowInsertProgrammeStatus += ShowInsertProgrammeStatus;
                cvm.HideInsertProgrammeStatus += HideInsertProgrammeStatus;
            }
        };
    }

    // ! PROGRAMME INSERT OVERLAY

    private void ToggleProgrammeInsertOverlay(object? sender, RoutedEventArgs e) 
    {
        Console.WriteLine("Open Programme Insert Overlay");
        ProgrammeInsertOverlay.IsVisible = !ProgrammeInsertOverlay.IsVisible;
    }

    private void CloseProgrammeInsertOverlay(object? sender, RoutedEventArgs e)
    {
        Console.WriteLine("Close Programme Overlay");
        ProgrammeInsertOverlay.IsVisible = false;
    }

    private void CloseProgrammeInsertOverlay(object? sender, EventArgs e)
    {
        Console.WriteLine("Close Programme Overlay");
        ProgrammeInsertOverlay.IsVisible = false;
    }

    private void ShowInsertProgrammeStatus(object? sender, EventArgs e) {
        Console.WriteLine("Show Insert Programme Status");
        InsertProgrammeStatus.IsVisible = true;
    }

    private void HideInsertProgrammeStatus(object? sender, EventArgs e) {
        Console.WriteLine("Show Insert Programme Status");
        InsertProgrammeStatus.IsVisible = false;
    }
}