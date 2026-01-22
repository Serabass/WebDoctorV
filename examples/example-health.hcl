interval = "30s"

group "httpbin" {
  name = "HttpBin.io Services"
  protocol = "https"
  host = "httpbin.io"
  port = 443
  method = "GET"
  
  service "health" {
    name = "HttpBin.io Health Check"
    timeout = "10s"
    path = "/anything/health"
    expected_status = 200
  }
  
  group "api" {
    name = "API Endpoints"
    path = "/anything/api"
    
    service "api-base" {
      path = ""
    }
    
    group "v1" {
      name = "API v1"
      path = "/v1"
      
      service "users" {
        name = "Users Endpoint"
        path = "/users"
        expected_status = 200
      }
      
      service "posts" {
        name = "Posts Endpoint"
        path = "/posts"
        expected_status = 200
      }
    }
    
    group "v2" {
      name = "API v2"
      path = "/v2"
      
      service "auth" {
        name = "Auth Endpoint"
        path = "/auth"
        expected_status = 200
      }
    }
  }
  
  group "monitoring" {
    name = "Monitoring"
    path = "/anything/monitoring"
    
    service "metrics" {
      name = "Metrics Endpoint"
      path = "/metrics"
      expected_status = 200
    }
    
    service "health" {
      name = "Monitoring Health"
      path = "/health"
      expected_status = 200
    }
  }
}

group "local" {
  name = "Local Services"
  protocol = "http"
  host = "test-service"
  port = 80
  method = "GET"
  
  service "test-service" {
    name = "Test Service (Local Nginx)"
    timeout = "5s"
    path = "/health"
    expected_status = 200
  }
  
  group "test-api" {
    name = "Test API"
    path = "/api"
    
    service "api-v1-users" {
      name = "Users Endpoint"
      path = "/v1/users"
      expected_status = 200
    }
    
    service "api-v1-posts" {
      name = "Posts Endpoint"
      path = "/v1/posts"
      expected_status = 200
    }
    
    service "api-v2-auth" {
      name = "Auth Endpoint"
      path = "/v2/auth"
      expected_status = 200
    }
  }
  
  group "test-monitoring" {
    name = "Test Monitoring"
    path = "/monitoring"
    
    service "metrics" {
      name = "Metrics Endpoint"
      path = "/metrics"
      expected_status = 200
    }
    
    service "health" {
      name = "Monitoring Health"
      path = "/health"
      expected_status = 200
    }
  }
}

group "databases" {
  name = "Database Services"
  
  service "mysql-test" {
    name = "MySQL Database Check"
    protocol = "mysql"
    host = "mysql"
    port = 3306
    timeout = "10s"
    username = env("MYSQL_USER")
    password = env("MYSQL_PASSWORD")
    database = env("MYSQL_DATABASE")
    query = "SELECT 1+1 AS result, NOW() AS timestamp"
    
    response {
      rows = 1
      columns = 2
    }
  }
  
  service "postgres-test" {
    name = "PostgreSQL Database Check"
    protocol = "postgresql"
    host = "postgres"
    port = 5432
    timeout = "10s"
    username = env("POSTGRES_USER")
    password = env("POSTGRES_PASSWORD")
    database = env("POSTGRES_DB")
    query = "SELECT 1+1 AS result, NOW() AS timestamp"
    
    response {
      rows = 1
      columns = 2
    }
  }
}
