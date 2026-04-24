using System.Net;
using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Messaging;

namespace HeartBeatProject.Server.Services.Alerts;

public sealed class SnmpTrapSender : ISnmpTrapSender
{
    // Private enterprise OID root — replace with your IANA-assigned number in production.
    private const string EnterpriseOid = "1.3.6.1.4.1.99999";
    private const string TrapOid       = EnterpriseOid + ".0.1";   // heartbeat alert trap
    private const string OidSubject    = EnterpriseOid + ".1.1.0"; // varbind: subject
    private const string OidMessage    = EnterpriseOid + ".1.2.0"; // varbind: message

    private readonly ILogger<SnmpTrapSender> _logger;
    private readonly RuntimeSettingsStore _settingsStore;

    public SnmpTrapSender(RuntimeSettingsStore settingsStore, ILogger<SnmpTrapSender> logger)
    {
        _settingsStore = settingsStore;
        _logger        = logger;
    }

    public async Task SendTrapAsync(string subject, string message, CancellationToken cancellationToken = default)
    {
        var opts = _settingsStore.Get();

        if (!opts.EnableSnmp || string.IsNullOrWhiteSpace(opts.SnmpHost) || opts.SnmpPort <= 0)
        {
            _logger.LogWarning("SNMP trap skipped: incomplete SNMP configuration.");
            return;
        }

        try
        {
            var addresses = await Dns.GetHostAddressesAsync(opts.SnmpHost, cancellationToken);
            var ip        = addresses.FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                            ?? addresses.First();
            var endpoint  = new IPEndPoint(ip, opts.SnmpPort);

            var uptimeCentiseconds = (uint)(Environment.TickCount64 / 10);

            var varbinds = new List<Variable>
            {
                new(new ObjectIdentifier(OidSubject), new OctetString(subject)),
                new(new ObjectIdentifier(OidMessage),  new OctetString(message))
            };

            await Task.Run(() =>
                Messenger.SendTrapV2(
                    0,
                    VersionCode.V2,
                    endpoint,
                    new OctetString(opts.Community),
                    new ObjectIdentifier(TrapOid),
                    uptimeCentiseconds,
                    varbinds),
                cancellationToken);

            _logger.LogInformation("SNMP trap sent to {Host}:{Port}. Subject: {Subject}",
                opts.SnmpHost, opts.SnmpPort, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send SNMP trap to {Host}:{Port}.",
                _settingsStore.Get().SnmpHost, _settingsStore.Get().SnmpPort);
        }
    }
}
