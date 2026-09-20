using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace HesabdariAsan.Native.Controls;

public sealed class AnimatedContentControl : ContentControl
{
    public AnimatedContentControl()
    {
        RenderTransformOrigin = new Point(0.5, 0.5);
        RenderTransform = new TranslateTransform();
    }

    protected override void OnContentChanged(object oldContent, object newContent)
    {
        base.OnContentChanged(oldContent, newContent);
        if (!IsLoaded) return;
        Animate();
    }

    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);
        Loaded += (_, _) => Animate();
    }

    private void Animate()
    {
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        BeginAnimation(OpacityProperty, new DoubleAnimation(0.35, 1, TimeSpan.FromMilliseconds(175)) { EasingFunction = ease });
        if (RenderTransform is TranslateTransform t)
            t.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(9, 0, TimeSpan.FromMilliseconds(190)) { EasingFunction = ease });
    }
}
