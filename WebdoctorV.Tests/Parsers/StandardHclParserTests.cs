using Xunit;
using WebdoctorV.Models;
using WebdoctorV.Parsers;

namespace WebdoctorV.Tests.Parsers;

public class StandardHclParserTests
{
  [Fact]
  public void ParseConfig_SimpleService_ShouldParse()
  {
    var config = @"
service ""my-service"" {
  name = ""Test Service""
  protocol = ""http""
  host = ""example.com""
  port = 80
}";

    var result = StandardHclParser.ParseConfig(config);

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
    var config = @"interval = ""60s""";

    var result = StandardHclParser.ParseConfig(config);

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
    var config = $@"interval = ""{timeSpanStr}""";

    var result = StandardHclParser.ParseConfig(config);

    Assert.NotNull(result.Interval);
    Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), result.Interval);
  }

  [Fact]
  public void ParseConfig_ServiceWithGroup_ShouldInheritAttributes()
  {
    var config = @"
group ""api"" {
  protocol = ""https""
  host = ""api.example.com""
  port = 443
  
  service ""health"" {
    name = ""Health Check""
    path = ""/health""
  }
}";

    var result = StandardHclParser.ParseConfig(config);

    Assert.Single(result.Services);
    var service = result.Services[0];
    Assert.Equal("api.health", service.Id);
    Assert.Equal("Health Check", service.Name);
    Assert.Equal("https", service.Protocol);
    Assert.Equal("api.example.com", service.Host);
    Assert.Equal(443, service.Port);
    Assert.Equal("/health", service.Path);
  }

  [Fact]
  public void ParseConfig_NestedGroups_ShouldBuildCorrectPath()
  {
    var config = @"
group ""api"" {
  path = ""/api""
  
  group ""v1"" {
    path = ""/v1""
    
    service ""users"" {
      name = ""Users Endpoint""
      path = ""/users""
    }
  }
}";

    var result = StandardHclParser.ParseConfig(config);

    Assert.Single(result.Services);
    var service = result.Services[0];
    Assert.Equal("api.v1.users", service.Id);
    Assert.Equal("/api/v1/users", service.Path);
  }

  [Fact]
  public void ParseConfig_ComplexHttpsExample_ShouldParse()
  {
    var config = @"
interval = ""60s""

group ""my-https-service"" {
  name = ""My HTTPS Service""
  protocol = ""https""
  host = ""my-https-service.com""
  port = 443
  timeout = ""30m""
  
  group ""api"" {
    method = ""GET""
    path = ""/api""
    
    service ""service1"" {
      name = ""Service 1""
      path = ""/service1""
      
      service ""health"" {
        path = ""/health""
      }
    }
  }
}";

    var result = StandardHclParser.ParseConfig(config);

    Assert.Equal(TimeSpan.FromSeconds(60), result.Interval);
    Assert.Single(result.Services);
    
    var service1 = result.Services[0];
    Assert.Equal("my-https-service.api.service1", service1.Id);
    Assert.Equal("Service 1", service1.Name);
    Assert.Equal("https", service1.Protocol);
    Assert.Equal("GET", service1.Method);
    Assert.Equal("/api/service1", service1.Path);
    
    // Nested service should be in Children
    Assert.Single(service1.Children);
    var health = service1.Children[0];
    Assert.Equal("my-https-service.api.service1.health", health.Id);
    Assert.Equal("/api/service1/health", health.Path);
  }

  [Fact]
  public void ParseConfig_WithComments_ShouldIgnoreComments()
  {
    var config = @"
# Global interval
interval = ""60s""

# Main service
service ""my-service"" {
  protocol = ""http""
  host = ""example.com"" # Host comment
  port = 80
}";

    var result = StandardHclParser.ParseConfig(config);

    Assert.NotNull(result.Interval);
    Assert.Equal(TimeSpan.FromSeconds(60), result.Interval);
    Assert.Single(result.Services);
    var service = result.Services[0];
    Assert.Equal("example.com", service.Host);
  }

  [Fact]
  public void ParseConfig_StringWithEscapedQuotes_ShouldParse()
  {
    var config = @"
service ""test"" {
  name = ""Test \""quoted\"" string""
  path = ""/test""
}";

    var result = StandardHclParser.ParseConfig(config);

    var service = result.Services[0];
    Assert.Contains("quoted", service.Name);
  }

  [Fact]
  public void ParseConfig_StringWithSpecialCharacters_ShouldParse()
  {
    var config = @"
service ""test"" {
  name = ""Test @#$%^&*() string""
  path = ""/test?param=value&other=123""
}";

    var result = StandardHclParser.ParseConfig(config);

    Assert.Single(result.Services);
    var service = result.Services[0];
    Assert.Contains("@#$%", service.Name);
    Assert.Contains("?param=value", service.Path);
  }

  [Fact]
  public void ParseConfig_EnvVariable_ShouldResolve()
  {
    Environment.SetEnvironmentVariable("TEST_VAR", "test_value");
    
    try
    {
      var config = @"
service ""test"" {
  name = ""Test Service""
  password = env(""TEST_VAR"")
}";

      var result = StandardHclParser.ParseConfig(config);

      Assert.Single(result.Services);
      var service = result.Services[0];
      Assert.Equal("test_value", service.Password);
    }
    finally
    {
      Environment.SetEnvironmentVariable("TEST_VAR", null);
    }
  }

  [Fact]
  public void ParseConfig_EnvVariableMissing_ShouldKeepOriginal()
  {
    var config = @"
service ""test"" {
  name = ""Test Service""
  password = env(""NONEXISTENT_VAR"")
}";

    var result = StandardHclParser.ParseConfig(config);

    Assert.Single(result.Services);
    var service = result.Services[0];
    // Should keep the env() expression if variable not found
    Assert.Equal("env(\"NONEXISTENT_VAR\")", service.Password);
  }

  [Fact]
  public void ParseConfig_ResponseBlock_ShouldParse()
  {
    var config = @"
service ""mysql-test"" {
  protocol = ""mysql""
  host = ""localhost""
  port = 3306
  
  response {
    rows = 1
    columns = 2
  }
}";

    var result = StandardHclParser.ParseConfig(config);

    Assert.Single(result.Services);
    var service = result.Services[0];
    Assert.NotNull(service.Response);
    Assert.Equal(1, service.Response.Rows);
    Assert.Equal(2, service.Response.Columns);
  }

  [Fact]
  public void ParseConfig_ExpectedStatus_ShouldParse()
  {
    var config = @"
service ""test"" {
  protocol = ""http""
  host = ""example.com""
  port = 80
  expected_status = 200
}";

    var result = StandardHclParser.ParseConfig(config);

    Assert.Single(result.Services);
    var service = result.Services[0];
    Assert.NotNull(service.Response);
    Assert.Equal(200, service.Response.Status);
  }

  [Fact]
  public void ParseConfig_EmptyConfig_ShouldReturnEmpty()
  {
    var config = "";

    var result = StandardHclParser.ParseConfig(config);

    Assert.NotNull(result);
    Assert.Empty(result.Services);
    Assert.Null(result.Interval);
  }

  [Fact]
  public void ParseConfig_ServiceWithoutProtocol_ShouldNotBeCheckable()
  {
    var config = @"
service ""test"" {
  name = ""Test Service""
  host = ""example.com""
  port = 80
}";

    var result = StandardHclParser.ParseConfig(config);

    // Service without protocol should not be added (not checkable)
    Assert.Empty(result.Services);
  }

  [Fact]
  public void ParseConfig_MultipleTopLevelServices_ShouldParseAll()
  {
    var config = @"
service ""service1"" {
  protocol = ""http""
  host = ""example1.com""
  port = 80
}

service ""service2"" {
  protocol = ""https""
  host = ""example2.com""
  port = 443
}";

    var result = StandardHclParser.ParseConfig(config);

    Assert.Equal(2, result.Services.Count);
    Assert.Equal("service1", result.Services[0].Id);
    Assert.Equal("service2", result.Services[1].Id);
  }

  [Fact]
  public void ParseConfig_PathInheritance_ShouldConcatenate()
  {
    var config = @"
group ""api"" {
  path = ""/api""
  
  group ""v1"" {
    path = ""/v1""
    
    service ""users"" {
      path = ""/users""
    }
  }
}";

    var result = StandardHclParser.ParseConfig(config);

    Assert.Single(result.Services);
    var service = result.Services[0];
    Assert.Equal("/api/v1/users", service.Path);
  }

  [Fact]
  public void ParseConfig_PathInheritanceEmptyServicePath_ShouldUseGroupPath()
  {
    var config = @"
group ""api"" {
  path = ""/api""
  
  service ""base"" {
    path = """"
  }
}";

    var result = StandardHclParser.ParseConfig(config);

    Assert.Single(result.Services);
    var service = result.Services[0];
    Assert.Equal("/api", service.Path);
  }
}
