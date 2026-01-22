# WebdoctorV Frontend

React-based frontend for WebdoctorV health check dashboard.

## Development

```bash
npm install
npm start
```

## Build

```bash
npm run build
```

## Docker

The frontend is containerized and runs on port 80 (mapped to 8080 on host).

```bash
docker-compose up frontend
```

The frontend automatically proxies API requests to the backend service.
