namespace WebdoctorV.Models;

public class Config
{
  public TimeSpan? Interval { get; set; }
  public List<ServiceConfig> Services { get; set; } = new();
}

public class ServiceConfig
{
  public string Id { get; set; } = string.Empty;
  public string? Name { get; set; }
  public string Protocol { get; set; } = string.Empty;
  public string Host { get; set; } = string.Empty;
  public int Port { get; set; }
  public TimeSpan? Timeout { get; set; }
  public TimeSpan? Interval { get; set; }
  public ResponseConfig? Response { get; set; }

  // Retry configuration
  public int RetryCount { get; set; } = 0; // Number of retries before marking as dead
  public TimeSpan? RetryDelay { get; set; } // Delay between retries

  // HTTP/HTTPS specific
  public string? Method { get; set; }
  public string? Path { get; set; }
  public bool PathIsPrefix { get; set; }
  public List<ServiceConfig> Children { get; set; } = new();
  public List<ExcludeConfig> Excludes { get; set; } = new();

  // MySQL/PostgreSQL specific
  public string? Query { get; set; }
  public string? Username { get; set; }
  public string? Password { get; set; }
  public string? Database { get; set; }

  // SSH specific
  public string? Command { get; set; }
}

public class ResponseConfig
{
  // HTTP
  public int? Status { get; set; }

  // HTTP body validation
  public string? BodyContains { get; set; } // Check if response body contains this string
  public string? BodyRegex { get; set; } // Validate response body against regex pattern
  public string? BodyJsonPath { get; set; } // JSON path expression (future: requires JSON path library)

  // MySQL/PostgreSQL
  public int? Rows { get; set; }
  public int? Columns { get; set; }
  public List<Dictionary<string, object>>? Data { get; set; }

  // SSH
  public int? ExitCode { get; set; }
}

public class ExcludeConfig
{
  public string? Path { get; set; }
  public bool PathIsPrefix { get; set; }
}
