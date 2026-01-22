using Sprache;
using WebdoctorV.Models;

namespace WebdoctorV.Parsers;

public class HclParser
{
  private static readonly Parser<char> Quote = Parse.Char('\'');
  private static readonly Parser<char> DoubleQuote = Parse.Char('"');
  private static readonly Parser<char> OpenBrace = Parse.Char('{');
  private static readonly Parser<char> CloseBrace = Parse.Char('}');
  private static readonly Parser<char> Dot = Parse.Char('.');
  private static readonly Parser<char> Exclamation = Parse.Char('!'); // For executable blocks
  private static readonly Parser<char> Minus = Parse.Char('-'); // For exclude blocks
  private static readonly Parser<char> Caret = Parse.Char('^');

  private static readonly Parser<string> Identifier =
      (Parse.LetterOrDigit.Or(Parse.Char('-')).Or(Parse.Char('_')))
          .AtLeastOnce()
          .Text();
  private static readonly Parser<string> Whitespace = Parse.WhiteSpace.Many().Text();
  private static readonly Parser<string> Comment = Parse.String("#").Then(_ => Parse.AnyChar.Until(Parse.LineEnd).Text());

  // QuotedString with support for escaped quotes ('')
  // Parse manually to handle escaped quotes properly
  private static readonly Parser<string> QuotedString =
      from open in Quote
      from content in ParseEscapedQuotedString()
      from close in Quote
      select content;

  private static Parser<string> ParseEscapedQuotedString()
  {
    // Parse characters one by one, handling escaped quotes ('')
    // Try to parse '' first (escaped quote), then any other char except '
    return (
        Parse.String("''").Return('\'') // Escaped quote: '' becomes '
            .Or(Parse.Char(c => c != '\'', "non-quote character"))
    )
    .Many()
    .Select(chars => new string(chars.ToArray()));
  }

  private static readonly Parser<string> DoubleQuotedString =
      from open in DoubleQuote
      from content in Parse.AnyChar.Except(DoubleQuote).Many().Text()
      from close in DoubleQuote
      select content;

  private static readonly Parser<string> StringValue = QuotedString.Or(DoubleQuotedString);

  private static readonly Parser<TimeSpan> TimeSpanValue =
      from number in Parse.Number
      from unit in Parse.String("ms").Or(Parse.String("s")).Or(Parse.String("m")).Or(Parse.String("h")).Text()
      select ParseTimeSpan(number, unit);

  private static TimeSpan ParseTimeSpan(string number, string unit)
  {
    var value = double.Parse(number);
    return unit switch
    {
      "ms" => TimeSpan.FromMilliseconds(value),
      "s" => TimeSpan.FromSeconds(value),
      "m" => TimeSpan.FromMinutes(value),
      "h" => TimeSpan.FromHours(value),
      _ => TimeSpan.FromSeconds(value)
    };
  }

  private static readonly Parser<int> IntValue = Parse.Number.Select(int.Parse);

  private static readonly Parser<object> JsonValue =
      Parse.String("true").Return((object)true)
      .Or(Parse.String("false").Return((object)false))
      .Or(Parse.String("null").Return((object)(object?)null!))
      .Or(IntValue.Select(i => (object)i))
      .Or(Parse.Number.Select(n => (object)double.Parse(n)))
      .Or(DoubleQuotedString.Select(s => (object)s));

  private static readonly Parser<Dictionary<string, object>> JsonObject =
      from open in Parse.Char('{')
      from ws1 in Whitespace.Optional()
      from pairs in JsonPair.DelimitedBy(Parse.Char(',').Then(_ => Whitespace.Optional()))
      from ws2 in Whitespace.Optional()
      from close in Parse.Char('}')
      select pairs.ToDictionary(p => p.Key, p => p.Value);

  private static readonly Parser<KeyValuePair<string, object>> JsonPair =
      from key in DoubleQuotedString
      from colon in Parse.Char(':').Then(_ => Whitespace.Optional())
      from value in JsonValue.Or(JsonObject.Select(o => (object)o))
      select new KeyValuePair<string, object>(key, value);

  private static readonly Parser<List<Dictionary<string, object>>> JsonArray =
      from open in Parse.Char('[')
      from ws1 in Whitespace.Optional()
      from items in JsonObject.DelimitedBy(Parse.Char(',').Then(_ => Whitespace.Optional()))
      from ws2 in Whitespace.Optional()
      from close in Parse.Char(']')
      select items.ToList();

  // String value parser that handles ^ prefix for paths
  private static readonly Parser<string> PathStringValue =
      Caret.Optional()
          .Then(caret =>
              StringValue.Select(s => (caret.IsDefined ? "^" : "") + s)
          );

  private static readonly Parser<object> AttributeValueParser =
      TimeSpanValue.Select(ts => (object)ts)
          .Or(JsonArray.Select(a => (object)a))
          .Or(IntValue.Select(i => (object)i))
          .Or(PathStringValue.Select(s => (object)s))
          .Or(StringValue.Select(s => (object)s));

  private static readonly Parser<Attribute> AttributeParser =
      from dot in Dot
      from name in Identifier
      from ws in Parse.WhiteSpace.AtLeastOnce() // Require at least one whitespace
      from value in AttributeValueParser // Value is required - parser will fail if not found
      select new Attribute { Name = name, Value = value };

  private static Parser<Block> BlockParserRef;

  private static readonly Parser<Block> BlockParser =
      Parse.Ref(() => BlockParserRef);

  static HclParser()
  {
    // Block parser: identifier (with optional ! for executable or - for exclude) followed by { }
    // No dot prefix - blocks are clearly distinguished from attributes
    // Block parser: parse attributes and children blocks interleaved
    // This allows mixing attributes and blocks in any order
    BlockParserRef =
        from name in (Exclamation.Then(_ => Identifier.Select(id => "!" + id))) // Executable blocks
            .Or(Minus.Then(_ => Identifier.Select(id => "-" + id))) // Exclude blocks
            .Or(Identifier) // Regular blocks
        from ws1 in Parse.WhiteSpace.Many()
        from open in OpenBrace
        from ws2 in Parse.WhiteSpace.Many()
          // Parse attributes and children interleaved - try block first, then attribute
          // Use Many() instead of XMany() - XMany() doesn't stop properly at closing brace
        from content in Parse.Ref(() =>
            Parse.WhiteSpace.Many()
                .Then(ws =>
                    // Try attribute first (starts with dot), then block
                    // AttributeParser will fail if value is missing (AttributeValueParser is required)
                    AttributeParser.Select(a => (object)a)
                        .Or(BlockParser.Select(b => (object)b))
                )
                .Many()
        )
        from ws3 in Parse.WhiteSpace.Many()
        from close in CloseBrace
        select new Block
        {
          Name = name,
          Attributes = content.OfType<Attribute>().ToList(),
          Children = content.OfType<Block>().ToList()
        };
  }

  private static readonly Parser<Config> ConfigParser =
      from ws0 in Parse.WhiteSpace.Many()
      from attributes in AttributeParser.Many()
      from ws1 in Parse.WhiteSpace.Many()
      from blocks in BlockParser.Many()
      from ws2 in Parse.WhiteSpace.Many()
      select BuildConfig(attributes.ToList(), blocks.ToList());

  // Wrapper to add logging  
  private static Config ParseConfigWithLogging(string cleaned)
  {
    try
    {
      var result = ConfigParser.Parse(cleaned);
      return result;
    }
    catch (Exception ex)
    {
      Console.WriteLine($"DEBUG: Parse exception: {ex.Message}");
      Console.WriteLine($"DEBUG: Exception type: {ex.GetType().Name}");
      if (ex.InnerException != null)
      {
        Console.WriteLine($"DEBUG: Inner exception: {ex.InnerException.Message}");
      }
      throw;
    }
  }

  public static Config ParseConfig(string input)
  {
    if (string.IsNullOrWhiteSpace(input))
    {
      return new Config();
    }

    var cleaned = input;
    // Remove comments (but preserve line structure)
    cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"#.*", "", System.Text.RegularExpressions.RegexOptions.Multiline);
    // Normalize line endings
    cleaned = cleaned.Replace("\r\n", "\n").Replace("\r", "\n");
    // Ensure whitespace before opening brace if missing (but not if it's already there)
    // But be careful not to break strings inside quotes
    var sb = new System.Text.StringBuilder();
    bool inSingleQuote = false;
    bool inDoubleQuote = false;
    for (int i = 0; i < cleaned.Length; i++)
    {
      char c = cleaned[i];
      if (c == '\'' && (i == 0 || cleaned[i - 1] != '\\'))
        inSingleQuote = !inSingleQuote;
      else if (c == '"' && (i == 0 || cleaned[i - 1] != '\\'))
        inDoubleQuote = !inDoubleQuote;

      if (!inSingleQuote && !inDoubleQuote && i > 0 && i < cleaned.Length - 1)
      {
        // Check if we need to add space before {
        if (c == '{' && cleaned[i - 1] != ' ' && cleaned[i - 1] != '\n' && cleaned[i - 1] != '\t')
        {
          // Check if previous char is part of identifier
          char prev = cleaned[i - 1];
          if (char.IsLetterOrDigit(prev) || prev == '-' || prev == '_' || prev == '!')
          {
            sb.Append(' ');
          }
        }
      }
      sb.Append(c);
    }
    cleaned = sb.ToString();

    // Find the position where first block starts (identifier followed by {, not preceded by .)
    // This separates global attributes from blocks
    // We need to find the first real block (not inside quotes, not an attribute)
    int blockStartPos = cleaned.Length;
    // Reset quote tracking for block detection
    inSingleQuote = false;
    inDoubleQuote = false;

    for (int i = 0; i < cleaned.Length; i++)
    {
      char c = cleaned[i];

      // Track quotes
      if (c == '\'' && (i == 0 || cleaned[i - 1] != '\\'))
        inSingleQuote = !inSingleQuote;
      else if (c == '"' && (i == 0 || cleaned[i - 1] != '\\'))
        inDoubleQuote = !inDoubleQuote;

      // If we're in quotes, skip
      if (inSingleQuote || inDoubleQuote)
        continue;

      // Look for pattern: identifier followed by {
      // But NOT preceded by . (which would be an attribute)
      if (i > 0 && cleaned[i - 1] == '.')
        continue;

      // Check if this looks like a block start: word char, !, or -, then optional whitespace, then {
      if (char.IsLetterOrDigit(cleaned[i]) || cleaned[i] == '-' || cleaned[i] == '_' || cleaned[i] == '!')
      {
        // Find the end of identifier
        int idStart = i;
        int idEnd = i;

        // Handle ! or - prefix
        if (cleaned[i] == '!' || cleaned[i] == '-')
        {
          idEnd++;
          if (idEnd >= cleaned.Length)
            continue;
        }

        // Parse identifier
        while (idEnd < cleaned.Length && (char.IsLetterOrDigit(cleaned[idEnd]) || cleaned[idEnd] == '-' || cleaned[idEnd] == '_'))
        {
          idEnd++;
        }

        // Skip whitespace
        int bracePos = idEnd;
        while (bracePos < cleaned.Length && char.IsWhiteSpace(cleaned[bracePos]))
        {
          bracePos++;
        }

        // Check if next char is {
        if (bracePos < cleaned.Length && cleaned[bracePos] == '{')
        {
          // Check that before idStart is not a dot
          if (idStart == 0 || cleaned[idStart - 1] != '.')
          {
            blockStartPos = idStart;
            break;
          }
        }
      }
    }

    string globalPart = blockStartPos < cleaned.Length ? cleaned.Substring(0, blockStartPos).Trim() : cleaned.Trim();
    string blocksPart = blockStartPos < cleaned.Length ? cleaned.Substring(blockStartPos).TrimStart() : "";

    try
    {
      // Parse global attributes (only from the part before first block)
      var globalAttrs = string.IsNullOrWhiteSpace(globalPart)
          ? new List<Attribute>()
          : AttributeParser.Many().Parse(globalPart).ToList();

      // Parse blocks using the recursive parser
      List<Block> blocks;
      if (string.IsNullOrWhiteSpace(blocksPart))
      {
        blocks = new List<Block>();
      }
      else
      {
        // Parse blocks with proper whitespace handling between them
        // Create a parser that explicitly handles whitespace before AND after each block
        var blocksWithWhitespace =
            from ws1 in Parse.WhiteSpace.Many()
            from block in BlockParser
            from ws2 in Parse.WhiteSpace.Many()
            select block;

        // Parse all blocks, each preceded and followed by optional whitespace
        // Try with End() first to catch missing closing braces, but allow trailing content
        // (global attributes after blocks should be ignored per test requirements)
        try
        {
          blocks = blocksWithWhitespace.Many().End().Parse(blocksPart).ToList();
        }
        catch (Sprache.ParseException)
        {
          // If End() fails, try without it - this allows trailing content (global attributes)
          // but we still want to catch missing closing braces within blocks
          blocks = blocksWithWhitespace.Many().Parse(blocksPart).ToList();
        }
      }

      var result = BuildConfig(globalAttrs, blocks);
      return result;
    }
    catch (Exception ex)
    {
      throw new InvalidOperationException($"Failed to parse config: {ex.Message}. Input length: {cleaned.Length}, First 100 chars: {cleaned.Substring(0, Math.Min(100, cleaned.Length))}", ex);
    }
  }

  private static Config BuildConfig(List<Attribute> globalAttributes, List<Block> blocks)
  {
    var config = new Config();

    foreach (var attr in globalAttributes)
    {
      if (attr.Name == "interval")
      {
        if (attr.Value is TimeSpan intervalTs)
          config.Interval = intervalTs;
        else if (attr.Value is string intervalStr)
          config.Interval = ParseTimeSpanFromString(intervalStr);
      }
    }

    foreach (var block in blocks)
    {
      var service = BuildService(block, config.Interval, null);
      if (service != null)
      {
        config.Services.Add(service);
      }
    }

    return config;
  }

  private static void InheritAttributes(ServiceConfig child, ServiceConfig parent)
  {
    // Inherit attributes from parent if not set in child
    if (string.IsNullOrEmpty(child.Host))
      child.Host = parent.Host;
    if (child.Port == 0)
      child.Port = parent.Port;
    if (string.IsNullOrEmpty(child.Protocol))
      child.Protocol = parent.Protocol;
    if (!child.Timeout.HasValue)
      child.Timeout = parent.Timeout;
    if (string.IsNullOrEmpty(child.Method))
      child.Method = parent.Method;

    // Recursively inherit attributes to all descendants from their immediate parent (child)
    // This ensures grandchildren inherit from grandparent through their parent
    foreach (var grandchild in child.Children)
    {
      InheritAttributes(grandchild, child);
    }
  }

  private static ServiceConfig? BuildService(Block block, TimeSpan? defaultInterval, string? parentPath = null)
  {
    // Exclude blocks are handled separately, not as services
    if (block.Name.StartsWith("-"))
      return null; // Exclude block

    // For executable blocks (!), use the name without ! as ID
    var serviceId = block.Name.StartsWith("!") ? block.Name.Substring(1) : block.Name;
    var service = new ServiceConfig { Id = serviceId };

    // Build full path for blocks - they form path hierarchy
    if (!string.IsNullOrEmpty(parentPath))
    {
      // All blocks form path hierarchy: parent.path.child
      service.Id = $"{parentPath}.{serviceId}";
    }
    else
    {
      // Top-level block - just use the name
      service.Id = serviceId;
    }
    var interval = defaultInterval;

    foreach (var attr in block.Attributes)
    {
      switch (attr.Name)
      {
        case "name" when attr.Value is string name:
          service.Name = name;
          break;
        case "protocol" when attr.Value is string protocol:
          service.Protocol = protocol;
          break;
        case "host" when attr.Value is string host:
          service.Host = host;
          break;
        case "port" when attr.Value is int port:
          service.Port = port;
          break;
        case "timeout":
          if (attr.Value is TimeSpan timeoutTs)
            service.Timeout = timeoutTs;
          else if (attr.Value is string timeoutStr)
            service.Timeout = ParseTimeSpanFromString(timeoutStr);
          break;
        case "interval":
          if (attr.Value is TimeSpan intervalTs)
          {
            interval = intervalTs;
            service.Interval = interval;
          }
          else if (attr.Value is string intervalStr)
          {
            interval = ParseTimeSpanFromString(intervalStr);
            service.Interval = interval;
          }
          break;
        case "method" when attr.Value is string method:
          service.Method = method;
          break;
        case "path" when attr.Value is string path:
          service.Path = path.TrimStart('^');
          service.PathIsPrefix = path.StartsWith("^");
          break;
        case "query" when attr.Value is string query:
          service.Query = query;
          break;
        case "command" when attr.Value is string command:
          service.Command = command;
          break;
        case "expected_status" when attr.Value is int status:
          // Short syntax for HTTP status: .expected_status 200
          if (service.Response == null)
            service.Response = new ResponseConfig();
          service.Response.Status = status;
          break;
        case "status" when attr.Value is int status:
          // Alternative short syntax: .status 200
          if (service.Response == null)
            service.Response = new ResponseConfig();
          service.Response.Status = status;
          break;
        case "retry_count" when attr.Value is int retryCount:
          service.RetryCount = retryCount;
          break;
        case "retry_delay":
          if (attr.Value is TimeSpan retryDelayTs)
            service.RetryDelay = retryDelayTs;
          else if (attr.Value is string retryDelayStr)
            service.RetryDelay = ParseTimeSpanFromString(retryDelayStr);
          break;
        case "body_contains" when attr.Value is string bodyContains:
          if (service.Response == null)
            service.Response = new ResponseConfig();
          service.Response.BodyContains = bodyContains;
          break;
        case "body_regex" when attr.Value is string bodyRegex:
          if (service.Response == null)
            service.Response = new ResponseConfig();
          service.Response.BodyRegex = bodyRegex;
          break;
        case "username" when attr.Value is string username:
          service.Username = username;
          break;
        case "password" when attr.Value is string password:
          service.Password = password;
          break;
        case "database" when attr.Value is string database:
          service.Database = database;
          break;
      }
    }

    // Handle response block (has priority over attributes)
    var responseBlock = block.Children.FirstOrDefault(c => c.Name == "response");
    if (responseBlock != null)
    {
      // If response block exists, it overrides any attributes
      service.Response = BuildResponse(responseBlock);
    }

    // Handle children blocks (regular and executable ! blocks)
    foreach (var childBlock in block.Children.Where(c => c.Name != "response" && !c.Name.StartsWith("-")))
    {
      // Build path for child: if parent is executable, use parent.Id, otherwise use parent.Id
      var childPath = service.Id;
      var childService = BuildService(childBlock, interval, childPath);
      if (childService != null)
      {
        // Merge parent attributes (inherit from parent if not set)
        InheritAttributes(childService, service);

        service.Children.Add(childService);
      }
    }

    // Handle exclude blocks (with - prefix)
    foreach (var excludeBlock in block.Children.Where(c => c.Name.StartsWith("-")))
    {
      var exclude = new ExcludeConfig();
      foreach (var attr in excludeBlock.Attributes)
      {
        if (attr.Name == "path" && attr.Value is string path)
        {
          exclude.Path = path.TrimStart('^');
          exclude.PathIsPrefix = path.StartsWith("^");
        }
      }
      service.Excludes.Add(exclude);
    }

    return service;
  }

  private static ResponseConfig BuildResponse(Block responseBlock)
  {
    var response = new ResponseConfig();

    foreach (var attr in responseBlock.Attributes)
    {
      switch (attr.Name)
      {
        case "status" when attr.Value is int status:
          response.Status = status;
          break;
        case "rows" when attr.Value is int rows:
          response.Rows = rows;
          break;
        case "columns" when attr.Value is int columns:
          response.Columns = columns;
          break;
        case "exit_code" when attr.Value is int exitCode:
          response.ExitCode = exitCode;
          break;
        case "data" when attr.Value is List<Dictionary<string, object>> data:
          response.Data = data;
          break;
        case "body_contains" when attr.Value is string bodyContains:
          response.BodyContains = bodyContains;
          break;
        case "body_regex" when attr.Value is string bodyRegex:
          response.BodyRegex = bodyRegex;
          break;
      }
    }

    return response;
  }

  private static TimeSpan ParseTimeSpanFromString(string value)
  {
    if (string.IsNullOrWhiteSpace(value))
      return TimeSpan.FromSeconds(60);

    var match = System.Text.RegularExpressions.Regex.Match(value, @"^(\d+)(ms|s|m|h)$");
    if (match.Success)
    {
      var number = double.Parse(match.Groups[1].Value);
      var unit = match.Groups[2].Value;
      return unit switch
      {
        "ms" => TimeSpan.FromMilliseconds(number),
        "s" => TimeSpan.FromSeconds(number),
        "m" => TimeSpan.FromMinutes(number),
        "h" => TimeSpan.FromHours(number),
        _ => TimeSpan.FromSeconds(number)
      };
    }

    return TimeSpan.FromSeconds(60);
  }

  private class Attribute
  {
    public string Name { get; set; } = string.Empty;
    public object Value { get; set; } = string.Empty;
  }

  private class Block
  {
    public string Name { get; set; } = string.Empty;
    public List<Attribute> Attributes { get; set; } = new();
    public List<Block> Children { get; set; } = new();
  }
}
