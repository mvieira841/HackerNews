# 🚀 HackerNews.Api
A .NET 10 Web API that fetches and serves the best stories from [Hacker News](https://news.ycombinator.com/) with caching, rate limiting, observability, structured logging, and performance validation.

## 🛠️ Technology Stack
* **.NET 10** * **Minimal APIs**
* **Polly** (Resilience & Transient Fault Handling)
* **StackExchange.Redis** (Distributed Caching)
* **Asp.Versioning.Http** (API Versioning)
* **FluentResults** (Result Pattern)
* **Serilog** (Structured Logging)
* **System.Threading.RateLimiting** (Token Bucket Rate Limiting)
* **Testing:**
  * xUnit
  * NSubstitute
  * WebApplicationFactory
  * Testcontainers (Redis)
  * k6 (Load Testing)

## 📂 Project Structure
The solution embraces a **Vertical Slice Architecture** for business features, combined with clear structural boundaries for infrastructure and cross-cutting concerns. 

### 1. 🐳 Root Directory (Deployment & Orchestration)
Contains solution-level configurations and container orchestration.
* `docker-compose.yml`: Provisions the API and its distributed dependencies (Redis).
* `.dockerignore`: Optimizes container build contexts.

### 2. 💻 `src/HackerNews.Api` (Main Application)
The executable Minimal API project. Responsibilities are cleanly divided into logical folders:
* **`Application/`**: Contains the core business logic, structured by feature rather than technical layer (Vertical Slice).
  * **`EndpointMappings.cs`**: Centralized mapping of all Minimal API routes.
  * **`Features/GetBestStories/`**: A self-contained vertical slice executing the primary use case.
    * `Contracts/`: DTOs specifically tailored to this feature (`HnStoryDto`, `StoryResponse`).
    * `Interfaces/`: Handler abstractions (`IGetBestStoriesHandler`).
    * `Endpoint.cs`: The HTTP route definition and parameter validation.
    * `Handler.cs`: The core orchestration logic (concurrency, cache lookup, rate limiting, and mapping).
    * `Constants.cs`: Scoped constants (cache keys, exact route paths) to prevent magic strings.
* **`Infrastructure/`**: Handles all communication with the outside world (External APIs, Caching, Rate Limiting). Depends heavily on abstractions to allow easy mocking during tests.
  * `HackerNewsApiClient.cs`: A strongly-typed HTTP Client using **System.Text.Json Source Generators** for reflection-free deserialization. Wrapped in **Polly** retry policies.
  * `RequestRateLimiter.cs`: Implements a Token Bucket algorithm via `System.Threading.RateLimiting` to control outbound traffic.
  * `RedisCacheService.cs` & `CacheService.cs`: Dual-cache implementations allowing the app to degrade gracefully to In-Memory caching if Redis is unconfigured.
  * `Interfaces/`: Strict contracts for all infrastructural tools (`ICacheService`, `IRequestRateLimiter`, etc.).
* **`Observability/`**: Manages application health, metrics, and telemetry.
  * `MetricsService.cs`: High-performance, thread-safe metric counting using `Interlocked`.
  * *Health Checks*: Configures separate `/health/live` and `/health/ready` probes, including active Redis pings.
* **`Configuration/` & `Api/`**: 
  * Strongly-typed option bindings mapping to `appsettings.json`.
  * API Versioning configuration (`Asp.Versioning.Http`).

### 3. 🧪 `test/HackerNews.Tests` (Quality Assurance)
A comprehensive test suite utilizing **xUnit**.
* **`Api/Integration/`**: End-to-end endpoint tests.
  * Uses `WebApplicationFactory` to spin up an in-memory test server.
  * Uses **Testcontainers** (`TestStartupFactory.cs`) to dynamically spin up, configure, and tear down a real, ephemeral Redis Docker container for accurate cache testing.
* **`Api/Unit/`**: Fast, highly isolated tests targeting individual classes.
  * Uses **NSubstitute** to mock external dependencies (like forcing the HackerNews API to return specific errors to test the Handler's resilience).
* **`Api/Regression/`**: Specialized tests validating complex configurations.
  * Includes tests utilizing custom `HttpMessageHandler` implementations to simulate transient network failures, proving that the **Polly** exponential backoff and circuit breakers function as designed.

### 4. 📈 `performance/` (Load Testing)
* `loadtest.js`: A **k6** load testing script defining traffic stages and CI gating thresholds (e.g., enforcing the 95th percentile response time remains under 500ms under heavy concurrent load).

## ✨ Features
- **Best Stories Endpoint**: Fetches top stories from Hacker News sorted by their score in a descending order (`/api/v1/best-stories?n=3`).
- **Caching**:  
  - Story IDs and details are cached for configurable durations.  
  - Supports **in-memory** or **Redis** caching depending on configuration.
- **Rate Limiting**:  
  - Token bucket rate limiter prevents overwhelming the Hacker News API.  
  - Allows short bursts while maintaining steady throughput.
- **Resilience**:  
  - Uses **Polly** for retry and circuit breaker policies.  
  - Handles transient failures gracefully.  
  - **Fallback strategy implemented**: if the Hacker News API is unavailable, stale cache data is served instead of returning empty results.
- **Observability**:  
  - Collects metrics (requests, cache hits/misses, failures, successes).  
  - Separated Liveness (`/health/live`) and Readiness (`/health/ready`) probes, including automated Redis connectivity checks.
- **Concurrency**:  
  - Configurable parallelism when fetching multiple stories.  
  - Efficient use of CPU and network resources.
- **Structured Logging**:  
  - Integrated with **Serilog** for structured, enriched, and configurable logging.  

## 🏛️ Architecture & Technical Decisions

### 🍰 Vertical Slice Architecture (Feature-Based Structure)
Instead of using a traditional layered architecture (where files are grouped by technical concern like `Controllers/`, `Services/`, and `Models/`), this application implements a **Vertical Slice Architecture**. Code is grouped purely by business feature. 

**Benefits of this approach:**
* **High Cohesion:** Everything a developer needs to understand, test, or modify for a specific feature lives in exactly one place. You don't have to jump between five different root folders to trace the lifecycle of a single HTTP request.
* **Eliminates "Layer Tax":** It removes the friction of creating pass-through services or interfaces that do nothing but hand data from a Controller to a Repository.
* **Scalability:** As the application grows, new endpoints won't bloat a massive `Controllers` or `Services` folder. New features simply get a new isolated folder, ensuring the codebase remains highly navigable regardless of size.

### 🛡️ Performance & Resilience Patterns
- **API Versioning**: Implemented URL-segment versioning (`/api/v1/...`) using `Asp.Versioning.Http` to ensure backward compatibility for future iterations of the API without breaking existing clients.
- **Integration Testing Architecture**: Uses `WebApplicationFactory` combined with `IAsyncLifetime` and **Testcontainers** to spin up ephemeral, isolated Docker Redis instances per test run. DI containers are intercepted via `ConfigureTestServices` to dynamically inject the containerized connection strings, ensuring integration tests hit real infrastructure without local dependencies.
- **High-Performance JSON Serialization**: Uses `System.Text.Json` **Source Generators** (`HackerNewsJsonContext`) to handle the Hacker News API responses. This eliminates runtime reflection overhead, significantly reducing memory allocations and boosting deserialization speed.
- **Concurrency**: To achieve fast response times, individual story details are fetched concurrently using `Parallel.ForEachAsync`. The maximum degree of parallelism is strictly controlled via configuration to prevent thread-pool starvation.
- **Adaptive Caching**: The `ICacheService` transparently handles either **Redis** (Distributed) or **In-Memory** caching. If Redis configuration is missing, it gracefully degrades to In-Memory caching. It also includes a **Stale Cache Fallback**: if the upstream HackerNews API goes down, the application catches the failure and serves expired cache data to remain highly available.
- **Resilience (Polly)**: The typed `HackerNewsApiClient` is wrapped in Polly policies. It includes an exponential backoff retry policy for transient errors (HTTP 5xx, Timeouts, JSON corruption) and a Circuit Breaker that opens after 5 consecutive failures to "fail fast" and prevent system hangs.
- **Egress Rate Limiting**: A `TokenBucketRateLimiter` ensures that our concurrent requests do not violate the Hacker News API's rate limits. It enforces a steady requests-per-second rate while allowing for configurable traffic bursts.
- **Thread-Safe Metrics**: Custom observability is implemented using `Interlocked` operations in the `MetricsService`, providing high-performance, thread-safe tracking of cache hit ratios and upstream failure rates.

## ⚙️ CI/CD Pipeline
The project utilizes **GitHub Actions** for robust continuous integration and delivery:
1. **.NET CI (`ci.yml`)**: On every Pull Request or push to `main`, the application is built and the full xUnit test suite runs. Because GitHub runners have Docker pre-installed, Testcontainers seamlessly spins up an ephemeral Redis instance to validate cache integration.
2. **Performance Gating (`performance-ci.yml`)**: Runs k6 load tests against the API to ensure throughput and response time thresholds (`p(95) < 500ms`) are met under concurrent stress.
3. **Docker CD (`cd.yml`)**: On a successful merge to `main`, the API is containerized using its multi-stage `Dockerfile` and published to the GitHub Container Registry (GHCR), ready for deployment.

## 🏁 Getting Started
### 📋 Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Redis (for distributed caching)
- Docker Desktop (for containerized deployment & testing)

### 💻 Running Locally
1. Clone the repository:
```bash
   git clone https://github.com/mvieira841/HackerNews.git
   cd HackerNews
```

2. Restore dependencies:
```bash
dotnet restore
```

3. Start Redis (optional):
```bash
   docker run --name hn-redis -p 6379:6379 -d redis:7
```

4. Run the API:
```bash
dotnet run --project src/HackerNews.Api/HackerNews.Api.csproj --urls "http://localhost:5000;https://localhost:5001"
```

5. Access the API:
* **HTTP**: `http://localhost:5000` 
* **HTTPS**: `https://localhost:5001` 
* **Swagger UI**: `https://localhost:5001/swagger` 
* **Best Stories Endpoint**:
```
GET http://localhost:5000/api/v1/best-stories?n=10
```

## 🐳 Quick Start with Docker Compose
To run the API with Redis caching enabled, use the `docker-compose.yml` located in the root directory.
Run these commands from the root of the repository:

1. **🔍 Check/Prepare SSL**:
Check if a cert exists: `dotnet dev-certs https --check`.
If missing or for Docker use:
```bash
dotnet dev-certs https -ep %USERPROFILE%\.aspnet\https\aspnetapp.pfx -p your_password
```

2. **Start the environment**:
```bash
docker compose up --build -d
```

This will:
1. Build the API image (ignoring local `bin`/`obj` folders via `.dockerignore`).
2. Start a Redis instance.
3. Automatically configure the API to use the Redis container via environment variables.

The API will be accessible at:
* **HTTP**: `http://localhost:5000` 
* **HTTPS**: `https://localhost:5001` 
* **Swagger UI**: `https://localhost:5001/swagger` 

## 📄 Sample JSON Response
Example response from `/api/v1/best-stories?n=3`:
```json
[
    {
        "title": "Statement from Dario Amodei on our discussions with the Department of War",
        "uri": "https://www.anthropic.com/news/statement-department-of-war",
        "postedBy": "qwertox",
        "time": "2026-02-26T22:42:47+00:00",
        "score": 2544,
        "commentCount": 1358
    },
    {
        "title": "Google API keys weren't secrets, but then Gemini changed the rules",
        "uri": "https://trufflesecurity.com/blog/google-api-keys-werent-secrets-but-then-gemini-changed-the-rules",
        "postedBy": "hiisthisthingon",
        "time": "2026-02-25T19:54:14+00:00",
        "score": 1256,
        "commentCount": 301
    },
    {
        "title": "Layoffs at Block",
        "uri": "https://twitter.com/jack/status/2027129697092731343",
        "postedBy": "mlex",
        "time": "2026-02-26T21:17:56+00:00",
        "score": 837,
        "commentCount": 935
    }
]
```

## 🔧 Configuration
Settings are defined in `appsettings.json`.
### HackerNewsSettings
* **BaseUrl** → Hacker News API base URL.
* **BestStoriesCacheMinutes** → Cache duration (minutes) for best story IDs.
* **StoryDetailsCacheMinutes** → Cache duration (minutes) for individual story details.
* **MaxDegreeOfParallelism** → Maximum concurrent requests when fetching stories.
* **RequestsPerSecond** → Rate limiter throughput (max requests per second).
* **RedisConnectionString** → Redis connection string (e.g., `localhost:6379`). If empty, in-memory cache is used.

## 📊 Observability & Health Checks
The API tracks and exposes internal metrics and health status:
* **Metrics (`/metrics`)**:
* **TotalRequests** → number of requests received
* **CacheHits** → number of times cached data was used
* **CacheMisses** → number of times cache lookup failed
* **FailedRequests** → number of failed upstream requests
* **SuccessfulUpstreamCalls** → number of successful calls to Hacker News API
* **Liveness Probe (`/health/live`)**: A lightweight check confirming the API process is active. 
* **Readiness Probe (`/health/ready`)**: A deep check that validates the API is ready to process traffic. This includes an **active ping to the Redis cache**; if Redis is configured but unreachable, this probe will return `Unhealthy`. 
## 🤔 Assumptions
* The Hacker News API is relatively stable and available at `https://hacker-news.firebaseio.com/v0/`.
* The `n` query parameter must be greater than 0; validation ensures this.
* Redis is optional; if not configured or unavailable at startup, the application safely defaults to in-memory caching.
* The API is intended for high-throughput read-heavy workloads, justifying the aggressive caching and egress rate-limiting strategies.
* **Comment Counts (`descendants` vs. `kids`)**: The Hacker News API provides both `kids` (an array of direct, first-level reply IDs) and `descendants` (an integer representing the total nested comment count). We assume the client only requires the total comment volume, so we map `descendants` directly to our `commentCount` property and completely discard the `kids` array to optimize memory and bandwidth. 
* **Eventual Consistency**: Because story details are aggressively cached (default 15 minutes), the `commentCount` returned by this API might temporarily diverge from the live Hacker News site if comments are rapidly added or deleted during the active cache window.

## ❓ Troubleshooting when using Docker Compose
### Connection Refused (Port 5000/5001)
If the API is running but unreachable:
1. **Check Logs**: `docker compose logs api`. Ensure it says `Now listening on: http://[::]:8080`.
2. **Postman Settings**: Disable **SSL Certificate Verification** in Postman (Settings > General).
3. **Internal Crash**: If logs show a `CryptographicException`, re-run the `dotnet dev-certs` export command with the `--force` flag.

### Redis Connectivity
If `/health/ready` returns `Unhealthy`:
* Check if the Redis container is healthy: `docker ps`.
* Ensure the connection string in `docker-compose.yml` is `redis:6379`.

## 🧪 Testing
Run unit and integration tests:

```bash
dotnet test
```
**Note on Integration Tests:** The test suite uses **Testcontainers** (`Testcontainers.Redis`) to automatically spin up a real, ephemeral Redis Docker container during integration tests. *Ensure Docker Desktop is running on your machine before running tests.* Tests cover API endpoints, caching behavior, rate limiting, and resilience policies.

## 📈 Load Testing with k6 (Docker)
To validate performance tuning and ensure throughput limits are respected under stress, the project includes a k6 load test script (`performance/loadtest.js`). It sets CI gating thresholds (e.g., 95% of requests must complete under 500ms).
You can easily run this test locally using the official k6 Docker container without installing k6 on your machine.
**Step 1: Start the API and Redis in the background**
```bash
docker compose up -d
```

**Step 2: Run the k6 container**
Open your terminal at the root of your repository and run the appropriate command for your OS:
**PowerShell (Windows) / Bash (Mac/Linux):**
```bash
docker run --rm -i `
  -v "$PWD/performance:/performance" `
  -e API_BASE_URL="http://host.docker.internal:5000" `
  grafana/k6 run /performance/loadtest.js
```
**Command Prompt (Windows Cmd):**
```cmd
docker run --rm -i ^
  -v "%cd%/performance:/performance" ^
  -e API_BASE_URL="http://host.docker.internal:5000" ^
  grafana/k6 run /performance/loadtest.js
```
*(Note: We use `http://host.docker.internal:5000` so the k6 container can route traffic out of its own network and hit the API mapped to your host machine).*

## 🔮 Enhancements & Future Improvements
* **Pagination & Filtering**: Allow clients to request stories by score range, author, or time window.
* **Metrics Export**: Integrate with Prometheus/Grafana (via OpenTelemetry) for richer, standard observability.
* **Distributed Tracing**: Add request-level tracing and correlations using OpenTelemetry to map individual requests down to the specific Redis calls.