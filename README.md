Пишем сервис для проверки хелсчеков Webdoctor.
Может быть несколько протоколов:
- HTTP
- HTTPS
- MySQL
- PostgreSQL
- TCP (проверка подключения)
- UDP (проверка подключения)
- SSH (проверка подключения, выполнение команды)
- будут добавляться другие протоколы по мере необходимости

Для парсинга конфигурации можно использовать PEG или Sprache.
Можно вносить улучшения в формат конфигурации.

Примеры конфигурации лежат в папке examples/ - https, mysql, ssh.
Протоколы и параметры можно расширять.

## Синтаксис конфигурации

### HTTP/HTTPS проверки

Для простых HTTP проверок используйте короткий синтаксис:
```
my-service {
  .protocol 'https'
  .host 'example.com'
  .port 443
  .path '/health'
  .expected_status 200
}
```

Для сложных случаев (MySQL, SSH) используйте блок `response`:
```
my-mysql-service {
  .protocol 'mysql'
  .host 'db.example.com'
  .port 3306
  .query 'SELECT 1'
  
  response {
    .rows 1
    .columns 1
    .data [{"result": 1}]
  }
}
```

Конфигу можно пробрасывать через ConfigMap в Kubernetes или volume в Docker.

Метрики должны отдаваться в Prometheus format по адресу /metrics.

Позже сделаем веб-интерфейс для просмотра статуса хелсчеков и настроек.
