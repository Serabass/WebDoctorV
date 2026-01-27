using MySqlConnector;
using WebdoctorV.Models;

namespace WebdoctorV.Checkers;

public class MySqlChecker : IChecker
{
  public bool Supports(string protocol) => protocol.Equals("mysql", StringComparison.OrdinalIgnoreCase);

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
      var database = service.Database ?? "mysql";
      var username = service.Username ?? "root";
      var password = service.Password ?? "";
      var connectionString = $"Server={service.Host};Port={service.Port};Database={database};User Id={username};Password={password};Connection Timeout={(int)(service.Timeout?.TotalSeconds ?? 30)}";

      await using var connection = new MySqlConnection(connectionString);
      await connection.OpenAsync();

      if (!string.IsNullOrEmpty(service.Query))
      {
        await using var command = new MySqlCommand(service.Query, connection);
        await using var reader = await command.ExecuteReaderAsync();

        var rows = 0;
        var columns = reader.FieldCount;
        var data = new List<Dictionary<string, object>>();

        while (await reader.ReadAsync())
        {
          rows++;
          var row = new Dictionary<string, object>();
          for (int i = 0; i < columns; i++)
          {
            row[reader.GetName(i)] = reader.GetValue(i);
          }
          data.Add(row);
        }

        result.Duration = DateTime.UtcNow - startTime;
        result.LastCheck = DateTime.UtcNow;

        if (service.Response != null)
        {
          var matches = true;
          if (service.Response.Rows.HasValue && service.Response.Rows.Value != rows)
            matches = false;
          if (service.Response.Columns.HasValue && service.Response.Columns.Value != columns)
            matches = false;
          if (service.Response.Data != null && !MatchesData(data, service.Response.Data))
            matches = false;

          result.Status = matches ? CheckStatus.Alive : CheckStatus.Dead;
        }
        else
        {
          result.Status = CheckStatus.Alive;
        }
      }
      else
      {
        result.Duration = DateTime.UtcNow - startTime;
        result.LastCheck = DateTime.UtcNow;
        result.Status = CheckStatus.Alive;
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

  private bool MatchesData(List<Dictionary<string, object>> actual, List<Dictionary<string, object>> expected)
  {
    if (actual.Count != expected.Count)
      return false;

    for (int i = 0; i < actual.Count; i++)
    {
      var actualRow = actual[i];
      var expectedRow = expected[i];

      foreach (var kvp in expectedRow)
      {
        if (!actualRow.ContainsKey(kvp.Key))
          return false;

        var actualValue = actualRow[kvp.Key];
        var expectedValue = kvp.Value;

        if (!Equals(actualValue, expectedValue))
          return false;
      }
    }

    return true;
  }
}
