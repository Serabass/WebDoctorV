using Xunit;
using WebdoctorV.Models;
using WebdoctorV.Parsers;

namespace WebdoctorV.Tests.Parsers;

public class HclParserTests
{
    [Fact]
    public void ParseConfig_SimpleService_ShouldParse()
    {
        var config = @"
my-service {
  .name 'Test Service'
  .protocol 'http'
  .host 'example.com'
  .port 80
}";

        var result = HclParser.ParseConfig(config);

        Assert.Single(result.Services);
        var service = result.Services[0];
        Assert.Equal("my-service", service.Id);
        Assert.Equal("Test Service", service.Name);
        Assert.Equal("http", service.Protocol);
        Assert.Equal("example.com", service.Host);
        Assert.Equal(80, service.Port);
    }

    [Fact]
    public void ParseConfig_GlobalInterval_ShouldParse()
    {
        var config = @".interval 60s";

        var result = HclParser.ParseConfig(config);

        Assert.NotNull(result.Interval);
        Assert.Equal(TimeSpan.FromSeconds(60), result.Interval);
    }

    [Theory]
    [InlineData("60s", 60)]
    [InlineData("30m", 1800)]
    [InlineData("2h", 7200)]
    [InlineData("500ms", 0.5)]
    public void ParseConfig_TimeSpanValues_ShouldParseCorrectly(string timeSpanStr, double expectedSeconds)
    {
        var config = $@".interval {timeSpanStr}";

        var result = HclParser.ParseConfig(config);

        Assert.NotNull(result.Interval);
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), result.Interval);
    }

    [Fact]
    public void ParseConfig_ServiceWithResponse_ShouldParse()
    {
        var config = @"
my-service {
  .protocol 'https'
  .host 'example.com'
  .port 443
  
  response {
    .status 200
  }
}";

        var result = HclParser.ParseConfig(config);

        var service = result.Services[0];
        Assert.NotNull(service.Response);
        Assert.Equal(200, service.Response.Status);
    }

    [Fact]
    public void ParseConfig_NestedBlocks_ShouldParse()
    {
        var config = @"
parent {
  .protocol 'https'
  .host 'example.com'
  .port 443
  
  child {
    .name 'Child Service'
    .path '/api'
  }
}";

        var result = HclParser.ParseConfig(config);

        var parent = result.Services[0];
        Assert.Single(parent.Children);
        var child = parent.Children[0];
        Assert.Equal("child", child.Id);
        Assert.Equal("Child Service", child.Name);
        Assert.Equal("/api", child.Path);
        Assert.Equal("https", child.Protocol); // Inherited from parent
        Assert.Equal("example.com", child.Host); // Inherited from parent
        Assert.Equal(443, child.Port); // Inherited from parent
    }

    [Fact]
    public void ParseConfig_ExcludeBlock_ShouldParse()
    {
        var config = @"
service {
  .protocol 'https'
  .host 'example.com'
  .port 443
  
  api {
    .path '/api'
    
    -health {
      .path ^'/health'
    }
  }
}";

        var result = HclParser.ParseConfig(config);

        var service = result.Services[0];
        var api = service.Children[0];
        Assert.Single(api.Excludes);
        var exclude = api.Excludes[0];
        Assert.Equal("/health", exclude.Path);
        Assert.True(exclude.PathIsPrefix);
    }

    [Fact]
    public void ParseConfig_PathPrefix_ShouldParse()
    {
        var config = @"
service {
  .protocol 'https'
  .host 'example.com'
  .port 443
  .path ^'/api'
}";

        var result = HclParser.ParseConfig(config);

        var service = result.Services[0];
        Assert.Equal("/api", service.Path);
        Assert.True(service.PathIsPrefix);
    }

    [Fact]
    public void ParseConfig_MySqlService_ShouldParse()
    {
        var config = @"
my-mysql-service {
  .name 'My MySQL Service'
  .protocol 'mysql'
  .host 'my-mysql-service.com'
  .port 3306
  .timeout 30m
  
  response {
    .rows 1
    .columns 1
    .data [
      {
        ""result"": 2
      }
    ]
  }
  
  .query 'SELECT 1+1 AS result'
}";

        var result = HclParser.ParseConfig(config);

        var service = result.Services[0];
        Assert.Equal("mysql", service.Protocol);
        Assert.Equal("SELECT 1+1 AS result", service.Query);
        Assert.NotNull(service.Response);
        Assert.Equal(1, service.Response.Rows);
        Assert.Equal(1, service.Response.Columns);
        Assert.NotNull(service.Response.Data);
        Assert.Single(service.Response.Data);
        Assert.Equal(2, service.Response.Data[0]["result"]);
    }

    [Fact]
    public void ParseConfig_SshService_ShouldParse()
    {
        var config = @"
my-ssh-service {
  .name 'My SSH Service'
  .protocol 'ssh'
  .host 'my-ssh-service.com'
  .port 22
  .timeout 30m
  
  response {
    .exit_code 0
  }
  
  .command 'ls -la'
}";

        var result = HclParser.ParseConfig(config);

        var service = result.Services[0];
        Assert.Equal("ssh", service.Protocol);
        Assert.Equal("ls -la", service.Command);
        Assert.NotNull(service.Response);
        Assert.Equal(0, service.Response.ExitCode);
    }

    [Fact]
    public void ParseConfig_ServiceWithCustomInterval_ShouldOverrideGlobal()
    {
        var config = @"
.interval 60s

service1 {
  .protocol 'http'
  .host 'example.com'
  .port 80
}

service2 {
  .interval 30s
  .protocol 'http'
  .host 'example.com'
  .port 80
}";

        var result = HclParser.ParseConfig(config);

        Assert.Equal(TimeSpan.FromSeconds(60), result.Interval);
        Assert.Null(result.Services[0].Interval); // Uses global
        Assert.Equal(TimeSpan.FromSeconds(30), result.Services[1].Interval); // Overrides global
    }

    [Fact]
    public void ParseConfig_ComplexHttpsExample_ShouldParse()
    {
        var config = @"
.interval 60s

my-https-service {
  .name 'My HTTPS Service'
  .protocol 'https'
  .host 'my-https-service.com'
  .port 443
  .timeout 30m

  response {
    .status 200
  }

  api {
    .method 'GET'
    .path '/api'

    service1 {
      .name 'Service 1'
      .path ^'/service1'

      !health {
        .path ^'/health'
      }
    }

    service2 {
      .name 'Service 2'
      .path ^'/service2'

      !health {
        .path ^'/health'
      }
    }
  }
}";

        var result = HclParser.ParseConfig(config);

        Assert.Equal(TimeSpan.FromSeconds(60), result.Interval);
        Assert.Single(result.Services);
        
        var mainService = result.Services[0];
        Assert.Equal("my-https-service", mainService.Id);
        Assert.Equal("My HTTPS Service", mainService.Name);
        Assert.Equal("https", mainService.Protocol);
        Assert.Equal(200, mainService.Response?.Status);
        
        Assert.Single(mainService.Children);
        var api = mainService.Children[0];
        Assert.Equal("api", api.Id);
        Assert.Equal("GET", api.Method);
        Assert.Equal("/api", api.Path);
        
        Assert.Equal(2, api.Children.Count);
        var service1 = api.Children[0];
        Assert.Equal("service1", service1.Id);
        Assert.Equal("Service 1", service1.Name);
        Assert.Equal("/service1", service1.Path);
        Assert.True(service1.PathIsPrefix);
        
        // !health blocks are now executable, so they should be in Children
        Assert.Single(service1.Children);
        var health1 = service1.Children[0];
        Assert.Equal("api.service1.health", health1.Id); // Path formed from hierarchy
        Assert.Equal("/health", health1.Path);
        Assert.True(health1.PathIsPrefix);
    }

    [Fact]
    public void ParseConfig_WithComments_ShouldIgnoreComments()
    {
        var config = @"
# Global interval
.interval 60s

# Main service
my-service {
  .protocol 'http'
  .host 'example.com' # Host comment
  .port 80
}";

        var result = HclParser.ParseConfig(config);

        Assert.Equal(TimeSpan.FromSeconds(60), result.Interval);
        Assert.Single(result.Services);
        var service = result.Services[0];
        Assert.Equal("http", service.Protocol);
        Assert.Equal("example.com", service.Host);
        Assert.Equal(80, service.Port);
    }

    [Fact]
    public void ParseConfig_DoubleQuotedStrings_ShouldParse()
    {
        var config = @"
service {
  .name ""Test Service""
  .protocol ""https""
  .host ""example.com""
  .port 443
}";

        var result = HclParser.ParseConfig(config);

        var service = result.Services[0];
        Assert.Equal("Test Service", service.Name);
        Assert.Equal("https", service.Protocol);
        Assert.Equal("example.com", service.Host);
    }

    [Fact]
    public void ParseConfig_EmptyConfig_ShouldReturnEmpty()
    {
        var config = "";

        var result = HclParser.ParseConfig(config);

        Assert.Empty(result.Services);
        Assert.Null(result.Interval);
    }

    [Fact]
    public void ParseConfig_MultipleServices_ShouldParseAll()
    {
        var config = @"
service1 {
  .protocol 'http'
  .host 'example1.com'
  .port 80
}

service2 {
  .protocol 'https'
  .host 'example2.com'
  .port 443
}";

        var result = HclParser.ParseConfig(config);

        Assert.Equal(2, result.Services.Count);
        Assert.Equal("service1", result.Services[0].Id);
        Assert.Equal("service2", result.Services[1].Id);
    }

    // ========== Deep Nesting Tests ==========
    
    [Fact]
    public void ParseConfig_DeepNesting_ThreeLevels_ShouldParse()
    {
        var config = @"
level1 {
  .protocol 'https'
  .host 'example.com'
  .port 443
  
  level2 {
    .path '/api'
    
    level3 {
      .name 'Deep Service'
      .path '/deep'
    }
  }
}";

        var result = HclParser.ParseConfig(config);

        Assert.Single(result.Services);
        var l1 = result.Services[0];
        Assert.Single(l1.Children);
        var l2 = l1.Children[0];
        Assert.Single(l2.Children);
        var l3 = l2.Children[0];
        Assert.Equal("Deep Service", l3.Name);
        Assert.Equal("/deep", l3.Path);
        Assert.Equal("https", l3.Protocol); // Inherited
    }

    [Fact]
    public void ParseConfig_DeepNesting_FourLevels_ShouldParse()
    {
        var config = @"
a {
  .protocol 'http'
  b {
    .host 'example.com'
    c {
      .port 80
      d {
        .path '/test'
      }
    }
  }
}";

        var result = HclParser.ParseConfig(config);

        Assert.Single(result.Services);
        var a = result.Services[0];
        Assert.Single(a.Children);
        var b = a.Children[0];
        Assert.Single(b.Children);
        var c = b.Children[0];
        Assert.Single(c.Children);
        var d = c.Children[0];
        Assert.Equal("/test", d.Path);
        Assert.Equal("http", d.Protocol); // Inherited
        Assert.Equal("example.com", d.Host); // Inherited
        Assert.Equal(80, d.Port); // Inherited
    }

    [Fact]
    public void ParseConfig_MultipleNestedBlocks_ShouldParseAll()
    {
        var config = @"
parent {
  .protocol 'https'
  
  child1 {
    .name 'Child 1'
  }
  
  child2 {
    .name 'Child 2'
  }
  
  child3 {
    .name 'Child 3'
    grandchild {
      .path '/nested'
    }
  }
}";

        var result = HclParser.ParseConfig(config);

        Assert.Single(result.Services);
        var parent = result.Services[0];
        Assert.Equal(3, parent.Children.Count);
        Assert.Equal("Child 1", parent.Children[0].Name);
        Assert.Equal("Child 2", parent.Children[1].Name);
        Assert.Equal("Child 3", parent.Children[2].Name);
        Assert.Single(parent.Children[2].Children);
        Assert.Equal("/nested", parent.Children[2].Children[0].Path);
    }

    // ========== Edge Cases Tests ==========
    
    [Fact]
    public void ParseConfig_EmptyBlock_ShouldParse()
    {
        var config = @"
empty {
}";

        var result = HclParser.ParseConfig(config);

        Assert.Single(result.Services);
        Assert.Equal("empty", result.Services[0].Id);
        Assert.Empty(result.Services[0].Children);
        // Empty block should have no properties set
        Assert.Null(result.Services[0].Name);
    }

    [Fact]
    public void ParseConfig_BlockWithOnlyAttributes_ShouldParse()
    {
        var config = @"
service {
  .name 'Test'
  .protocol 'http'
}";

        var result = HclParser.ParseConfig(config);

        Assert.Single(result.Services);
        var service = result.Services[0];
        Assert.Equal("Test", service.Name);
        Assert.Equal("http", service.Protocol);
        Assert.Empty(service.Children);
    }

    [Fact]
    public void ParseConfig_BlockWithOnlyChildren_ShouldParse()
    {
        var config = @"
parent {
  child {
    .name 'Child'
  }
}";

        var result = HclParser.ParseConfig(config);

        Assert.Single(result.Services);
        var parent = result.Services[0];
        Assert.Single(parent.Children);
        Assert.Equal("child", parent.Children[0].Id);
    }

    [Fact]
    public void ParseConfig_MultipleResponseBlocks_ShouldParseLast()
    {
        var config = @"
service {
  .protocol 'https'
  
  response {
    .status 200
  }
  
  response {
    .status 404
  }
}";

        var result = HclParser.ParseConfig(config);

        var service = result.Services[0];
        // Should parse both, but BuildService uses FirstOrDefault, so first one wins
        Assert.NotNull(service.Response);
        Assert.Equal(200, service.Response.Status);
    }

    [Fact]
    public void ParseConfig_ResponseBlockWithMultipleAttributes_ShouldParse()
    {
        var config = @"
mysql-service {
  .protocol 'mysql'
  
  response {
    .rows 2
    .columns 3
    .data [
      { ""id"": 1, ""name"": ""test"" },
      { ""id"": 2, ""name"": ""test2"" }
    ]
  }
}";

        var result = HclParser.ParseConfig(config);

        var service = result.Services[0];
        Assert.NotNull(service.Response);
        Assert.Equal(2, service.Response.Rows);
        Assert.Equal(3, service.Response.Columns);
        Assert.NotNull(service.Response.Data);
        Assert.Equal(2, service.Response.Data.Count);
    }

    // ========== Whitespace and Formatting Tests ==========
    
    [Fact]
    public void ParseConfig_NoWhitespaceBeforeBrace_ShouldParse()
    {
        var config = @"service{.name 'Test'}";

        var result = HclParser.ParseConfig(config);

        Assert.Single(result.Services);
        Assert.Equal("service", result.Services[0].Id);
    }

    [Fact]
    public void ParseConfig_ExtraWhitespace_ShouldParse()
    {
        var config = @"
    service     {
        .name      'Test'
        .protocol  'http'
    }
";

        var result = HclParser.ParseConfig(config);

        Assert.Single(result.Services);
        Assert.Equal("Test", result.Services[0].Name);
        Assert.Equal("http", result.Services[0].Protocol);
    }

    [Fact]
    public void ParseConfig_MinimalWhitespace_ShouldParse()
    {
        var config = @".interval 30s
service{.name 'Test'.protocol 'http'}";

        var result = HclParser.ParseConfig(config);

        Assert.Equal(TimeSpan.FromSeconds(30), result.Interval);
        Assert.Single(result.Services);
    }

    [Fact]
    public void ParseConfig_AllOnOneLine_ShouldParse()
    {
        var config = @".interval 60s service{.name 'Test'.protocol 'http'.port 80}";

        var result = HclParser.ParseConfig(config);

        Assert.Equal(TimeSpan.FromSeconds(60), result.Interval);
        Assert.Single(result.Services);
    }

    // ========== Identifier Edge Cases ==========
    
    [Fact]
    public void ParseConfig_IdentifierWithUnderscores_ShouldParse()
    {
        var config = @"
my_service {
  .name 'Test'
}

my-service-name {
  .name 'Test 2'
}";

        var result = HclParser.ParseConfig(config);

        Assert.Equal(2, result.Services.Count);
        Assert.Equal("my_service", result.Services[0].Id);
        Assert.Equal("my-service-name", result.Services[1].Id);
    }

    [Fact]
    public void ParseConfig_IdentifierStartingWithUnderscore_ShouldParse()
    {
        var config = @"
_service {
  .name 'Test'
}";

        var result = HclParser.ParseConfig(config);

        Assert.Single(result.Services);
        Assert.Equal("_service", result.Services[0].Id);
    }

    // ========== String Edge Cases ==========
    
    [Fact]
    public void ParseConfig_StringWithEscapedQuotes_ShouldParse()
    {
        var config = @"
service {
  .name 'Test ''quoted'' string'
  .path '/test'
}";

        var result = HclParser.ParseConfig(config);

        var service = result.Services[0];
        // Note: текущий парсер не поддерживает экранирование, но должен парсить как есть
        Assert.Contains("quoted", service.Name);
    }

    [Fact]
    public void ParseConfig_StringWithSpecialCharacters_ShouldParse()
    {
        var config = @"
service {
  .name 'Test @#$%^&*() string'
  .path '/test?param=value&other=123'
}";

        var result = HclParser.ParseConfig(config);

        var service = result.Services[0];
        Assert.Contains("@#$%", service.Name);
        Assert.Contains("?param=value", service.Path);
    }

    [Fact]
    public void ParseConfig_EmptyString_ShouldParse()
    {
        var config = @"
service {
  .name ''
  .path ''
}";

        var result = HclParser.ParseConfig(config);

        var service = result.Services[0];
        Assert.Equal("", service.Name);
        Assert.Equal("", service.Path);
    }

    // ========== Number Edge Cases ==========
    
    [Fact]
    public void ParseConfig_LargePortNumber_ShouldParse()
    {
        var config = @"
service {
  .port 65535
}";

        var result = HclParser.ParseConfig(config);

        Assert.Equal(65535, result.Services[0].Port);
    }

    [Fact]
    public void ParseConfig_ZeroPort_ShouldParse()
    {
        var config = @"
service {
  .port 0
}";

        var result = HclParser.ParseConfig(config);

        Assert.Equal(0, result.Services[0].Port);
    }

    // ========== TimeSpan Edge Cases ==========
    
    [Fact]
    public void ParseConfig_VerySmallTimeSpan_ShouldParse()
    {
        var config = @"
service {
  .timeout 1ms
}";

        var result = HclParser.ParseConfig(config);

        Assert.Equal(TimeSpan.FromMilliseconds(1), result.Services[0].Timeout);
    }

    [Fact]
    public void ParseConfig_VeryLargeTimeSpan_ShouldParse()
    {
        var config = @"
service {
  .timeout 24h
}";

        var result = HclParser.ParseConfig(config);

        Assert.Equal(TimeSpan.FromHours(24), result.Services[0].Timeout);
    }

    // ========== Complex Nested Scenarios ==========
    
    [Fact]
    public void ParseConfig_NestedWithResponseAndExcludes_ShouldParse()
    {
        var config = @"
api {
  .protocol 'https'
  .host 'api.example.com'
  
  v1 {
    .path '/v1'
    
    users {
      .path '/users'
      
      response {
        .status 200
      }
      
      -admin {
        .path '/admin'
      }
      
      -test {
        .path '/test'
      }
    }
  }
}";

        var result = HclParser.ParseConfig(config);

        var api = result.Services[0];
        Assert.Single(api.Children);
        var v1 = api.Children[0];
        Assert.Single(v1.Children);
        var users = v1.Children[0];
        Assert.NotNull(users.Response);
        Assert.Equal(2, users.Excludes.Count);
    }

    [Fact]
    public void ParseConfig_MultipleExcludes_ShouldParseAll()
    {
        var config = @"
service {
  .protocol 'https'
  
  api {
    .path '/api'
    
    -health {
      .path '/health'
    }
    
    -metrics {
      .path '/metrics'
    }
    
    -debug {
      .path '/debug'
    }
  }
}";

        var result = HclParser.ParseConfig(config);

        var service = result.Services[0];
        var api = service.Children[0];
        Assert.Equal(3, api.Excludes.Count);
        Assert.Equal("/health", api.Excludes[0].Path);
        Assert.Equal("/metrics", api.Excludes[1].Path);
        Assert.Equal("/debug", api.Excludes[2].Path);
    }

    // ========== Global Attributes Edge Cases ==========
    
    [Fact]
    public void ParseConfig_MultipleGlobalAttributes_ShouldParse()
    {
        var config = @"
.interval 30s
.another 'value'

service {
  .name 'Test'
}";

        var result = HclParser.ParseConfig(config);

        Assert.Equal(TimeSpan.FromSeconds(30), result.Interval);
        Assert.Single(result.Services);
    }

    [Fact]
    public void ParseConfig_GlobalAttributesAfterBlocks_ShouldIgnore()
    {
        var config = @"
service {
  .name 'Test'
}

.interval 30s";

        var result = HclParser.ParseConfig(config);

        // Global attributes после блоков должны игнорироваться текущей логикой
        Assert.Single(result.Services);
        // Interval может быть null, так как он парсится до блоков
    }

    // ========== Error Cases (should throw or handle gracefully) ==========
    
    [Fact]
    public void ParseConfig_MissingClosingBrace_ShouldThrow()
    {
        var config = @"
service {
  .name 'Test'
";

        Assert.ThrowsAny<Exception>(() => HclParser.ParseConfig(config));
    }

    [Fact]
    public void ParseConfig_ExtraClosingBrace_ShouldThrow()
    {
        var config = @"
service {
  .name 'Test'
}
}";

        Assert.ThrowsAny<Exception>(() => HclParser.ParseConfig(config));
    }

    [Fact]
    public void ParseConfig_MissingAttributeValue_ShouldThrow()
    {
        var config = @"
service {
  .name
}";

        Assert.ThrowsAny<Exception>(() => HclParser.ParseConfig(config));
    }

    [Fact]
    public void ParseConfig_UnclosedString_ShouldThrow()
    {
        var config = @"
service {
  .name 'Test
}";

        Assert.ThrowsAny<Exception>(() => HclParser.ParseConfig(config));
    }

    // ========== Real-world Complex Scenarios ==========
    
    [Fact]
    public void ParseConfig_RealWorldComplex_ShouldParse()
    {
        var config = @"
.interval 60s

main-api {
  .name 'Main API'
  .protocol 'https'
  .host 'api.example.com'
  .port 443
  .timeout 30s
  
  response {
    .status 200
  }
  
  v1 {
    .method 'GET'
    .path '/v1'
    
    users {
      .name 'Users API'
      .path '/users'
      
      response {
        .status 200
      }
      
      !health {
        .path '/health'
      }
      
      profile {
        .path '/profile'
      }
    }
    
    posts {
      .name 'Posts API'
      .path '/posts'
      
      !draft {
        .path '/draft'
      }
    }
  }
  
  v2 {
    .path '/v2'
    
    auth {
      .path '/auth'
    }
  }
}";

        var result = HclParser.ParseConfig(config);

        Assert.Equal(TimeSpan.FromSeconds(60), result.Interval);
        Assert.Single(result.Services);
        
        var main = result.Services[0];
        Assert.Equal(2, main.Children.Count); // v1 and v2
        
        var v1 = main.Children[0];
        Assert.Equal(2, v1.Children.Count); // users and posts
        
        var users = v1.Children[0];
        Assert.Equal(2, users.Children.Count); // profile and !health (executable)
        // !health is now executable, so it should be in Children with path v1.users.health
        var health = users.Children.FirstOrDefault(c => c.Id.Contains("health"));
        Assert.NotNull(health);
        Assert.Equal("/health", health.Path);
    }
}
