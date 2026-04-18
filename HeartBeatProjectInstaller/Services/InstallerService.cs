using HeartBeatProjectInstaller.Models;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace HeartBeatProjectInstaller.Services;

public class InstallerService
{
    private readonly string _scPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.System), "sc.exe");

    public void WriteAppSettings(InstallerConfig config, string targetDir)
    {
        var path = Path.Combine(targetDir, "appsettings.json");

        var root = File.Exists(path)
            ? JsonNode.Parse(File.ReadAllText(path))?.AsObject() ?? new JsonObject()
            : new JsonObject();

        if (root["Heartbeat"] is not JsonObject hb)
        {
            hb = new JsonObject();
            root["Heartbeat"] = hb;
        }
        hb["Mode"]                 = config.Mode;
        hb["FolderPath"]           = config.FolderPath;
        hb["IntervalSeconds"]      = config.IntervalSeconds;
        hb["CheckIntervalSeconds"] = config.CheckIntervalSeconds;
        hb["ThresholdSeconds"]     = config.ThresholdSeconds;

        if (root["Alerts"] is not JsonObject alerts)
        {
            alerts = new JsonObject();
            root["Alerts"] = alerts;
        }
        alerts["EnableEmail"] = config.EnableEmail;
        alerts["SmtpServer"]  = config.SmtpServer;
        alerts["Port"]        = config.SmtpPort;
        alerts["From"]        = config.From;
        alerts["To"]          = config.To;

        File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    public bool ServiceExists(string name)
        => Run(_scPath, $"query \"{name}\"").ExitCode == 0;

    public CommandResult Stop(string name)   => Run(_scPath, $"stop \"{name}\"");
    public CommandResult Delete(string name) => Run(_scPath, $"delete \"{name}\"");
    public CommandResult Start(string name)  => Run(_scPath, $"start \"{name}\"");

    public CommandResult Create(string name, string exePath)
        => Run(_scPath, $"create \"{name}\" binPath= \"{exePath}\" start= auto DisplayName= \"Heartbeat Monitoring\"");

    private static CommandResult Run(string fileName, string args)
    {
        var psi = new ProcessStartInfo(fileName, args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true
        };
        using var proc = Process.Start(psi)!;
        // Read both streams concurrently before WaitForExit to prevent deadlock:
        // if stderr fills its buffer while we're blocked on stdout, both sides freeze.
        var stdoutTask = proc.StandardOutput.ReadToEndAsync();
        var stderrTask = proc.StandardError.ReadToEndAsync();
        proc.WaitForExit();
        return new CommandResult(proc.ExitCode, stdoutTask.Result + stderrTask.Result);
    }
}

public record CommandResult(int ExitCode, string Output)
{
    public bool Success => ExitCode == 0;
}
