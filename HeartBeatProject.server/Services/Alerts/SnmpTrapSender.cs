using System.Net;
using HeartBeatProject.server.Configuration;
using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Messaging;
using Microsoft.Extensions.Options;

namespace HeartBeatProject.server.Services.Alerts;

public sealed class SnmpTrapSender : ISnmpTrapSender
{
    // Private enterprise OID root — replace with your IANA-assigned number in production.
    private const string EnterpriseOid    = "1.3.6.1.4.1.99999";
    private const string TrapOid          = EnterpriseOid + ".0.1";   // heartbeat alert trap
    private const string OidSubject       = EnterpriseOid + ".1.1.0"; // varbind: subject
    private const string OidMessage       = EnterpriseOid + ".1.2.0"; // varbind: message

    private readonly ILogger<SnmpTrapSender> _logger;
    private readonly AlertOptions _options;

    public SnmpTrapSender(IOptions<AlertOptions> options, ILogger<SnmpTrapSender> logger)
    {
        _options = options.Value;
        _logger  = logger;
    }

    private bool IsConfigured =>
        _options.EnableSnmp &&
        !string.IsNullOrWhiteSpace(_options.SnmpHost) &&
        _options.SnmpPort > 0;

    public async Task SendTrapAsync(string subject, string message, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            _logger.LogWarning("SNMP trap skipped: incomplete SNMP configuration.");
            return;
        }

        try
        {
            var addresses = await Dns.GetHostAddressesAsync(_options.SnmpHost, cancellationToken);
            var ip        = addresses.FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                            ?? addresses.First();
            var endpoint  = new IPEndPoint(ip, _options.SnmpPort);

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
                    new OctetString(_options.Community),
                    new ObjectIdentifier(TrapOid),
                    uptimeCentiseconds,
                    varbinds),
                cancellationToken);

            _logger.LogInformation("SNMP trap sent to {Host}:{Port}. Subject: {Subject}",
                _options.SnmpHost, _options.SnmpPort, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send SNMP trap to {Host}:{Port}.",
                _options.SnmpHost, _options.SnmpPort);
        }
    }
}
