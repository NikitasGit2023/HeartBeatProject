using HeartBeatProjectInstaller.Models;
using HeartBeatProjectInstaller.Pages;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HeartBeatProjectInstaller;

public partial class MainWindow : Window
{
    private readonly InstallerConfig _config = new();
    private readonly WelcomePage  _welcome;
    private readonly ConfigPage   _config2;
    private readonly InstallPage  _install;
    private readonly FinishPage   _finish;
    private readonly UserControl[] _pages;
    private int _step = 0;

    // Sidebar elements grouped for easy update
    private Border[]    StepDots  => [Step1Dot,  Step2Dot,  Step3Dot,  Step4Dot];
    private TextBlock[] StepLabels => [Step1Lbl,  Step2Lbl,  Step3Lbl,  Step4Lbl];
    private TextBlock[] StepTexts  => [Step1Text, Step2Text, Step3Text, Step4Text];

    private static readonly SolidColorBrush BrushActive    = Brushes.White;
    private static readonly SolidColorBrush BrushDone      = new(Color.FromRgb(0x43, 0xA0, 0x47));
    private static readonly SolidColorBrush BrushPending   = new(Color.FromRgb(0x3D, 0x8B, 0xE8));
    private static readonly SolidColorBrush BrushTextOn    = Brushes.White;
    private static readonly SolidColorBrush BrushTextOff   = new(Color.FromRgb(0xB0, 0xBE, 0xC5));
    private static readonly SolidColorBrush BrushNumActive = new(Color.FromRgb(0x15, 0x65, 0xC0));

    public MainWindow()
    {
        InitializeComponent();
        _welcome  = new WelcomePage();
        _config2  = new ConfigPage();
        _install  = new InstallPage();
        _finish   = new FinishPage();
        _pages    = [_welcome, _config2, _install, _finish];
        GoTo(0);
    }

    // ── Navigation ────────────────────────────────────────────────────────────

    private async void NextButton_Click(object sender, RoutedEventArgs e)
    {
        switch (_step)
        {
            case 0:
                GoTo(1);
                _config2.Initialize(_config);
                break;

            case 1:
                if (!_config2.Validate(out string err))
                {
                    MessageBox.Show(err, "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                _config2.Commit(_config);
                GoTo(2);
                await _install.RunInstallAsync(_config);
                NextButton.IsEnabled = true;
                break;

            case 2:
                _finish.SetResult(_install.Success, _install.ServiceStarted);
                GoTo(3);
                break;

            case 3:
                Application.Current.Shutdown();
                break;
        }
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (_step > 0) GoTo(_step - 1);
    }

    private void GoTo(int step)
    {
        _step = step;
        PageContent.Content = _pages[step];
        RefreshSidebar();
        RefreshButtons();
    }

    // ── Sidebar ───────────────────────────────────────────────────────────────

    private void RefreshSidebar()
    {
        var dots   = StepDots;
        var labels = StepLabels;
        var texts  = StepTexts;

        for (int i = 0; i < 4; i++)
        {
            if (i < _step)
            {
                dots[i].Background   = BrushDone;
                labels[i].Text       = "✓";
                labels[i].Foreground = BrushTextOn;
                texts[i].Foreground  = BrushTextOn;
                texts[i].FontWeight  = FontWeights.Normal;
            }
            else if (i == _step)
            {
                dots[i].Background   = BrushActive;
                labels[i].Text       = (i + 1).ToString();
                labels[i].Foreground = BrushNumActive;
                texts[i].Foreground  = BrushTextOn;
                texts[i].FontWeight  = FontWeights.SemiBold;
            }
            else
            {
                dots[i].Background   = BrushPending;
                labels[i].Text       = (i + 1).ToString();
                labels[i].Foreground = BrushTextOff;
                texts[i].Foreground  = BrushTextOff;
                texts[i].FontWeight  = FontWeights.Normal;
            }
        }
    }

    // ── Buttons ───────────────────────────────────────────────────────────────

    private void RefreshButtons()
    {
        switch (_step)
        {
            case 0:
                BackButton.Visibility = Visibility.Collapsed;
                NextButton.Content    = "Next →";
                NextButton.IsEnabled  = true;
                break;
            case 1:
                BackButton.Visibility = Visibility.Visible;
                NextButton.Content    = "Install";
                NextButton.IsEnabled  = true;
                break;
            case 2:
                BackButton.Visibility = Visibility.Collapsed;
                NextButton.Content    = "Next →";
                NextButton.IsEnabled  = false; // enabled after install finishes
                break;
            case 3:
                BackButton.Visibility = Visibility.Collapsed;
                NextButton.Content    = "Finish";
                NextButton.IsEnabled  = true;
                break;
        }
    }
}
