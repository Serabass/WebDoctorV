namespace WebdoctorV.Models;

public class CheckResult
{
  public string ServiceId { get; set; } = string.Empty;
  public string Path { get; set; } = string.Empty;
  public string FullHttpPath { get; set; } = string.Empty;
  public string Protocol { get; set; } = string.Empty;
  public string Name { get; set; } = string.Empty;
  public string Host { get; set; } = string.Empty;
  public int Port { get; set; }
  public string? AdditionalInfo { get; set; } // Query for MySQL/PostgreSQL, Command for SSH, etc.
  public CheckStatus Status { get; set; } = CheckStatus.Pending;
  public TimeSpan? Duration { get; set; }
  public DateTime? LastCheck { get; set; }
  public string? Error { get; set; }
}

public enum CheckStatus
{
  Pending = -1,
  Dead = 0,
  Alive = 1
}
