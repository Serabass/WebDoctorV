using System;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using WebdoctorV.Models;
using WebdoctorV.Services;

namespace WebdoctorV.Hubs;

public class HealthCheckHub : Hub
{
  private readonly IServiceProvider _serviceProvider;

  public HealthCheckHub(IServiceProvider serviceProvider)
  {
    _serviceProvider = serviceProvider;
  }
 
  public override async Task OnConnectedAsync()
  {
    // Send all current statuses to newly connected client
    var healthCheckService = _serviceProvider.GetRequiredService<HealthCheckService>();
    var results = healthCheckService.GetAllResults().ToList();
    if (results.Any())
    {
      var services = results.Select(r => new
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
      }).ToList();

      await Clients.Caller.SendAsync("AllStatuses", new
      {
        count = services.Count,
        services
      });
    }

    await base.OnConnectedAsync();
  }
  public async Task SendStatusUpdate(CheckResult result)
  {
    await Clients.All.SendAsync("StatusUpdate", new
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

  public async Task SendAllStatuses(IEnumerable<CheckResult> results)
  {
    var services = results.Select(r => new
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
    }).ToList();

    await Clients.All.SendAsync("AllStatuses", new
    {
      count = services.Count,
      services
    });
  }

  public async Task SendSummary(int total, int alive, int dead, int pending, double uptimePercent, double avgDuration)
  {
    await Clients.All.SendAsync("Summary", new
    {
      total,
      alive,
      dead,
      pending,
      uptimePercent,
      averageDurationMs = avgDuration,
      timestamp = DateTime.UtcNow
    });
  }
}
