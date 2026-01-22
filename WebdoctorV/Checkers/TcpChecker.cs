using System.Net.Sockets;
using WebdoctorV.Models;

namespace WebdoctorV.Checkers;

public class TcpChecker : IChecker
{
  public bool Supports(string protocol) => protocol.Equals("tcp", StringComparison.OrdinalIgnoreCase);

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
      using var client = new TcpClient();
      var timeout = service.Timeout ?? TimeSpan.FromSeconds(30);

      var connectTask = client.ConnectAsync(service.Host, service.Port);
      var timeoutTask = Task.Delay(timeout);

      var completedTask = await Task.WhenAny(connectTask, timeoutTask);

      if (completedTask == timeoutTask)
      {
        result.Duration = DateTime.UtcNow - startTime;
        result.LastCheck = DateTime.UtcNow;
        result.Status = CheckStatus.Dead;
        result.Error = "Connection timeout";
      }
      else
      {
        result.Duration = DateTime.UtcNow - startTime;
        result.LastCheck = DateTime.UtcNow;
        result.Status = client.Connected ? CheckStatus.Alive : CheckStatus.Dead;
      }
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
