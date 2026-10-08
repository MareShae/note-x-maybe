

using System;
using Avalonia;
using Avalonia.Controls;

namespace NoteXMaybe.CustomControls;
/**
 * A custom Panel that allows for specific positioning but automated sizing
**/ 
public class PositionPanel: Panel
{
    /** 
     * ! [attached properties]
     * ! attached properties for the .axaml
    **/

    /** X property **/

    // register property name
    public static readonly AttachedProperty<double> XProperty = AvaloniaProperty.RegisterAttached<PositionPanel, Control, double>("X");
    // register setter
    public static void SetX(Control element, double value) => element.SetValue(XProperty, value);
    // register getter
    public static double GetX(Control element) => element.GetValue(XProperty);

    /** Y property **/

    // register property name
    public static readonly AttachedProperty<double> YProperty = AvaloniaProperty.RegisterAttached<PositionPanel, Control, double>("Y");
    // register setter
    public static void SetY(Control element, double value) => element.SetValue(YProperty, value);
    // register getter
    public static double GetY(Control element) => element.GetValue(YProperty);

    static PositionPanel()
    {
        AffectsArrange<PositionPanel>(XProperty);
        AffectsArrange<PositionPanel>(YProperty);
    }
    
    /**
     * Measures the control and its child elements as part of a layout pass.
     * Returns:
     *  The desired size for the control.
    **/
    protected override Size MeasureOverride(Size availableSize)
    {
        double width = 0, height = 0;

        foreach (var child in Children)
        {
            // calculates desiredsize
            child.Measure(Size.Infinity);

            // set the set position
            var x = GetX(child);
            var y = GetY(child);

            // constrain size
            width = Math.Max(width, x + child.DesiredSize.Width);
            height = Math.Max(height, y + child.DesiredSize.Height);
        }
        
        return new Size(width, height);
    }

    /**
     * Positions child elements as part of a layout pass.
     * Returns:
     *     The actual size used.
    **/
    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
        {
            var x = GetX(child);
            var y = GetY(child);

            child.Arrange(new Rect(
                new Point(x, y),
                child.DesiredSize
            ));
        }
        return finalSize;
    }
}