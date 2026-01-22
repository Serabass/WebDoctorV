# Запуск через Docker

## Быстрый старт

```powershell
# Сборка образа
docker-compose build

# Запуск контейнера
docker-compose up -d

# Просмотр логов
docker-compose logs -f

# Остановка
docker-compose down
```

## Конфигурация

Конфиг монтируется из `examples/example-health.wd` в контейнер как `/app/config.wd`.

Чтобы использовать другой конфиг, измените путь в `docker-compose.yml`:

```yaml
volumes:
  - ./examples/your-config.wd:/app/config.wd:ro
```

## Метрики

Метрики доступны по адресу: http://localhost:8080/metrics

## Проверка работы

```powershell
# Проверить метрики
curl http://localhost:8080/metrics

# Проверить логи
docker-compose logs --tail=50

# Проверить конфиг в контейнере
docker-compose exec webdoctorv cat /app/config.wd
```
