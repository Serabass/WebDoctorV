using System.Net;
using System.Net.Sockets;
using WebdoctorV.Models;

namespace WebdoctorV.Checkers;

public class UdpChecker : IChecker
{
  public bool Supports(string protocol) => protocol.Equals("udp", StringComparison.OrdinalIgnoreCase);

  public async Task<CheckResult> CheckAsync(ServiceConfig service, string path)
  {
    var result = new CheckResult
    {
      ServiceId = service.Id,
      Path = path,
      Protocol = service.Protocol,
      Name = service.Name ?? service.Id,
      Status = CheckStatus.Pending
    };

    var startTime = DateTime.UtcNow;

    try
    {
      using var client = new UdpClient();
      var endpoint = new IPEndPoint(IPAddress.Parse(service.Host), service.Port);

      // UDP is connectionless, so we just try to send/receive
      var timeout = service.Timeout ?? TimeSpan.FromSeconds(30);
      client.Client.ReceiveTimeout = (int)timeout.TotalMilliseconds;

      // Try to send empty packet
      await client.SendAsync(Array.Empty<byte>(), 0, endpoint);

      result.Duration = DateTime.UtcNow - startTime;
      result.LastCheck = DateTime.UtcNow;
      result.Status = CheckStatus.Alive; // UDP doesn't guarantee response
    }
    catch (Exception ex)
    {
      result.Duration = DateTime.UtcNow - startTime;
      result.LastCheck = DateTime.UtcNow;
      result.Status = CheckStatus.Dead;
      result.Error = ex.Message;
    }

    return result;
  }
}
