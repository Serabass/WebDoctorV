# WebdoctorV - Health Check Service

Сервис для проверки хелсчеков различных протоколов с метриками Prometheus.

## Возможности

- Поддержка протоколов: HTTP, HTTPS, MySQL, PostgreSQL, TCP, UDP, SSH
- Парсинг конфигурации в формате HCL
- Метрики Prometheus на `/metrics`
- Периодические проверки с настраиваемым интервалом
- Место для будущего фронтенда в папке `wwwroot`

## Запуск

```powershell
dotnet run -- --ConfigPath "..\examples\https-example.wd"
```

Или установите переменную окружения:
```powershell
$env:ConfigPath = "..\examples\https-example.wd"
dotnet run
```

## Конфигурация

Конфигурация загружается из файла `.wd` в формате HCL. Примеры находятся в папке `examples/`.

## Метрики

Метрики доступны по адресу `/metrics`:

- `webdoctor_executable_item_count` - количество проверяемых элементов
- `webdoctor_item_status` - статус элемента (-1 = pending, 0 = dead, 1 = alive)
- `webdoctor_item_last_duration` - длительность последней проверки в миллисекундах
- `webdoctor_item_last_check_date` - timestamp последней проверки

## Фронтенд

Папка `wwwroot` зарезервирована для будущего веб-интерфейса.
