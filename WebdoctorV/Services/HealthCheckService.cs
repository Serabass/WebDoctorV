using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Prometheus;
using WebdoctorV.Checkers;
using WebdoctorV.Hubs;
using WebdoctorV.Models;

namespace WebdoctorV.Services;

public class HealthCheckService : IHostedService
{
  private static readonly Gauge ItemStatus = Metrics
      .CreateGauge("webdoctor_item_status", "Status of the item (-1 = pending, 0 = dead, 1 = alive)", ["proto", "name", "id", "path"]);

  private static readonly Gauge ItemLastDuration = Metrics
      .CreateGauge("webdoctor_item_last_duration", "Last duration of the item", ["proto", "name", "id", "path"]);

  private static readonly Gauge ItemLastCheckDate = Metrics
      .CreateGauge("webdoctor_item_last_check_date", "Last check date of the item", ["proto", "name", "id", "path"]);

  private static readonly Gauge ExecutableItemCount = Metrics
      .CreateGauge("webdoctor_executable_item_count", "Number of items");

  private static readonly Counter CheckTotal = Metrics
      .CreateCounter("webdoctor_checks_total", "Total number of checks performed", ["proto", "name", "id", "path", "status"]);

  private static readonly Gauge ServiceUptimePercent = Metrics
      .CreateGauge("webdoctor_service_uptime_percent", "Uptime percentage for service", ["proto", "name", "id", "path"]);

  private readonly ILogger<HealthCheckService> _logger;
  private readonly List<IChecker> _checkers;
  private readonly Config _config;
  private readonly ConcurrentDictionary<string, CheckResult> _results = new();
  private readonly ConcurrentDictionary<string, (int total, int alive)> _serviceStats = new();
  private readonly IHubContext<HealthCheckHub>? _hubContext;
  private Timer? _timer;

  public HealthCheckService(
      ILogger<HealthCheckService> logger,
      IEnumerable<IChecker> checkers,
      Config config,
      IHubContext<HealthCheckHub>? hubContext = null)
  {
    _logger = logger;
    _checkers = checkers.ToList();
    _config = config;
    _hubContext = hubContext;
  }

  public Task StartAsync(CancellationToken cancellationToken)
  {
    _logger.LogInformation("Starting health check service");

    var interval = _config.Interval ?? TimeSpan.FromSeconds(60);
    _timer = new Timer(DoWork, null, TimeSpan.Zero, interval);

    return Task.CompletedTask;
  }

  public Task StopAsync(CancellationToken cancellationToken)
  {
    _logger.LogInformation("Stopping health check service");
    _timer?.Dispose();
    return Task.CompletedTask;
  }

  private async void DoWork(object? state)
  {
    try
    {
      await PerformChecks();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error performing health checks");
    }
  }

  private async Task PerformChecks()
  {
    var tasks = new List<Task>();

    foreach (var service in _config.Services)
    {
      tasks.Add(CheckService(service, service.Id, null, new List<ServiceConfig>()));
      tasks.AddRange(CheckServiceChildren(service, service.Id, new List<ServiceConfig>()));
    }

    await Task.WhenAll(tasks);

    // Send summary update after all checks complete
    await SendSummaryUpdate();
  }

  private List<Task> CheckServiceChildren(ServiceConfig service, string parentPath, List<ServiceConfig> parentChain)
  {
    var tasks = new List<Task>();

    foreach (var child in service.Children)
    {
      // If child.Id contains a dot, it's an executable block with already formed path
      // Otherwise, form path as parent.child
      var childPath = child.Id.Contains('.') ? child.Id : $"{parentPath}.{child.Id}";
      var childParentChain = new List<ServiceConfig>(parentChain) { service };
      tasks.Add(CheckService(child, childPath, service, childParentChain));

      // Recursively check children
      tasks.AddRange(CheckServiceChildren(child, childPath, childParentChain));
    }

    return tasks;
  }

  private string BuildFullHttpPath(ServiceConfig service, List<ServiceConfig> parentChain)
  {
    var pathParts = new List<string>();

    // Add paths from parent chain (these are intermediate paths that form the full route)
    // Skip the root service if it has children (it's a container, not part of the path)
    var startIndex = 0;
    if (parentChain.Count > 0 && parentChain[0].Children.Count > 0)
    {
      // Root service has children, so it's a container - skip its path
      startIndex = 1;
    }

    for (int i = startIndex; i < parentChain.Count; i++)
    {
      var parent = parentChain[i];
      if (!string.IsNullOrEmpty(parent.Path))
      {
        var parentPath = parent.Path.TrimStart('/').TrimEnd('/');
        if (!string.IsNullOrEmpty(parentPath))
        {
          pathParts.Add(parentPath);
        }
      }
    }

    // Add current service path
    if (!string.IsNullOrEmpty(service.Path))
    {
      var currentPath = service.Path.TrimStart('/').TrimEnd('/');
      if (!string.IsNullOrEmpty(currentPath))
      {
        pathParts.Add(currentPath);
      }
    }

    // Build full path
    if (pathParts.Count == 0)
    {
      return service.Path ?? "/";
    }

    var fullPath = "/" + string.Join("/", pathParts);
    return fullPath;
  }

  private async Task CheckService(ServiceConfig service, string path, ServiceConfig? parent = null, List<ServiceConfig>? parentChain = null)
  {
    // Check excludes
    if (parent != null)
    {
      foreach (var exclude in parent.Excludes)
      {
        if (exclude.Path != null)
        {
          if (exclude.PathIsPrefix && service.Path?.StartsWith(exclude.Path) == true)
            return;
          if (!exclude.PathIsPrefix && service.Path == exclude.Path)
            return;
        }
      }
    }

    var checker = _checkers.FirstOrDefault(c => c.Supports(service.Protocol));
    if (checker == null)
    {
      _logger.LogWarning("No checker found for protocol: {Protocol}", service.Protocol);
      return;
    }

    var fullHttpPath = BuildFullHttpPath(service, parentChain ?? new List<ServiceConfig>());

    // Retry logic
    CheckResult? result = null;
    var retryCount = service.RetryCount;
    var retryDelay = service.RetryDelay ?? TimeSpan.FromSeconds(1);

    for (int attempt = 0; attempt <= retryCount; attempt++)
    {
      if (attempt > 0)
      {
        _logger.LogDebug("Retry attempt {Attempt} for {Path}", attempt, path);
        await Task.Delay(retryDelay);
      }

      result = await checker.CheckAsync(service, path);
      result.FullHttpPath = fullHttpPath;
      result.Host = service.Host;
      result.Port = service.Port;

      // Set additional info based on protocol
      if (service.Protocol.Equals("mysql", StringComparison.OrdinalIgnoreCase) ||
          service.Protocol.Equals("postgresql", StringComparison.OrdinalIgnoreCase))
      {
        result.AdditionalInfo = service.Query;
      }
      else if (service.Protocol.Equals("ssh", StringComparison.OrdinalIgnoreCase))
      {
        result.AdditionalInfo = service.Command;
      }

      // If successful or no more retries, break
      if (result.Status == CheckStatus.Alive || attempt >= retryCount)
      {
        break;
      }
    }

    if (result == null)
    {
      _logger.LogError("Failed to get check result for {Path}", path);
      return;
    }

    _results[path] = result;

    // Update Prometheus metrics - use both id (config path) and path (full HTTP path)
    var labels = new[] { result.Protocol, result.Name, result.Path, result.FullHttpPath };
    ItemStatus.WithLabels(labels).Set((int)result.Status);

    if (result.Duration.HasValue)
    {
      ItemLastDuration.WithLabels(labels).Set(result.Duration.Value.TotalMilliseconds);
    }

    if (result.LastCheck.HasValue)
    {
      ItemLastCheckDate.WithLabels(labels).Set(
          new DateTimeOffset(result.LastCheck.Value).ToUnixTimeMilliseconds()
      );
    }

    // Update statistics
    var stats = _serviceStats.GetOrAdd(path, _ => (0, 0));
    var newTotal = stats.total + 1;
    var newAlive = stats.alive + (result.Status == CheckStatus.Alive ? 1 : 0);
    _serviceStats[path] = (newTotal, newAlive);

    // Update counters
    var statusLabel = result.Status.ToString().ToLower();
    var counterLabels = new[] { result.Protocol, result.Name, result.Path, result.FullHttpPath, statusLabel };
    CheckTotal.WithLabels(counterLabels).Inc();

    // Update uptime percentage
    if (newTotal > 0)
    {
      var uptimePercent = (double)newAlive / newTotal * 100;
      ServiceUptimePercent.WithLabels(labels).Set(uptimePercent);
    }

    ExecutableItemCount.Set(_results.Count);

    // Send update via SignalR
    if (_hubContext != null)
    {
      try
      {
        await _hubContext.Clients.All.SendAsync("StatusUpdate", new
        {
          serviceId = result.ServiceId,
          path = result.Path,
          fullHttpPath = result.FullHttpPath,
          protocol = result.Protocol,
          name = result.Name,
          host = result.Host,
          port = result.Port,
          additionalInfo = result.AdditionalInfo,
          status = result.Status.ToString().ToLower(),
          duration = result.Duration?.TotalMilliseconds,
          lastCheck = result.LastCheck,
          error = result.Error
        });
      }
      catch (Exception ex)
      {
        _logger.LogWarning(ex, "Failed to send SignalR update for {Path}", path);
      }
    }

    _logger.LogInformation(
        "Check completed: {Path} - {Status} ({Duration}ms)",
        path,
        result.Status,
        result.Duration?.TotalMilliseconds ?? 0
    );
  }

  private async Task SendSummaryUpdate()
  {
    if (_hubContext == null) return;

    try
    {
      var results = _results.Values.ToList();
      var total = results.Count;
      if (total == 0) return;

      var alive = results.Count(r => r.Status == CheckStatus.Alive);
      var dead = results.Count(r => r.Status == CheckStatus.Dead);
      var pending = results.Count(r => r.Status == CheckStatus.Pending);

      var avgDuration = results
          .Where(r => r.Duration.HasValue)
          .Select(r => r.Duration!.Value.TotalMilliseconds)
          .DefaultIfEmpty(0)
          .Average();

      var uptimePercent = (double)alive / total * 100;

      await _hubContext.Clients.All.SendAsync("Summary", new
      {
        total,
        alive,
        dead,
        pending,
        uptimePercent = Math.Round(uptimePercent, 2),
        averageDurationMs = Math.Round(avgDuration, 2),
        timestamp = DateTime.UtcNow
      });
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Failed to send summary update via SignalR");
    }
  }

  public CheckResult? GetResult(string path)
  {
    return _results.TryGetValue(path, out var result) ? result : null;
  }

  public IEnumerable<CheckResult> GetAllResults()
  {
    return _results.Values;
  }

  public int GetExecutableItemCount()
  {
    return _results.Count;
  }
}
