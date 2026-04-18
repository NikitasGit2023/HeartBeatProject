using HeartBeatProjectInstaller.Models;
using HeartBeatProjectInstaller.Services;
using System.Diagnostics;
using System.IO;
using System.Windows.Controls;

namespace HeartBeatProjectInstaller.Pages;

public partial class InstallPage : UserControl
{
    public bool    Success       { get; private set; }
    public bool    ServiceStarted { get; private set; }
    public string? ExePath        { get; private set; }

    public InstallPage() => InitializeComponent();

    public async Task RunInstallAsync(InstallerConfig config)
    {
        Success        = false;
        ServiceStarted = false;
        var svc = new InstallerService();
        const string ExeName = "HeartBeatProject.server.exe";

        void SetStatus(int percent, string msg) => Dispatcher.Invoke(() =>
        {
            ProgressBar.Value = percent;
            StatusText.Text   = msg;
        });

        void Log(string msg) => Dispatcher.Invoke(() =>
        {
            LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}{Environment.NewLine}");
            LogBox.ScrollToEnd();
        });

        SetStatus(0, "Starting…");
        await Task.Delay(200);

        // 1 — Find executable
        Log($"Looking for {ExeName}…");
        var exePath = FindExecutable(AppContext.BaseDirectory, ExeName, Log);
        if (exePath is null)
        {
            Log($"ERROR: {ExeName} was not found anywhere under:{Environment.NewLine}  {AppContext.BaseDirectory}");
            SetStatus(0, "Failed — executable not found.");
            return;
        }
        ExePath = exePath;
        Log($"Found: {exePath}");
        SetStatus(40, "Updating appsettings.json…");

        // 2 — Patch appsettings.json next to the exe, and any parent-level one (dev mode)
        try
        {
            var exeDir = Path.GetDirectoryName(exePath)!;
            await Task.Run(() => svc.WriteAppSettings(config, exeDir));
            Log($"appsettings.json updated in:{Environment.NewLine}  {exeDir}");

            // Walk up to find a second appsettings.json (project source dir when running dev build)
            var dir = new DirectoryInfo(exeDir).Parent;
            for (int i = 0; i < 4 && dir is not null; i++, dir = dir.Parent)
            {
                var candidate = Path.Combine(dir.FullName, "appsettings.json");
                if (File.Exists(candidate))
                {
                    await Task.Run(() => svc.WriteAppSettings(config, dir.FullName));
                    Log($"Also updated:{Environment.NewLine}  {candidate}");
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Log($"ERROR updating appsettings.json: {ex.Message}");
            SetStatus(40, "Failed.");
            return;
        }

        // 3 — Create heartbeat folder before the service starts
        if (!string.IsNullOrWhiteSpace(config.FolderPath))
        {
            try
            {
                Directory.CreateDirectory(config.FolderPath);
                Log($"Heartbeat folder ready: {config.FolderPath}");
            }
            catch (Exception ex)
            {
                Log($"WARNING: could not create heartbeat folder: {ex.Message}");
            }
        }

        // 4 — Install / reinstall the Windows service
        SetStatus(75, "Installing Windows service…");
        const string ServiceName = "HeartbeatService";
        try
        {
            if (svc.ServiceExists(ServiceName))
            {
                Log($"Existing service found — stopping and removing…");
                svc.Stop(ServiceName);
                await Task.Delay(1500);
                var del = svc.Delete(ServiceName);
                if (!del.Success)
                    Log($"WARNING: could not remove old service:{Environment.NewLine}  {del.Output}");
                await Task.Delay(500);
            }

            var create = svc.Create(ServiceName, exePath);
            if (!create.Success)
            {
                Log($"ERROR: failed to create service:{Environment.NewLine}  {create.Output}");
                SetStatus(75, "Failed — could not install service.");
                return;
            }
            Log($"Service '{ServiceName}' installed.");

            var start = svc.Start(ServiceName);
            if (start.Success)
            {
                ServiceStarted = true;
                Log($"Service '{ServiceName}' started successfully.");
            }
            else
            {
                Log($"WARNING: service installed but could not start:{Environment.NewLine}  {start.Output}");
            }
        }
        catch (Exception ex)
        {
            Log($"ERROR during service installation: {ex.Message}");
            SetStatus(75, "Failed.");
            return;
        }

        SetStatus(100, "Installation complete!");
        Success = true;
    }

    // Walk up the directory tree from baseDir (up to 6 levels), searching each
    // level's subtree for the exe. Finds the exe both in production (installer
    // sits next to a HeartBeatProject/ folder) and in development (installer runs
    // from its bin/Debug directory, several levels away from the server output).
    private static string? FindExecutable(string baseDir, string exeName, Action<string> log)
    {
        var dir = new DirectoryInfo(baseDir);
        for (int level = 0; level < 6 && dir is not null; level++, dir = dir.Parent)
        {
            log($"Searching (level {level}): {dir.FullName}");
            try
            {
                var hit = dir.EnumerateFiles(exeName, SearchOption.AllDirectories).FirstOrDefault();
                if (hit is not null)
                    return hit.FullName;
            }
            catch (Exception) { }
        }
        return null;
    }
}
