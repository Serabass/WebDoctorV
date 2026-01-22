group "default" {
  targets = [
    "latest",
    "alpine",
    "slim",
    "jammy"
  ]
}

target "latest" {
  dockerfile = "Dockerfile"
  context = "."
  tags = ["webdoctorv:latest", "webdoctorv:8.0"]
  platforms = ["linux/amd64"]
}

target "alpine" {
  dockerfile = "Dockerfile.alpine"
  context = "."
  tags = ["webdoctorv:alpine", "webdoctorv:8.0-alpine"]
  platforms = ["linux/amd64"]
}

target "slim" {
  dockerfile = "Dockerfile.slim"
  context = "."
  tags = ["webdoctorv:slim", "webdoctorv:8.0-slim"]
  platforms = ["linux/amd64"]
}

target "jammy" {
  dockerfile = "Dockerfile.jammy"
  context = "."
  tags = ["webdoctorv:jammy", "webdoctorv:8.0-jammy"]
  platforms = ["linux/amd64"]
}
