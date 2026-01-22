using Microsoft.AspNetCore.Mvc;
using WebdoctorV.Models;
using WebdoctorV.Services;

namespace WebdoctorV.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
  private readonly HealthCheckService _healthCheckService;
  private readonly ILogger<HealthController> _logger;

  public HealthController(
      HealthCheckService healthCheckService,
      ILogger<HealthController> logger)
  {
    _healthCheckService = healthCheckService;
    _logger = logger;
  }

  /// <summary>
  /// Get health status of WebdoctorV itself
  /// </summary>
  [HttpGet]
  public IActionResult GetHealth()
  {
    var results = _healthCheckService.GetAllResults().ToList();
    var total = results.Count;
    var alive = results.Count(r => r.Status == CheckStatus.Alive);
    var dead = results.Count(r => r.Status == CheckStatus.Dead);
    var pending = results.Count(r => r.Status == CheckStatus.Pending);

    var overallStatus = total == 0
        ? "no_services"
        : dead > 0
            ? "degraded"
            : pending > 0
                ? "checking"
                : "healthy";

    return Ok(new
    {
      status = overallStatus,
      timestamp = DateTime.UtcNow,
      services = new
      {
        total,
        alive,
        dead,
        pending
      }
    });
  }

  /// <summary>
  /// Get all service check results
  /// </summary>
  [HttpGet("services")]
  public IActionResult GetAllServices()
  {
    var results = _healthCheckService.GetAllResults()
        .Select(r => new
        {
          serviceId = r.ServiceId,
          path = r.Path,
          fullHttpPath = r.FullHttpPath,
          protocol = r.Protocol,
          name = r.Name,
          host = r.Host,
          port = r.Port,
          additionalInfo = r.AdditionalInfo,
          status = r.Status.ToString().ToLower(),
          duration = r.Duration?.TotalMilliseconds,
          lastCheck = r.LastCheck,
          error = r.Error
        })
        .OrderBy(r => r.serviceId)
        .ToList();

    return Ok(new
    {
      count = results.Count,
      services = results
    });
  }

  /// <summary>
  /// Get specific service check result by path
  /// </summary>
  [HttpGet("services/{*path}")]
  public IActionResult GetService(string path)
  {
    var result = _healthCheckService.GetResult(path);

    if (result == null)
    {
      return NotFound(new
      {
        error = "Service not found",
        path
      });
    }

    return Ok(new
    {
      serviceId = result.ServiceId,
      path = result.Path,
      fullHttpPath = result.FullHttpPath,
      protocol = result.Protocol,
      name = result.Name,
      host = result.Host,
      port = result.Port,
      status = result.Status.ToString().ToLower(),
      duration = result.Duration?.TotalMilliseconds,
      lastCheck = result.LastCheck,
      error = result.Error
    });
  }

  /// <summary>
  /// Get summary statistics
  /// </summary>
  [HttpGet("summary")]
  public IActionResult GetSummary()
  {
    var results = _healthCheckService.GetAllResults().ToList();
    var total = results.Count;

    if (total == 0)
    {
      return Ok(new
      {
        total = 0,
        message = "No services configured"
      });
    }

    var alive = results.Count(r => r.Status == CheckStatus.Alive);
    var dead = results.Count(r => r.Status == CheckStatus.Dead);
    var pending = results.Count(r => r.Status == CheckStatus.Pending);

    var avgDuration = results
        .Where(r => r.Duration.HasValue)
        .Select(r => r.Duration!.Value.TotalMilliseconds)
        .DefaultIfEmpty(0)
        .Average();

    var uptimePercent = total > 0
        ? (double)alive / total * 100
        : 0;

    return Ok(new
    {
      total,
      alive,
      dead,
      pending,
      uptimePercent = Math.Round(uptimePercent, 2),
      averageDurationMs = Math.Round(avgDuration, 2),
      lastUpdate = results
            .Where(r => r.LastCheck.HasValue)
            .Select(r => r.LastCheck!.Value)
            .DefaultIfEmpty(DateTime.MinValue)
            .Max()
    });
  }
}
