using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace HesabdariAsan.Native.Controls;

/// <summary>
/// Native wheel/touch scrolling tuned for desktop accounting screens.
/// Repeated wheel input continues from the current animated position instead of
/// restarting from a stale layout offset, which removes the RC2 micro-jitter.
/// </summary>
public static class SmoothScrollBehavior
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(SmoothScrollBehavior), new PropertyMetadata(false, OnIsEnabledChanged));

    private static readonly DependencyProperty AnimatedOffsetProperty = DependencyProperty.RegisterAttached(
        "AnimatedOffset", typeof(double), typeof(SmoothScrollBehavior),
        new PropertyMetadata(0d, (d, e) =>
        {
            if (d is ScrollViewer sv) sv.ScrollToVerticalOffset((double)e.NewValue);
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
            sv.ScrollChanged += OnScrollChanged;
            sv.PanningMode = PanningMode.VerticalOnly;
            sv.PanningDeceleration = 0.0024;
            sv.PanningRatio = 1.0;
        }
        else
        {
            sv.PreviewMouseWheel -= OnPreviewMouseWheel;
            sv.Loaded -= OnLoaded;
            sv.ScrollChanged -= OnScrollChanged;
            sv.BeginAnimation(AnimatedOffsetProperty, null);
        }
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not ScrollViewer sv) return;
        Sync(sv, sv.VerticalOffset);
    }

    private static void OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (sender is not ScrollViewer sv || e.ExtentHeightChange == 0) return;
        var target = Math.Clamp((double)sv.GetValue(TargetOffsetProperty), 0, sv.ScrollableHeight);
        sv.SetValue(TargetOffsetProperty, target);
    }

    private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer sv || sv.ScrollableHeight <= 0) return;

        var direction = -Math.Sign(e.Delta);
        var atTop = sv.VerticalOffset <= 0.5 && direction < 0;
        var atBottom = sv.VerticalOffset >= sv.ScrollableHeight - 0.5 && direction > 0;
        if (atTop || atBottom) return; // nested ScrollViewer can hand the wheel to its parent

        var animatedNow = (double)sv.GetValue(AnimatedOffsetProperty);
        if (double.IsNaN(animatedNow) || Math.Abs(animatedNow - sv.VerticalOffset) > 260)
            animatedNow = sv.VerticalOffset;

        var currentTarget = (double)sv.GetValue(TargetOffsetProperty);
        if (Math.Abs(currentTarget - animatedNow) > 520) currentTarget = animatedNow;

        // Trackpads and high-resolution wheels can report smaller deltas. Clamp the
        // step so one gesture remains responsive without jumping a full screen.
        var magnitude = Math.Clamp(Math.Abs(e.Delta) / 120d, 0.45, 2.2);
        var step = 78d * magnitude;
        var target = Math.Clamp(currentTarget + direction * step, 0, sv.ScrollableHeight);
        if (Math.Abs(target - animatedNow) < 0.25) return;

        sv.BeginAnimation(AnimatedOffsetProperty, null);
        sv.SetValue(AnimatedOffsetProperty, animatedNow);
        sv.SetValue(TargetOffsetProperty, target);

        var distance = Math.Abs(target - animatedNow);
        var duration = TimeSpan.FromMilliseconds(Math.Clamp(125 + distance * 0.42, 145, 235));
        var animation = new DoubleAnimation(animatedNow, target, duration)
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop
        };
        animation.Completed += (_, _) =>
        {
            sv.BeginAnimation(AnimatedOffsetProperty, null);
            Sync(sv, target);
        };
        sv.BeginAnimation(AnimatedOffsetProperty, animation, HandoffBehavior.SnapshotAndReplace);
        e.Handled = true;
    }

    private static void Sync(ScrollViewer sv, double offset)
    {
        offset = Math.Clamp(offset, 0, Math.Max(0, sv.ScrollableHeight));
        sv.SetValue(TargetOffsetProperty, offset);
        sv.SetValue(AnimatedOffsetProperty, offset);
        sv.ScrollToVerticalOffset(offset);
    }
}
