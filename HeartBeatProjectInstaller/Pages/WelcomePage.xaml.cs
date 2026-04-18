using HeartBeatProjectInstaller.Models;
using System.Windows.Controls;

namespace HeartBeatProjectInstaller.Pages;

public partial class WelcomePage : UserControl
{
    public WelcomePage() => InitializeComponent();

    public bool Validate(out string error) { error = ""; return true; }
    public void Initialize(InstallerConfig config) { }
    public void Commit(InstallerConfig config) { }
}
