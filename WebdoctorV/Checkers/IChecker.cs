using WebdoctorV.Models;

namespace WebdoctorV.Checkers;

public interface IChecker
{
  Task<CheckResult> CheckAsync(ServiceConfig service, string path);
  bool Supports(string protocol);
}
