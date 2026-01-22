using Renci.SshNet;
using WebdoctorV.Models;

namespace WebdoctorV.Checkers;

public class SshChecker : IChecker
{
  public bool Supports(string protocol) => protocol.Equals("ssh", StringComparison.OrdinalIgnoreCase);

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
      var timeout = service.Timeout ?? TimeSpan.FromSeconds(30);

      using var client = new SshClient(service.Host, service.Port, "root", "");
      client.ConnectionInfo.Timeout = timeout;

      await Task.Run(() => client.Connect());

      if (!string.IsNullOrEmpty(service.Command))
      {
        var command = client.RunCommand(service.Command);
        var exitCode = command.ExitStatus;

        result.Duration = DateTime.UtcNow - startTime;
        result.LastCheck = DateTime.UtcNow;

        if (service.Response?.ExitCode.HasValue == true)
        {
          result.Status = exitCode == service.Response.ExitCode.Value
              ? CheckStatus.Alive
              : CheckStatus.Dead;
        }
        else
        {
          result.Status = exitCode == 0 ? CheckStatus.Alive : CheckStatus.Dead;
        }

        if (result.Status == CheckStatus.Dead)
        {
          result.Error = $"Exit code: {exitCode}, Error: {command.Error}";
        }
      }
      else
      {
        result.Duration = DateTime.UtcNow - startTime;
        result.LastCheck = DateTime.UtcNow;
        result.Status = CheckStatus.Alive;
      }

      client.Disconnect();
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
