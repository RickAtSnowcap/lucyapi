using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;

namespace LucyAPI.Services.Utilities;

/// <summary>
/// HTTP handler for fetching URLs supplied by callers (SSRF guard): the address is checked on the
/// ACTUAL socket being opened, so DNS tricks and redirects can't reach loopback, private, link-local,
/// CGNAT, multicast or reserved addresses — only the public internet.
/// </summary>
public static class PublicHttp
{
    public static SocketsHttpHandler CreateHandler(bool allowRedirects, TimeSpan connectTimeout) => new()
    {
        AllowAutoRedirect = allowRedirects,
        MaxAutomaticRedirections = 3,
        ConnectTimeout = connectTimeout,
        ConnectCallback = async (context, ct) =>
        {
            var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, ct);
            var target = addresses.FirstOrDefault(a => !IsPrivate(a))
                ?? throw new HttpRequestException("host resolves only to non-public addresses");
            var socket = new Socket(target.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(new IPEndPoint(target, context.DnsEndPoint.Port), ct);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }
    };

    public static bool IsPrivate(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        if (IPAddress.IsLoopback(ip)) return true;
        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
            return ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6UniqueLocal || ip.Equals(IPAddress.IPv6None);
        var b = ip.GetAddressBytes();
        return b[0] switch
        {
            0 or 10 or 127 => true,
            100 => b[1] >= 64 && b[1] <= 127,             // CGNAT 100.64/10
            169 => b[1] == 254,                           // link-local
            172 => b[1] >= 16 && b[1] <= 31,
            192 => b[1] == 168 || (b[1] == 0 && b[2] == 0),
            _ => b[0] >= 224                              // multicast / reserved
        };
    }
}
