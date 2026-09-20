using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace HesabdariAsan.Native.Controls;

public static class SmoothScrollBehavior
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(SmoothScrollBehavior), new PropertyMetadata(false, OnIsEnabledChanged));

    private static readonly DependencyProperty AnimatedOffsetProperty = DependencyProperty.RegisterAttached(
        "AnimatedOffset", typeof(double), typeof(SmoothScrollBehavior),
        new PropertyMetadata(0d, (_, e) =>
        {
            if (_ is ScrollViewer sv) sv.ScrollToVerticalOffset((double)e.NewValue);
        }));

    private static readonly DependencyProperty TargetOffsetProperty = DependencyProperty.RegisterAttached(
        "TargetOffset", typeof(double), typeof(SmoothScrollBehavior), new PropertyMetadata(0d));

    public static void SetIsEnabled(DependencyObject o, bool value) => o.SetValue(IsEnabledProperty, value);
    public static bool GetIsEnabled(DependencyObject o) => (bool)o.GetValue(IsEnabledProperty);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ScrollViewer sv) return;
        if ((bool)e.NewValue)
        {
            sv.PreviewMouseWheel += OnPreviewMouseWheel;
            sv.Loaded += OnLoaded;
            sv.PanningMode = PanningMode.VerticalOnly;
            sv.PanningDeceleration = 0.0018;
        }
        else
        {
            sv.PreviewMouseWheel -= OnPreviewMouseWheel;
            sv.Loaded -= OnLoaded;
        }
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not ScrollViewer sv) return;
        sv.SetValue(TargetOffsetProperty, sv.VerticalOffset);
        sv.SetValue(AnimatedOffsetProperty, sv.VerticalOffset);
    }

    private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer sv || sv.ScrollableHeight <= 0) return;
        var currentTarget = (double)sv.GetValue(TargetOffsetProperty);
        if (Math.Abs(currentTarget - sv.VerticalOffset) > 320) currentTarget = sv.VerticalOffset;
        var target = Math.Clamp(currentTarget - Math.Sign(e.Delta) * 92d, 0, sv.ScrollableHeight);
        if (Math.Abs(target - currentTarget) < 0.1) return; // Let a parent ScrollViewer continue at the boundary.
        sv.SetValue(TargetOffsetProperty, target);

        var animation = new DoubleAnimation
        {
            From = sv.VerticalOffset,
            To = target,
            Duration = TimeSpan.FromMilliseconds(170),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        sv.BeginAnimation(AnimatedOffsetProperty, animation, HandoffBehavior.SnapshotAndReplace);
        e.Handled = true;
    }
}
