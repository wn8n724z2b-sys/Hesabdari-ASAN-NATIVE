using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace HesabdariAsan.Native.Controls;

public sealed class AnimatedContentControl : ContentControl
{
    private readonly TranslateTransform _translate = new();
    private readonly ScaleTransform _scale = new(1, 1);

    public AnimatedContentControl()
    {
        RenderTransformOrigin = new Point(0.5, 0.15);
        var group = new TransformGroup();
        group.Children.Add(_scale);
        group.Children.Add(_translate);
        RenderTransform = group;
        SnapsToDevicePixels = true;
        UseLayoutRounding = true;
    }

    protected override void OnContentChanged(object oldContent, object newContent)
    {
        base.OnContentChanged(oldContent, newContent);
        if (IsLoaded) Animate();
    }

    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);
        Loaded += (_, _) => Animate();
    }

    private void Animate()
    {
        BeginAnimation(OpacityProperty, null);
        _translate.BeginAnimation(TranslateTransform.YProperty, null);
        _scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        _scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        BeginAnimation(OpacityProperty, new DoubleAnimation(0.22, 1, TimeSpan.FromMilliseconds(155)) { EasingFunction = ease });
        _translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(11, 0, TimeSpan.FromMilliseconds(190)) { EasingFunction = ease });
        _scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.994, 1, TimeSpan.FromMilliseconds(190)) { EasingFunction = ease });
        _scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.994, 1, TimeSpan.FromMilliseconds(190)) { EasingFunction = ease });
    }
}
