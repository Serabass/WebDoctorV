# Руководство по разработке плагинов для WebdoctorV

Это руководство поможет вам создать собственный чекер-плагин для WebdoctorV без необходимости изменять основной код проекта.

## Содержание

- [Введение](#введение)
- [Быстрый старт](#быстрый-старт)
- [Архитектура плагина](#архитектура-плагина)
- [Создание плагина](#создание-плагина)
- [Интерфейс IChecker](#интерфейс-ichecker)
- [Работа с ServiceConfig](#работа-с-serviceconfig)
- [Возврат результатов](#возврат-результатов)
- [Расширенные возможности](#расширенные-возможности)
- [Тестирование плагина](#тестирование-плагина)
- [Упаковка и распространение](#упаковка-и-распространение)
- [Примеры](#примеры)
- [Часто задаваемые вопросы](#часто-задаваемые-вопросы)

## Введение

Плагины WebdoctorV позволяют расширять функциональность системы мониторинга, добавляя поддержку новых протоколов и типов проверок. Плагины загружаются динамически из папки `plugins/` и автоматически регистрируются в системе.

### Преимущества плагинной системы

- ✅ Не нужно форкать основной репозиторий
- ✅ Обновления WebdoctorV не ломают ваши плагины
- ✅ Легко распространять через NuGet или GitHub
- ✅ Изоляция - сломанный плагин не уронит систему
- ✅ Простая структура - всего один интерфейс

## Быстрый старт

### Шаг 1: Создание проекта

Создайте новый проект библиотеки классов (.NET 6.0 или выше):

```bash
dotnet new classlib -n WebdoctorV.Plugin.MyChecker
cd WebdoctorV.Plugin.MyChecker
```

### Шаг 2: Добавление зависимостей

Добавьте ссылку на WebdoctorV (через NuGet или проект):

```bash
# Если WebdoctorV опубликован как NuGet пакет
dotnet add package WebdoctorV.Plugin.SDK

# Или добавьте ссылку на проект (для локальной разработки)
dotnet add reference ../WebdoctorV/WebdoctorV.csproj
```

### Шаг 3: Создание чекера

Создайте файл `MyChecker.cs`:

```csharp
using WebdoctorV.Checkers;
using WebdoctorV.Models;

namespace WebdoctorV.Plugin.MyChecker;

public class MyChecker : IChecker
{
    public bool Supports(string protocol)
    {
        return protocol.Equals("myprotocol", StringComparison.OrdinalIgnoreCase);
    }

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
            // Ваша логика проверки здесь
            // ...

            result.Status = CheckStatus.Alive;
            result.Duration = DateTime.UtcNow - startTime;
            result.LastCheck = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            result.Status = CheckStatus.Dead;
            result.Error = ex.Message;
            result.Duration = DateTime.UtcNow - startTime;
            result.LastCheck = DateTime.UtcNow;
        }

        return result;
    }
}
```

### Шаг 4: Компиляция

```bash
dotnet build -c Release
```

### Шаг 5: Установка плагина

Скопируйте скомпилированный `.dll` файл в папку `plugins/` рядом с исполняемым файлом WebdoctorV:

```
webdoctorv/
  ├── WebdoctorV.exe (или .dll)
  ├── plugins/
  │   └── WebdoctorV.Plugin.MyChecker.dll
  └── config.hcl
```

Готово! WebdoctorV автоматически обнаружит и загрузит ваш плагин при запуске.

## Архитектура плагина

### Структура проекта

```
WebdoctorV.Plugin.MyChecker/
  ├── MyChecker.csproj          # Файл проекта
  ├── MyChecker.cs               # Основной класс чекера
  ├── README.md                  # Документация плагина
  └── Properties/
      └── AssemblyInfo.cs       # Метаданные сборки (опционально)
```

### Требования к проекту

- **Target Framework**: .NET 6.0 или выше
- **Тип проекта**: Class Library
- **Зависимости**: Только `WebdoctorV.Plugin.SDK` (или ссылка на основной проект)

### Именование

Рекомендуется использовать следующую схему именования:
- Проект: `WebdoctorV.Plugin.{ProtocolName}Checker`
- Namespace: `WebdoctorV.Plugin.{ProtocolName}`
- Класс: `{ProtocolName}Checker`

Примеры:
- `WebdoctorV.Plugin.RedisChecker`
- `WebdoctorV.Plugin.MongoDBChecker`
- `WebdoctorV.Plugin.ElasticsearchChecker`

## Создание плагина

### Файл проекта (.csproj)

Минимальный пример `MyChecker.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <AssemblyName>WebdoctorV.Plugin.MyChecker</AssemblyName>
    <RootNamespace>WebdoctorV.Plugin.MyChecker</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="WebdoctorV.Plugin.SDK" Version="1.0.0" />
  </ItemGroup>
</Project>
```

### Основной класс чекера

Каждый плагин должен содержать класс, реализующий интерфейс `IChecker`:

```csharp
using WebdoctorV.Checkers;
using WebdoctorV.Models;

namespace WebdoctorV.Plugin.MyChecker;

public class MyChecker : IChecker
{
    // Реализация интерфейса
}
```

## Интерфейс IChecker

Интерфейс `IChecker` состоит из двух методов:

### bool Supports(string protocol)

Определяет, поддерживает ли чекер указанный протокол. Вызывается для каждого сервиса в конфигурации.

**Параметры:**
- `protocol` - название протокола из конфигурации (например, "redis", "mongodb", "myprotocol")

**Возвращает:**
- `true` - если чекер поддерживает этот протокол
- `false` - в противном случае

**Пример:**

```csharp
public bool Supports(string protocol)
{
    return protocol.Equals("redis", StringComparison.OrdinalIgnoreCase) ||
           protocol.Equals("rediss", StringComparison.OrdinalIgnoreCase); // Redis over SSL
}
```

### Task<CheckResult> CheckAsync(ServiceConfig service, string path)

Выполняет проверку сервиса.

**Параметры:**
- `service` - конфигурация сервиса из HCL файла
- `path` - путь к сервису в иерархии конфигурации (например, "databases.redis")

**Возвращает:**
- `CheckResult` - результат проверки

**Пример:**

```csharp
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
        // Выполнение проверки
        var isHealthy = await PerformCheck(service);

        result.Status = isHealthy ? CheckStatus.Alive : CheckStatus.Dead;
        result.Duration = DateTime.UtcNow - startTime;
        result.LastCheck = DateTime.UtcNow;
    }
    catch (Exception ex)
    {
        result.Status = CheckStatus.Dead;
        result.Error = ex.Message;
        result.Duration = DateTime.UtcNow - startTime;
        result.LastCheck = DateTime.UtcNow;
    }

    return result;
}
```

## Работа с ServiceConfig

`ServiceConfig` содержит всю информацию о сервисе из конфигурации:

### Базовые свойства

```csharp
public class ServiceConfig
{
    public string Id { get; set; }              // ID сервиса
    public string? Name { get; set; }           // Отображаемое имя
    public string Protocol { get; set; }         // Протокол ("redis", "mongodb", etc.)
    public string Host { get; set; }             // Хост
    public int Port { get; set; }                // Порт
    public TimeSpan? Timeout { get; set; }       // Таймаут проверки
    public int RetryCount { get; set; }          // Количество повторов
    public TimeSpan? RetryDelay { get; set; }    // Задержка между повторами
}
```

### Использование таймаутов

Всегда используйте `service.Timeout` для ограничения времени выполнения проверки:

```csharp
var timeout = service.Timeout ?? TimeSpan.FromSeconds(30);
using var cts = new CancellationTokenSource(timeout);

try
{
    await SomeAsyncOperation(cts.Token);
}
catch (OperationCanceledException)
{
    result.Status = CheckStatus.Dead;
    result.Error = "Operation timeout";
}
```

### Кастомные свойства

Для хранения специфичных для протокола данных используйте дополнительные поля в `ServiceConfig`. Например, для Redis можно использовать:

```csharp
// В конфигурации HCL
service "redis-cache" {
  protocol = "redis"
  host = "redis.example.com"
  port = 6379
  password = "secret"
  database = 0
}

// В чекере
var password = service.Password; // Если добавлено в ServiceConfig
var database = service.Database; // Если добавлено в ServiceConfig
```

**Примечание:** Если вам нужны дополнительные поля в `ServiceConfig`, создайте issue в основном репозитории или используйте `AdditionalInfo` в `CheckResult`.

## Возврат результатов

### Структура CheckResult

```csharp
public class CheckResult
{
    public string ServiceId { get; set; }        // ID сервиса
    public string Path { get; set; }             // Путь в конфигурации
    public string FullHttpPath { get; set; }      // Полный HTTP путь (для HTTP чекеров)
    public string Protocol { get; set; }         // Протокол
    public string Name { get; set; }             // Имя сервиса
    public string Host { get; set; }             // Хост
    public int Port { get; set; }                // Порт
    public string? AdditionalInfo { get; set; }  // Дополнительная информация
    public CheckStatus Status { get; set; }      // Статус проверки
    public TimeSpan? Duration { get; set; }       // Длительность проверки
    public DateTime? LastCheck { get; set; }      // Время последней проверки
    public string? Error { get; set; }           // Сообщение об ошибке
}
```

### Статусы проверки

```csharp
public enum CheckStatus
{
    Pending = -1,  // Ожидает проверки
    Dead = 0,      // Сервис недоступен
    Alive = 1      // Сервис доступен
}
```

### Обязательные поля

Всегда заполняйте следующие поля:

```csharp
result.ServiceId = service.Id;
result.Path = path;
result.Protocol = service.Protocol;
result.Name = service.Name ?? service.Id;
result.Host = service.Host;
result.Port = service.Port;
result.Status = CheckStatus.Alive; // или Dead
result.Duration = DateTime.UtcNow - startTime;
result.LastCheck = DateTime.UtcNow;
```

### Дополнительная информация

Используйте `AdditionalInfo` для хранения специфичной для протокола информации:

```csharp
// Для Redis
result.AdditionalInfo = $"Database: {database}, Keys: {keyCount}";

// Для MongoDB
result.AdditionalInfo = $"Collections: {collectionCount}, Documents: {docCount}";
```

### Обработка ошибок

Всегда обрабатывайте исключения и устанавливайте соответствующий статус:

```csharp
try
{
    // Проверка
    result.Status = CheckStatus.Alive;
}
catch (TimeoutException ex)
{
    result.Status = CheckStatus.Dead;
    result.Error = $"Timeout: {ex.Message}";
}
catch (ConnectionException ex)
{
    result.Status = CheckStatus.Dead;
    result.Error = $"Connection failed: {ex.Message}";
}
catch (Exception ex)
{
    result.Status = CheckStatus.Dead;
    result.Error = ex.Message;
}
finally
{
    result.Duration = DateTime.UtcNow - startTime;
    result.LastCheck = DateTime.UtcNow;
}
```

## Расширенные возможности

### Использование зависимостей

Плагины могут использовать любые NuGet пакеты. Например, для работы с Redis:

```xml
<ItemGroup>
  <PackageReference Include="StackExchange.Redis" Version="2.6.90" />
</ItemGroup>
```

```csharp
using StackExchange.Redis;

public class RedisChecker : IChecker
{
    public async Task<CheckResult> CheckAsync(ServiceConfig service, string path)
    {
        // Использование StackExchange.Redis
        var connection = await ConnectionMultiplexer.ConnectAsync(...);
        // ...
    }
}
```

### Кэширование соединений

Для оптимизации производительности можно кэшировать соединения:

```csharp
public class RedisChecker : IChecker
{
    private static readonly Dictionary<string, ConnectionMultiplexer> _connections = new();

    public async Task<CheckResult> CheckAsync(ServiceConfig service, string path)
    {
        var connectionKey = $"{service.Host}:{service.Port}";
        
        if (!_connections.TryGetValue(connectionKey, out var connection))
        {
            connection = await ConnectionMultiplexer.ConnectAsync(...);
            _connections[connectionKey] = connection;
        }

        // Использование соединения
    }
}
```

**Внимание:** Учитывайте, что плагины могут быть выгружены из памяти. Используйте слабые ссылки или периодически проверяйте соединения.

### Логирование

Для логирования используйте стандартный `ILogger` (если доступен через DI) или `Console.WriteLine`:

```csharp
// Простое логирование
Console.WriteLine($"[MyChecker] Checking {service.Host}:{service.Port}");

// Или через ILogger (если доступен)
private readonly ILogger<MyChecker> _logger;

public MyChecker(ILogger<MyChecker> logger)
{
    _logger = logger;
}

_logger.LogInformation("Checking {Host}:{Port}", service.Host, service.Port);
```

**Примечание:** В текущей версии плагины загружаются без DI. Логирование через `Console.WriteLine` будет видно в логах WebdoctorV.

### Валидация конфигурации

Проверяйте корректность конфигурации перед выполнением проверки:

```csharp
public async Task<CheckResult> CheckAsync(ServiceConfig service, string path)
{
    // Валидация обязательных полей
    if (string.IsNullOrEmpty(service.Host))
    {
        return new CheckResult
        {
            ServiceId = service.Id,
            Path = path,
            Protocol = service.Protocol,
            Status = CheckStatus.Dead,
            Error = "Host is required"
        };
    }

    if (service.Port <= 0 || service.Port > 65535)
    {
        return new CheckResult
        {
            ServiceId = service.Id,
            Path = path,
            Protocol = service.Protocol,
            Status = CheckStatus.Dead,
            Error = "Invalid port number"
        };
    }

    // Продолжение проверки...
}
```

## Тестирование плагина

### Unit тесты

Создайте тестовый проект:

```bash
dotnet new xunit -n WebdoctorV.Plugin.MyChecker.Tests
dotnet add reference ../WebdoctorV.Plugin.MyChecker/WebdoctorV.Plugin.MyChecker.csproj
```

Пример теста:

```csharp
using Xunit;
using WebdoctorV.Models;
using WebdoctorV.Plugin.MyChecker;

public class MyCheckerTests
{
    [Fact]
    public void Supports_ReturnsTrue_ForSupportedProtocol()
    {
        var checker = new MyChecker();
        Assert.True(checker.Supports("myprotocol"));
    }

    [Fact]
    public async Task CheckAsync_ReturnsAlive_WhenServiceIsHealthy()
    {
        var checker = new MyChecker();
        var service = new ServiceConfig
        {
            Id = "test",
            Protocol = "myprotocol",
            Host = "localhost",
            Port = 8080
        };

        var result = await checker.CheckAsync(service, "test");

        Assert.Equal(CheckStatus.Alive, result.Status);
        Assert.NotNull(result.Duration);
    }
}
```

### Интеграционные тесты

Для интеграционных тестов используйте Docker Compose или реальные сервисы:

```yaml
# docker-compose.test.yml
version: '3.8'
services:
  test-service:
    image: my-service:test
    ports:
      - "8080:8080"
```

## Упаковка и распространение

### Сборка для релиза

```bash
dotnet build -c Release
dotnet pack -c Release
```

### Публикация в NuGet

1. Создайте аккаунт на [nuget.org](https://www.nuget.org)
2. Получите API ключ
3. Опубликуйте пакет:

```bash
dotnet nuget push WebdoctorV.Plugin.MyChecker.1.0.0.nupkg -k YOUR_API_KEY -s https://api.nuget.org/v3/index.json
```

### Распространение через GitHub Releases

1. Создайте релиз на GitHub
2. Приложите `.dll` файл
3. Добавьте инструкции по установке в README

### Установка плагина

#### Способ 1: Ручная установка

```bash
# Скопировать DLL в папку plugins
cp WebdoctorV.Plugin.MyChecker.dll /path/to/webdoctorv/plugins/
```

#### Способ 2: Через Docker volume

```yaml
volumes:
  - ./plugins:/app/plugins
```

#### Способ 3: Встроить в Docker образ

```dockerfile
FROM webdoctorv:latest
COPY plugins/ /app/plugins/
```

## Примеры

### Пример 1: Простой TCP-подобный чекер

```csharp
using System.Net.Sockets;
using WebdoctorV.Checkers;
using WebdoctorV.Models;

namespace WebdoctorV.Plugin.MyProtocol;

public class MyProtocolChecker : IChecker
{
    public bool Supports(string protocol)
    {
        return protocol.Equals("myprotocol", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<CheckResult> CheckAsync(ServiceConfig service, string path)
    {
        var result = new CheckResult
        {
            ServiceId = service.Id,
            Path = path,
            Protocol = service.Protocol,
            Name = service.Name ?? service.Id,
            Host = service.Host,
            Port = service.Port,
            Status = CheckStatus.Pending
        };

        var startTime = DateTime.UtcNow;

        try
        {
            using var client = new TcpClient();
            var timeout = service.Timeout ?? TimeSpan.FromSeconds(30);
            
            var connectTask = client.ConnectAsync(service.Host, service.Port);
            var timeoutTask = Task.Delay(timeout);
            
            var completedTask = await Task.WhenAny(connectTask, timeoutTask);
            
            if (completedTask == timeoutTask)
            {
                result.Status = CheckStatus.Dead;
                result.Error = "Connection timeout";
            }
            else
            {
                result.Status = client.Connected ? CheckStatus.Alive : CheckStatus.Dead;
            }
        }
        catch (Exception ex)
        {
            result.Status = CheckStatus.Dead;
            result.Error = ex.Message;
        }
        finally
        {
            result.Duration = DateTime.UtcNow - startTime;
            result.LastCheck = DateTime.UtcNow;
        }

        return result;
    }
}
```

### Пример 2: HTTP API чекер

```csharp
using System.Net.Http;
using WebdoctorV.Checkers;
using WebdoctorV.Models;

namespace WebdoctorV.Plugin.CustomApi;

public class CustomApiChecker : IChecker
{
    private readonly HttpClient _httpClient = new();

    public bool Supports(string protocol)
    {
        return protocol.Equals("customapi", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<CheckResult> CheckAsync(ServiceConfig service, string path)
    {
        var result = new CheckResult
        {
            ServiceId = service.Id,
            Path = path,
            Protocol = service.Protocol,
            Name = service.Name ?? service.Id,
            Host = service.Host,
            Port = service.Port,
            Status = CheckStatus.Pending
        };

        var startTime = DateTime.UtcNow;

        try
        {
            var url = $"http://{service.Host}:{service.Port}{service.Path ?? "/health"}";
            var timeout = service.Timeout ?? TimeSpan.FromSeconds(30);
            _httpClient.Timeout = timeout;

            var response = await _httpClient.GetAsync(url);
            
            result.Status = response.IsSuccessStatusCode 
                ? CheckStatus.Alive 
                : CheckStatus.Dead;
            
            if (!response.IsSuccessStatusCode)
            {
                result.Error = $"HTTP {response.StatusCode}";
            }
        }
        catch (Exception ex)
        {
            result.Status = CheckStatus.Dead;
            result.Error = ex.Message;
        }
        finally
        {
            result.Duration = DateTime.UtcNow - startTime;
            result.LastCheck = DateTime.UtcNow;
        }

        return result;
    }
}
```

### Пример 3: Redis чекер (с использованием библиотеки)

```csharp
using StackExchange.Redis;
using WebdoctorV.Checkers;
using WebdoctorV.Models;

namespace WebdoctorV.Plugin.Redis;

public class RedisChecker : IChecker
{
    public bool Supports(string protocol)
    {
        return protocol.Equals("redis", StringComparison.OrdinalIgnoreCase) ||
               protocol.Equals("rediss", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<CheckResult> CheckAsync(ServiceConfig service, string path)
    {
        var result = new CheckResult
        {
            ServiceId = service.Id,
            Path = path,
            Protocol = service.Protocol,
            Name = service.Name ?? service.Id,
            Host = service.Host,
            Port = service.Port,
            Status = CheckStatus.Pending
        };

        var startTime = DateTime.UtcNow;
        ConnectionMultiplexer? connection = null;

        try
        {
            var configuration = $"{service.Host}:{service.Port}";
            if (!string.IsNullOrEmpty(service.Password))
            {
                configuration += $",password={service.Password}";
            }

            var timeout = (int)(service.Timeout?.TotalMilliseconds ?? 30000);
            
            connection = await ConnectionMultiplexer.ConnectAsync(
                new ConfigurationOptions
                {
                    EndPoints = { configuration },
                    ConnectTimeout = timeout,
                    SyncTimeout = timeout
                }
            );

            var database = connection.GetDatabase();
            await database.PingAsync();

            result.Status = CheckStatus.Alive;
            result.AdditionalInfo = $"Connected to Redis";
        }
        catch (Exception ex)
        {
            result.Status = CheckStatus.Dead;
            result.Error = ex.Message;
        }
        finally
        {
            connection?.Dispose();
            result.Duration = DateTime.UtcNow - startTime;
            result.LastCheck = DateTime.UtcNow;
        }

        return result;
    }
}
```

## Часто задаваемые вопросы

### Как добавить дополнительные параметры в конфигурацию?

Если вам нужны дополнительные поля в `ServiceConfig`, создайте issue в основном репозитории. Временно можно использовать существующие поля или хранить данные в `AdditionalInfo` результата.

### Можно ли использовать Dependency Injection в плагинах?

В текущей версии плагины загружаются без DI. Используйте статические методы или создавайте зависимости напрямую.

### Как отладить плагин?

1. Соберите плагин в режиме Debug
2. Скопируйте DLL в папку `plugins/`
3. Запустите WebdoctorV с отладчиком
4. Установите breakpoint в коде плагина
5. Прикрепите отладчик к процессу WebdoctorV

### Плагин не загружается. Что делать?

1. Проверьте, что DLL находится в папке `plugins/`
2. Убедитесь, что плагин скомпилирован для правильной версии .NET
3. Проверьте логи WebdoctorV на наличие ошибок загрузки
4. Убедитесь, что класс реализует `IChecker` и имеет публичный конструктор без параметров

### Можно ли использовать асинхронные операции?

Да! Метод `CheckAsync` возвращает `Task<CheckResult>`, поэтому вы можете использовать `async/await` и любые асинхронные операции.

### Как обработать таймауты?

Используйте `CancellationTokenSource` с таймаутом из `service.Timeout`:

```csharp
var timeout = service.Timeout ?? TimeSpan.FromSeconds(30);
using var cts = new CancellationTokenSource(timeout);

try
{
    await SomeAsyncOperation(cts.Token);
}
catch (OperationCanceledException)
{
    result.Status = CheckStatus.Dead;
    result.Error = "Operation timeout";
}
```

### Можно ли использовать несколько чекеров в одном плагине?

Да! Один плагин может содержать несколько классов, реализующих `IChecker`. Все они будут автоматически загружены.

### Как вернуть дополнительную информацию о проверке?

Используйте поле `AdditionalInfo` в `CheckResult`:

```csharp
result.AdditionalInfo = $"Version: {version}, Uptime: {uptime}";
```

Эта информация будет отображаться в веб-интерфейсе и API.

## Полезные ссылки

- [Основной репозиторий WebdoctorV](https://github.com/your-org/webdoctorv)
- [Примеры плагинов](https://github.com/your-org/webdoctorv-plugins)
- [Документация .NET](https://docs.microsoft.com/dotnet/)
- [NuGet Package Explorer](https://www.nuget.org/packages/NuGetPackageExplorer/)

## Поддержка

Если у вас возникли вопросы или проблемы:

1. Проверьте [FAQ](#часто-задаваемые-вопросы)
2. Создайте issue в основном репозитории
3. Напишите в Discussions

---

**Удачи в разработке плагинов! 🚀**
