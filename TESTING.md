# Testing Guide

## Running Tests

### Run All Tests

```powershell
.\test.ps1
```

### Run HCL Parser Tests Only

```powershell
.\test-hcl.ps1
```

### Run Tests with Filter

```powershell
.\test.ps1 -Filter "FullyQualifiedName~HclParserTests"
```

### Run Tests with Custom Verbosity

```powershell
.\test.ps1 -Verbosity detailed
.\test-hcl.ps1 -Verbosity detailed
```

## Docker Compose Test Service

Tests are run in a separate Docker service defined in `docker-compose.yml`:

```yaml
tests:
  image: mcr.microsoft.com/dotnet/sdk:8.0
  profiles:
    - test
```

The test service uses a separate volume for build outputs to avoid file locking issues.

## Manual Docker Commands

You can also run tests manually:

```powershell
# Run all tests
docker compose --profile test run --rm tests

# Run specific test filter
docker compose --profile test run --rm tests dotnet test WebdoctorV.Tests/WebdoctorV.Tests.csproj --filter "FullyQualifiedName~HclParserTests"

# Run with detailed output
docker compose --profile test run --rm tests dotnet test WebdoctorV.Tests/WebdoctorV.Tests.csproj --verbosity detailed
```

## Test Results

Current test status:
- ✅ 32 tests passing
- ❌ 16 tests failing (parser improvements needed)

The failing tests are mostly related to:
- Multiple services parsing
- Deep nesting scenarios
- Edge cases with attribute parsing

These will be fixed as the parser is refined.
