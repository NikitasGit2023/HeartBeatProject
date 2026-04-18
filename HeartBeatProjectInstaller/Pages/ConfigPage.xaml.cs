using HeartBeatProjectInstaller.Models;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;

namespace HeartBeatProjectInstaller.Pages;

public partial class ConfigPage : UserControl
{
    public ConfigPage() => InitializeComponent();

    public void Initialize(InstallerConfig config)
    {
        TxRadio.IsChecked = config.Mode != "RX";
        RxRadio.IsChecked = config.Mode == "RX";
        FolderPathBox.Text        = config.FolderPath;
        IntervalBox.Text          = config.IntervalSeconds.ToString();
        CheckIntervalBox.Text     = config.CheckIntervalSeconds.ToString();
        ThresholdBox.Text         = config.ThresholdSeconds.ToString();
        EnableEmailCheck.IsChecked = config.EnableEmail;
        SmtpServerBox.Text        = config.SmtpServer;
        SmtpPortBox.Text          = config.SmtpPort.ToString();
        FromBox.Text              = config.From;
        ToBox.Text                = config.To;
        UpdateModeVisibility();
        UpdateSmtpVisibility();
    }

    public bool Validate(out string error)
    {
        if (string.IsNullOrWhiteSpace(FolderPathBox.Text))
        {
            error = "Folder Path is required.";
            return false;
        }

        if (TxRadio.IsChecked == true)
        {
            if (!int.TryParse(IntervalBox.Text, out int iv) || iv <= 0)
            {
                error = "Heartbeat Interval must be a positive integer.";
                return false;
            }
        }
        else
        {
            if (!int.TryParse(CheckIntervalBox.Text, out int ci) || ci <= 0)
            {
                error = "Check Interval must be a positive integer.";
                return false;
            }
            if (!int.TryParse(ThresholdBox.Text, out int th) || th <= 0)
            {
                error = "Alert Threshold must be a positive integer.";
                return false;
            }
        }

        if (EnableEmailCheck.IsChecked == true &&
            (!int.TryParse(SmtpPortBox.Text, out int port) || port <= 0))
        {
            error = "SMTP Port must be a positive integer.";
            return false;
        }

        error = "";
        return true;
    }

    public void Commit(InstallerConfig config)
    {
        config.Mode                 = TxRadio.IsChecked == true ? "TX" : "RX";
        config.FolderPath           = FolderPathBox.Text.Trim();
        config.IntervalSeconds      = int.TryParse(IntervalBox.Text,      out int iv) ? iv : 30;
        config.CheckIntervalSeconds = int.TryParse(CheckIntervalBox.Text, out int ci) ? ci : 30;
        config.ThresholdSeconds     = int.TryParse(ThresholdBox.Text,     out int th) ? th : 90;
        config.EnableEmail          = EnableEmailCheck.IsChecked == true;
        config.SmtpServer           = SmtpServerBox.Text.Trim();
        config.SmtpPort             = int.TryParse(SmtpPortBox.Text,      out int p)  ? p  : 587;
        config.From                 = FromBox.Text.Trim();
        config.To                   = ToBox.Text.Trim();
    }

    private void Mode_Changed(object sender, RoutedEventArgs e) => UpdateModeVisibility();
    private void EnableEmail_Changed(object sender, RoutedEventArgs e) => UpdateSmtpVisibility();

    private void UpdateModeVisibility()
    {
        if (TxRadio is null || TxPanel is null || RxPanel is null) return;
        bool isTx = TxRadio.IsChecked == true;
        TxPanel.Visibility = isTx ? Visibility.Visible   : Visibility.Collapsed;
        RxPanel.Visibility = isTx ? Visibility.Collapsed : Visibility.Visible;
    }

    private void UpdateSmtpVisibility()
    {
        if (EnableEmailCheck is null || SmtpPanel is null) return;
        SmtpPanel.Visibility = EnableEmailCheck.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Select heartbeat folder" };
        if (dialog.ShowDialog() == true)
            FolderPathBox.Text = dialog.FolderName;
    }
}
