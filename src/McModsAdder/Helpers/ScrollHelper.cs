using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace McModsAdder.Helpers;

public static class ScrollHelper
{
    public static readonly DependencyProperty AutoHideScrollBarsProperty =
        DependencyProperty.RegisterAttached(
            "AutoHideScrollBars",
            typeof(bool),
            typeof(ScrollHelper),
            new PropertyMetadata(false, OnAutoHideScrollBarsChanged));

    private static readonly DependencyProperty HideTimerProperty =
        DependencyProperty.RegisterAttached(
            "HideTimer",
            typeof(DispatcherTimer),
            typeof(ScrollHelper),
            new PropertyMetadata(null));

    public static void SetAutoHideScrollBars(DependencyObject element, bool value) =>
        element.SetValue(AutoHideScrollBarsProperty, value);

    public static bool GetAutoHideScrollBars(DependencyObject element) =>
        (bool)element.GetValue(AutoHideScrollBarsProperty);

    public static void HandleWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }

        var node = Mouse.DirectlyOver as DependencyObject;
        while (node != null)
        {
            if (node is Page)
            {
                e.Handled = true;
                return;
            }

            if (node is ScrollViewer sv && sv.ScrollableHeight > 0)
            {
                var canDown = sv.VerticalOffset < sv.ScrollableHeight - 0.5;
                var canUp = sv.VerticalOffset > 0.5;
                if ((e.Delta < 0 && canDown) || (e.Delta > 0 && canUp))
                {
                    sv.ScrollToVerticalOffset(sv.VerticalOffset - e.Delta);
                    e.Handled = true;
                    return;
                }
            }

            node = GetParent(node);
        }
    }

    private static void OnAutoHideScrollBarsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ScrollViewer scrollViewer)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            scrollViewer.ScrollChanged += ScrollViewer_ScrollChanged;
            scrollViewer.Loaded += ScrollViewer_Loaded;
        }
        else
        {
            scrollViewer.ScrollChanged -= ScrollViewer_ScrollChanged;
            scrollViewer.Loaded -= ScrollViewer_Loaded;
        }
    }

    private static void ScrollViewer_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer)
        {
            SetScrollBarsVisibility(scrollViewer, Visibility.Collapsed);
        }
    }

    private static void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer)
        {
            return;
        }

        SetScrollBarsVisibility(scrollViewer, Visibility.Visible);

        var timer = (DispatcherTimer?)scrollViewer.GetValue(HideTimerProperty);
        if (timer == null)
        {
            timer = new DispatcherTimer(DispatcherPriority.Input)
            {
                Interval = TimeSpan.FromMilliseconds(650)
            };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                SetScrollBarsVisibility(scrollViewer, Visibility.Collapsed);
            };
            scrollViewer.SetValue(HideTimerProperty, timer);
        }

        timer.Stop();
        timer.Start();
    }

    private static void SetScrollBarsVisibility(ScrollViewer scrollViewer, Visibility visibility)
    {
        scrollViewer.ApplyTemplate();
        if (scrollViewer.Template.FindName("PART_VerticalScrollBar", scrollViewer) is ScrollBar verticalScrollBar)
        {
            verticalScrollBar.Visibility = visibility;
            MakeScrollBarButtonsTransparent(verticalScrollBar);
        }

        if (scrollViewer.Template.FindName("PART_HorizontalScrollBar", scrollViewer) is ScrollBar horizontalScrollBar)
        {
            horizontalScrollBar.Visibility = visibility;
            MakeScrollBarButtonsTransparent(horizontalScrollBar);
        }

        if (scrollViewer.Template.FindName("PART_Corner", scrollViewer) is FrameworkElement corner)
        {
            corner.Visibility = Visibility.Collapsed;
        }
    }

    private static void MakeScrollBarButtonsTransparent(ScrollBar scrollBar)
    {
        foreach (var button in FindVisualChildren<RepeatButton>(scrollBar))
        {
            button.Background = Brushes.Transparent;
            button.BorderBrush = Brushes.Transparent;
            button.Foreground = Brushes.Transparent;
            button.BorderThickness = new Thickness(0);
            button.Padding = new Thickness(0);
        }
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T item)
            {
                yield return item;
            }

            foreach (var descendant in FindVisualChildren<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private static DependencyObject? GetParent(DependencyObject node) => node switch
    {
        Visual or System.Windows.Media.Media3D.Visual3D => VisualTreeHelper.GetParent(node),
        FrameworkContentElement fce => fce.Parent,
        _ => null
    };
}
