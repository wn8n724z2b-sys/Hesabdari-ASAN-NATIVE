using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace HesabdariAsan.Native.Dialogs;

public partial class AppDialogWindow : Window
{
    private readonly MessageBoxResult _defaultResult;
    public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;

    public AppDialogWindow(string message, string title, MessageBoxButton buttons, MessageBoxImage icon, MessageBoxResult defaultResult)
    {
        InitializeComponent();
        TitleText.Text = string.IsNullOrWhiteSpace(title) ? "حسابداری آسان" : title;
        MessageText.Text = message ?? "";
        _defaultResult = defaultResult == MessageBoxResult.None ? DefaultFor(buttons) : defaultResult;
        ConfigureIcon(icon);
        BuildButtons(buttons);
        Loaded += (_, _) => AnimateIn();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                e.Handled = true;
                Result = buttons is MessageBoxButton.YesNo or MessageBoxButton.YesNoCancel ? MessageBoxResult.No : MessageBoxResult.Cancel;
                Close();
            }
        };
    }

    private void ConfigureIcon(MessageBoxImage icon)
    {
        var (text, bg, fg) = icon switch
        {
            MessageBoxImage.Error or MessageBoxImage.Stop => ("!", "DangerSoft", "Danger"),
            MessageBoxImage.Warning or MessageBoxImage.Exclamation => ("!", "WarningSoft", "Warning"),
            MessageBoxImage.Question => ("?", "AccentSoft", "Accent"),
            _ => ("i", "AccentSoft", "Accent")
        };
        IconText.Text = text;
        if (Application.Current?.Resources[bg] is Brush background) IconHost.Background = background;
        if (Application.Current?.Resources[fg] is Brush foreground) IconText.Foreground = foreground;
    }

    private void BuildButtons(MessageBoxButton buttons)
    {
        ButtonsPanel.Children.Clear();
        switch (buttons)
        {
            case MessageBoxButton.OK:
                AddButton("تأیید", MessageBoxResult.OK, true);
                break;
            case MessageBoxButton.OKCancel:
                AddButton("انصراف", MessageBoxResult.Cancel, false);
                AddButton("تأیید", MessageBoxResult.OK, true);
                break;
            case MessageBoxButton.YesNo:
                AddButton("خیر", MessageBoxResult.No, false);
                AddButton("بله", MessageBoxResult.Yes, true);
                break;
            case MessageBoxButton.YesNoCancel:
                AddButton("انصراف", MessageBoxResult.Cancel, false);
                AddButton("خیر", MessageBoxResult.No, false);
                AddButton("بله", MessageBoxResult.Yes, true);
                break;
        }
    }

    private void AddButton(string text, MessageBoxResult result, bool primary)
    {
        var b = new Button
        {
            Content = text,
            MinWidth = 96,
            Height = 39,
            MinHeight = 39,
            Margin = new Thickness(6, 0, 0, 0),
            Style = (Style)FindResource(primary ? "PrimaryButton" : "SecondaryButton"),
            IsDefault = result == _defaultResult,
            IsCancel = result == MessageBoxResult.Cancel || (result == MessageBoxResult.No && _defaultResult == MessageBoxResult.No)
        };
        b.Click += (_, _) => { Result = result; Close(); };
        ButtonsPanel.Children.Add(b);
    }

    private void AnimateIn()
    {
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        Root.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)) { EasingFunction = ease });
        if (Root.RenderTransform is TranslateTransform t)
            t.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(14, 0, TimeSpan.FromMilliseconds(200)) { EasingFunction = ease });
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Result = _defaultResult;
        Close();
    }

    private static MessageBoxResult DefaultFor(MessageBoxButton buttons) => buttons switch
    {
        MessageBoxButton.YesNo => MessageBoxResult.No,
        MessageBoxButton.YesNoCancel => MessageBoxResult.Cancel,
        MessageBoxButton.OKCancel => MessageBoxResult.Cancel,
        _ => MessageBoxResult.OK
    };
}
