using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HeartBeatProjectInstaller.Pages;

public partial class FinishPage : UserControl
{
    public FinishPage() => InitializeComponent();

    public void SetResult(bool success, bool serviceStarted)
    {
        if (!success)
        {
            IconCircle.Background       = new SolidColorBrush(Color.FromRgb(0xE5, 0x39, 0x35));
            IconText.Text               = "✗";
            HeadingText.Text            = "Installation Failed";
            BodyText.Text               = "An error occurred during installation. Review the log on the previous screen for details.";
            ManualStartPanel.Visibility = Visibility.Collapsed;
            return;
        }

        IconCircle.Background = new SolidColorBrush(Color.FromRgb(0x43, 0xA0, 0x47));
        IconText.Text         = "✓";

        if (serviceStarted)
        {
            HeadingText.Text            = "Installation Complete!";
            BodyText.Text               = "The HeartbeatService has been installed and started successfully.";
            ManualStartPanel.Visibility = Visibility.Collapsed;
        }
        else
        {
            HeadingText.Text            = "Service Installed";
            BodyText.Text               = "The HeartbeatService was installed successfully.";
            ManualStartPanel.Visibility = Visibility.Visible;
        }
    }
}
