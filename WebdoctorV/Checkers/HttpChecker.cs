using System.Net;
using WebdoctorV.Models;

namespace WebdoctorV.Checkers;

public class HttpChecker : IChecker
{
  private readonly HttpClient _httpClient;

  public HttpChecker(HttpClient httpClient)
  {
    _httpClient = httpClient;
  }

  public bool Supports(string protocol) => protocol.Equals("http", StringComparison.OrdinalIgnoreCase) ||
                                           protocol.Equals("https", StringComparison.OrdinalIgnoreCase);

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
      var scheme = service.Protocol.ToLower();
      var url = $"{scheme}://{service.Host}:{service.Port}{service.Path}";

      var request = new HttpRequestMessage(
          new HttpMethod(service.Method ?? "GET"),
          url
      );

      var timeout = service.Timeout ?? TimeSpan.FromSeconds(30);
      using var cts = new CancellationTokenSource(timeout);

      var response = await _httpClient.SendAsync(request, cts.Token);

      result.Duration = DateTime.UtcNow - startTime;
      result.LastCheck = DateTime.UtcNow;

      // Check status code
      bool statusOk = false;
      if (service.Response?.Status.HasValue == true)
      {
        statusOk = response.StatusCode == (HttpStatusCode)service.Response.Status.Value;
      }
      else
      {
        statusOk = response.IsSuccessStatusCode;
      }

      if (!statusOk)
      {
        result.Status = CheckStatus.Dead;
        result.Error = $"HTTP {response.StatusCode}";
        return result;
      }

      // Validate response body if configured
      if (service.Response != null && (!string.IsNullOrEmpty(service.Response.BodyContains) ||
                                       !string.IsNullOrEmpty(service.Response.BodyRegex)))
      {
        var bodyContent = await response.Content.ReadAsStringAsync(cts.Token);

        // Check contains
        if (!string.IsNullOrEmpty(service.Response.BodyContains))
        {
          if (!bodyContent.Contains(service.Response.BodyContains, StringComparison.OrdinalIgnoreCase))
          {
            result.Status = CheckStatus.Dead;
            result.Error = $"Response body does not contain expected text: {service.Response.BodyContains}";
            return result;
          }
        }

        // Check regex
        if (!string.IsNullOrEmpty(service.Response.BodyRegex))
        {
          try
          {
            var regex = new System.Text.RegularExpressions.Regex(service.Response.BodyRegex);
            if (!regex.IsMatch(bodyContent))
            {
              result.Status = CheckStatus.Dead;
              result.Error = $"Response body does not match regex pattern: {service.Response.BodyRegex}";
              return result;
            }
          }
          catch (Exception regexEx)
          {
            result.Status = CheckStatus.Dead;
            result.Error = $"Invalid regex pattern: {regexEx.Message}";
            return result;
          }
        }
      }

      result.Status = CheckStatus.Alive;
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
