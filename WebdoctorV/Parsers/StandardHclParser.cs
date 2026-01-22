using System.Text.RegularExpressions;
using WebdoctorV.Models;

namespace WebdoctorV.Parsers;

/// <summary>
/// Parser for standard HCL format (HashiCorp Configuration Language)
/// Supports: name = "value", blocks, nested structures
/// Groups are containers, services are checkable items
/// </summary>
public class StandardHclParser
{
  public static Config ParseConfig(string input)
  {
    if (string.IsNullOrWhiteSpace(input))
    {
      return new Config();
    }

    var config = new Config();
    var cleaned = CleanInput(input);

    // Parse global attributes (top-level key = value pairs)
    var globalAttributes = ParseGlobalAttributes(cleaned);
    if (globalAttributes.TryGetValue("interval", out var intervalStr))
    {
      config.Interval = ParseTimeSpan(intervalStr);
    }

    // Parse top-level blocks (both group and service)
    var topLevelBlocks = ParseTopLevelBlocks(cleaned);
    foreach (var block in topLevelBlocks)
    {
      if (block.Type == "group")
      {
        // Groups are containers - process their children
        ProcessGroup(block, config, config.Interval, null, null);
      }
      else if (block.Type == "service")
      {
        // Services are checkable items (top-level, no inheritance)
        var service = BuildService(block, config.Interval, null, null);
        if (service != null && IsCheckableService(service))
        {
          config.Services.Add(service);
        }
      }
    }

    return config;
  }

  private static void ProcessGroup(HclBlock groupBlock, Config config, TimeSpan? defaultInterval, string? parentPath, Dictionary<string, string>? inheritedAttributes = null)
  {
    // Merge group attributes with inherited attributes (group attributes override inherited)
    var groupAttributes = new Dictionary<string, string>(inheritedAttributes ?? new Dictionary<string, string>());
    foreach (var attr in groupBlock.Attributes)
    {
      groupAttributes[attr.Key] = attr.Value;
    }

    // Process all children of the group
    foreach (var child in groupBlock.Children)
    {
      if (child.Type == "group")
      {
        // Recursively process nested groups, passing inherited attributes
        ProcessGroup(child, config, defaultInterval, parentPath, groupAttributes);
      }
      else if (child.Type == "service")
      {
        // Build service from child block with inherited attributes
        var servicePath = string.IsNullOrEmpty(parentPath) ? child.Name : $"{parentPath}.{child.Name}";
        var service = BuildService(child, defaultInterval, servicePath, groupAttributes);
        if (service != null && IsCheckableService(service))
        {
          config.Services.Add(service);
        }
      }
    }
  }

  private static bool IsCheckableService(ServiceConfig service)
  {
    // Service is checkable if it has protocol and host
    return !string.IsNullOrEmpty(service.Protocol) && !string.IsNullOrEmpty(service.Host);
  }

  private static string CleanInput(string input)
  {
    // Remove comments
    var cleaned = Regex.Replace(input, @"#.*", "", RegexOptions.Multiline);
    // Normalize line endings
    cleaned = cleaned.Replace("\r\n", "\n").Replace("\r", "\n");
    return cleaned;
  }

  private static Dictionary<string, string> ParseGlobalAttributes(string input)
  {
    var attributes = new Dictionary<string, string>();
    var lines = input.Split('\n');

    foreach (var line in lines)
    {
      var trimmed = line.Trim();
      if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#") || trimmed.Contains("{"))
        continue;

      var match = Regex.Match(trimmed, @"^(\w+)\s*=\s*(.+)$");
      if (match.Success)
      {
        var key = match.Groups[1].Value;
        var value = Unquote(match.Groups[2].Value.Trim());
        attributes[key] = value;
      }
    }

    return attributes;
  }

  private static List<HclBlock> ParseTopLevelBlocks(string input)
  {
    var blocks = new List<HclBlock>();

    // Find all top-level blocks (group or service)
    var blockPattern = @"(group|service)\s+""([^""]+)""\s*\{";
    var matches = Regex.Matches(input, blockPattern);

    foreach (Match match in matches)
    {
      var blockType = match.Groups[1].Value;
      var blockName = match.Groups[2].Value;
      var startPos = match.Index + match.Length;
      var blockContent = ExtractBlockContent(input, startPos - 1);

      if (blockContent != null)
      {
        var block = ParseBlock(blockName, blockContent, blockType);
        blocks.Add(block);
      }
    }

    return blocks;
  }

  private static string? ExtractBlockContent(string input, int startPos)
  {
    int depth = 0;
    int start = startPos;
    bool inString = false;
    char stringChar = '\0';
    bool escaped = false;

    for (int i = startPos; i < input.Length; i++)
    {
      char c = input[i];

      if (escaped)
      {
        escaped = false;
        continue;
      }

      if (c == '\\')
      {
        escaped = true;
        continue;
      }

      if (!inString && (c == '"' || c == '\''))
      {
        inString = true;
        stringChar = c;
      }
      else if (inString && c == stringChar)
      {
        inString = false;
      }
      else if (!inString)
      {
        if (c == '{')
        {
          if (depth == 0) start = i + 1;
          depth++;
        }
        else if (c == '}')
        {
          depth--;
          if (depth == 0)
          {
            return input.Substring(start, i - start);
          }
        }
      }
    }

    return null;
  }

  private static HclBlock ParseBlock(string name, string content, string type)
  {
    var block = new HclBlock { Name = name, Type = type };

    // Parse attributes
    var attributePattern = @"(\w+)\s*=\s*(""(?:[^""\\]|\\.)*""|'[^']*'|\d+|\[.*?\])";
    var attrMatches = Regex.Matches(content, attributePattern, RegexOptions.Singleline);

    foreach (Match match in attrMatches)
    {
      var key = match.Groups[1].Value;
      var value = Unquote(match.Groups[2].Value.Trim());
      block.Attributes[key] = value;
    }

    // Parse nested blocks (both group and service)
    var nestedPattern = @"(group|service)\s+""([^""]+)""\s*\{";
    var nestedMatches = Regex.Matches(content, nestedPattern);

    foreach (Match match in nestedMatches)
    {
      var nestedType = match.Groups[1].Value;
      var nestedName = match.Groups[2].Value;
      var nestedStart = match.Index;
      var nestedContent = ExtractBlockContent(content, nestedStart);

      if (nestedContent != null)
      {
        var nestedBlock = ParseBlock(nestedName, nestedContent, nestedType);
        block.Children.Add(nestedBlock);
      }
    }

    // Parse response block
    var responseMatch = Regex.Match(content, @"response\s*\{([^}]+)\}", RegexOptions.Singleline);
    if (responseMatch.Success)
    {
      var responseContent = responseMatch.Groups[1].Value;
      var responseAttrs = new Dictionary<string, string>();
      var responseAttrMatches = Regex.Matches(responseContent, @"(\w+)\s*=\s*(\d+)");
      foreach (Match m in responseAttrMatches)
      {
        responseAttrs[m.Groups[1].Value] = m.Groups[2].Value;
      }
      block.ResponseAttributes = responseAttrs;
    }

    return block;
  }

  private static string Unquote(string value)
  {
    value = value.Trim();
    if ((value.StartsWith("\"") && value.EndsWith("\"")) ||
        (value.StartsWith("'") && value.EndsWith("'")))
    {
      value = value.Substring(1, value.Length - 2);
      // Unescape
      value = value.Replace("\\\"", "\"").Replace("\\'", "'").Replace("\\\\", "\\");
    }
    return value;
  }

  private static ServiceConfig? BuildService(HclBlock block, TimeSpan? defaultInterval, string? serviceId, Dictionary<string, string>? inheritedAttributes = null)
  {
    if (string.IsNullOrEmpty(serviceId))
    {
      serviceId = block.Name;
    }

    var service = new ServiceConfig { Id = serviceId };
    var interval = defaultInterval;

    // Merge inherited attributes with block attributes (block attributes override inherited)
    var allAttributes = new Dictionary<string, string>(inheritedAttributes ?? new Dictionary<string, string>());
    foreach (var attr in block.Attributes)
    {
      allAttributes[attr.Key] = attr.Value;
    }

    // Parse attributes (from merged dictionary)
    foreach (var attr in allAttributes)
    {
      switch (attr.Key)
      {
        case "name":
          service.Name = attr.Value;
          break;
        case "protocol":
          service.Protocol = attr.Value;
          break;
        case "host":
          service.Host = attr.Value;
          break;
        case "port":
          if (int.TryParse(attr.Value, out var port))
            service.Port = port;
          break;
        case "timeout":
          service.Timeout = ParseTimeSpan(attr.Value);
          break;
        case "interval":
          interval = ParseTimeSpan(attr.Value);
          service.Interval = interval;
          break;
        case "method":
          service.Method = attr.Value;
          break;
        case "path":
          var path = attr.Value;
          service.PathIsPrefix = path.StartsWith("^");
          var cleanPath = path.TrimStart('^');

          // If there's an inherited path (from group), concatenate it as prefix
          if (inheritedAttributes != null && inheritedAttributes.TryGetValue("path", out var inheritedPath))
          {
            var cleanInherited = inheritedPath.TrimStart('^').TrimStart('/').TrimEnd('/');
            var cleanCurrent = cleanPath.TrimStart('/').TrimEnd('/');
            if (!string.IsNullOrEmpty(cleanInherited) && !string.IsNullOrEmpty(cleanCurrent))
            {
              // Group path + service path
              service.Path = $"/{cleanInherited}/{cleanCurrent}";
            }
            else if (!string.IsNullOrEmpty(cleanInherited))
            {
              // Only group path
              service.Path = $"/{cleanInherited}";
            }
            else if (!string.IsNullOrEmpty(cleanCurrent))
            {
              // Only service path
              service.Path = cleanPath.StartsWith("/") ? cleanPath : $"/{cleanCurrent}";
            }
            else
            {
              service.Path = cleanPath;
            }
          }
          else
          {
            // No inherited path, use service path as-is
            service.Path = cleanPath;
          }
          break;
        case "query":
          service.Query = attr.Value;
          break;
        case "command":
          service.Command = attr.Value;
          break;
        case "username":
          service.Username = attr.Value;
          break;
        case "password":
          service.Password = attr.Value;
          break;
        case "database":
          service.Database = attr.Value;
          break;
        case "expected_status":
        case "status":
          if (int.TryParse(attr.Value, out var status))
          {
            if (service.Response == null)
              service.Response = new ResponseConfig();
            service.Response.Status = status;
          }
          break;
        case "retry_count":
          if (int.TryParse(attr.Value, out var retryCount))
            service.RetryCount = retryCount;
          break;
        case "retry_delay":
          service.RetryDelay = ParseTimeSpan(attr.Value);
          break;
      }
    }

    // Parse response block
    if (block.ResponseAttributes != null)
    {
      if (service.Response == null)
        service.Response = new ResponseConfig();

      if (block.ResponseAttributes.TryGetValue("rows", out var rowsStr) && int.TryParse(rowsStr, out var rows))
        service.Response.Rows = rows;

      if (block.ResponseAttributes.TryGetValue("columns", out var colsStr) && int.TryParse(colsStr, out var cols))
        service.Response.Columns = cols;
    }

    // Build children (for services that have nested services)
    foreach (var childBlock in block.Children)
    {
      if (childBlock.Type == "service")
      {
        var childServiceId = $"{serviceId}.{childBlock.Name}";
        // Build child service with inherited attributes from parent service
        var childInherited = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(service.Protocol)) childInherited["protocol"] = service.Protocol;
        if (!string.IsNullOrEmpty(service.Host)) childInherited["host"] = service.Host;
        if (service.Port != 0) childInherited["port"] = service.Port.ToString();
        if (service.Timeout.HasValue) childInherited["timeout"] = $"{service.Timeout.Value.TotalSeconds}s";
        if (!string.IsNullOrEmpty(service.Method)) childInherited["method"] = service.Method;
        if (!string.IsNullOrEmpty(service.Username)) childInherited["username"] = service.Username;
        if (!string.IsNullOrEmpty(service.Password)) childInherited["password"] = service.Password;
        if (!string.IsNullOrEmpty(service.Database)) childInherited["database"] = service.Database;

        var childService = BuildService(childBlock, interval ?? defaultInterval, childServiceId, childInherited);
        if (childService != null)
        {
          service.Children.Add(childService);
        }
      }
    }

    return service;
  }

  private static TimeSpan? ParseTimeSpan(string value)
  {
    if (string.IsNullOrWhiteSpace(value))
      return null;

    value = value.Trim().ToLower();

    if (value.EndsWith("s") && double.TryParse(value.Substring(0, value.Length - 1), out var seconds))
      return TimeSpan.FromSeconds(seconds);

    if (value.EndsWith("m") && double.TryParse(value.Substring(0, value.Length - 1), out var minutes))
      return TimeSpan.FromMinutes(minutes);

    if (value.EndsWith("h") && double.TryParse(value.Substring(0, value.Length - 1), out var hours))
      return TimeSpan.FromHours(hours);

    if (value.EndsWith("d") && double.TryParse(value.Substring(0, value.Length - 1), out var days))
      return TimeSpan.FromDays(days);

    return null;
  }

  private class HclBlock
  {
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "service"; // "group" or "service"
    public Dictionary<string, string> Attributes { get; set; } = new();
    public List<HclBlock> Children { get; set; } = new();
    public Dictionary<string, string>? ResponseAttributes { get; set; }
  }
}
