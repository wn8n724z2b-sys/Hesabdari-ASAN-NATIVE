using System.Windows;
using System.Windows.Controls;
using HesabdariAsan.Native.Models;
using HesabdariAsan.Native.Services;

namespace HesabdariAsan.Native.Dialogs;

public partial class FirstRunWindow : Window
{
    private readonly SettingsService _settingsService = new();
    private readonly ReceiptPrinterService _printer = new();
    private readonly AdminSecurityService _security = new();
    private readonly AppSettings _settings;

    public FirstRunWindow()
    {
        InitializeComponent();
        _settings = _settingsService.Load();
        StoreNameBox.Text = _settings.ShopName == "فروشگاه من" ? "" : _settings.ShopName;
        AddressBox.Text = _settings.ShopAddress;
        PhoneBox.Text = _settings.ShopPhone;
        ThemeBox.SelectedIndex = string.Equals(_settings.Theme, "Dark", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        BackupBox.SelectedIndex = Math.Clamp(_settings.AutoBackupPerDay, 1, 6) - 1;
        LoadPrinters();
    }

    private void DetectPrinters_Click(object sender, RoutedEventArgs e) => LoadPrinters(true);

    private void LoadPrinters(bool showStatus = false)
    {
        var current = PrinterBox.Text;
        PrinterBox.Items.Clear();
        foreach (var name in _printer.GetInstalledPrinters()) PrinterBox.Items.Add(name);
        var preferred = PrinterBox.Items.Cast<string>().FirstOrDefault(x => x.Contains("XPrinter", StringComparison.OrdinalIgnoreCase))
                        ?? PrinterBox.Items.Cast<string>().FirstOrDefault();
        PrinterBox.Text = !string.IsNullOrWhiteSpace(current) ? current : preferred ?? "";
        if (showStatus) ErrorText.Text = PrinterBox.Items.Count == 0 ? "پرینتری توسط Windows شناسایی نشد؛ بعداً می‌توانید تنظیم کنید." : $"{PrinterBox.Items.Count} پرینتر شناسایی شد.";
    }

    private void Finish_Click(object sender, RoutedEventArgs e)
    {
        var name = StoreNameBox.Text.Trim();
        var password = PasswordBox.Password;
        var confirm = ConfirmPasswordBox.Password;
        if (string.IsNullOrWhiteSpace(name)) { ErrorText.Text = "نام فروشگاه را وارد کنید."; StoreNameBox.Focus(); return; }
        if (password.Length < 4) { ErrorText.Text = "رمز مدیر حداقل 4 کاراکتر باشد."; PasswordBox.Focus(); return; }
        if (!string.Equals(password, confirm, StringComparison.Ordinal)) { ErrorText.Text = "تکرار رمز مدیر یکسان نیست."; ConfirmPasswordBox.Focus(); return; }

        try
        {
            _security.SetPassword(password);
            _settings.ShopName = name;
            _settings.ShopAddress = AddressBox.Text.Trim();
            _settings.ShopPhone = PhoneBox.Text.Trim();
            _settings.PrinterName = PrinterBox.Text.Trim();
            _settings.Theme = (ThemeBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() == "Dark" ? "Dark" : "Light";
            _settings.AutoBackupPerDay = int.TryParse((BackupBox.SelectedItem as ComboBoxItem)?.Tag?.ToString(), out var n) ? Math.Clamp(n, 1, 6) : 4;
            _settings.OnboardingComplete = true;
            _settingsService.Save(_settings);
            ThemeService.Apply(_settings.Theme);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            AppLog.Error(ex, "FirstRun");
            ErrorText.Text = "ذخیره تنظیمات اولیه انجام نشد: " + ex.Message;
        }
    }
}
