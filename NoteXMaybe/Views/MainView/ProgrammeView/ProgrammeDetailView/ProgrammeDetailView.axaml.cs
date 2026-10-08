
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using NoteXMaybe.ViewModels;

namespace NoteXMaybe.Views;

public partial class ProgrammeDetailView : UserControl
{
    public ProgrammeDetailView()
    {
        InitializeComponent();

        // Register events from ViewModel
        DataContextChanged += (_, _) => {
            if (DataContext is ProgrammeDetailViewModel cvm) {
                cvm.CloseActivityInsertOverlay += CloseActivityInsertOverlay;
                cvm.ShowInsertActivityStatus += ShowInsertActivityStatus;
                cvm.HideInsertActivityStatus += HideInsertActivityStatus;
            }
        };
    }

    // ! ACTIVITY INSERT OVERLAY

    private void ToggleActivityInsertOverlay(object? sender, RoutedEventArgs e) 
    {
        Console.WriteLine("Open Activity Insert Overlay");
        ActivityInsertOverlay.IsVisible = !ActivityInsertOverlay.IsVisible;
    }

    private void CloseActivityInsertOverlay(object? sender, RoutedEventArgs e)
    {
        Console.WriteLine("Close Activity Overlay");
        ActivityInsertOverlay.IsVisible = false;
    }

    private void CloseActivityInsertOverlay(object? sender, EventArgs e)
    {
        Console.WriteLine("Close Activity Overlay");
        ActivityInsertOverlay.IsVisible = false;
    }

    private void ShowInsertActivityStatus(object? sender, EventArgs e) {
        Console.WriteLine("Show Insert Activity Status");
        InsertActivityStatus.IsVisible = true;
    }

    private void HideInsertActivityStatus(object? sender, EventArgs e) {
        Console.WriteLine("Show Insert Activity Status");
        InsertActivityStatus.IsVisible = false;
    }
}