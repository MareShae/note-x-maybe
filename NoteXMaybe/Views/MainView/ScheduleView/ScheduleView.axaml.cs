
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using NoteXMaybe.ViewModels;

namespace NoteXMaybe.Views;

public partial class ScheduleView : UserControl
{
    public ScheduleView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) =>
        {
            if (DataContext is ScheduleViewModel svm)
            {
                svm.CloseScheduleInsertOverlay += CloseScheduleInsertOverlay;
                svm.ShowScheduleInsertStatus += ShowScheduleInsertStatus;
                svm.HideScheduleInsertStatus += HideScheduleInsertStatus;
            }
        };
    }

    private void ShowScheduleInsertOverlay(object? sender, RoutedEventArgs e)
    {
        ScheduleInsertOverlay.IsVisible = true;
    }

    private void CloseScheduleInsertOverlay(object? sender, RoutedEventArgs e)
    {
        ScheduleInsertOverlay.IsVisible = false;
    }

    private void CloseScheduleInsertOverlay(object? sender, EventArgs e)
    {
        ScheduleInsertOverlay.IsVisible = false;
    }

    private void ShowScheduleInsertStatus(object? sender, EventArgs e)
    {
        ScheduleInsertStatus.IsVisible = true;
    }

    private void HideScheduleInsertStatus(object? sender, EventArgs e)
    {
        ScheduleInsertStatus.IsVisible = false;
    }
}