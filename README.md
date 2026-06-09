# Cairo Metro Route Planner

**An ASP.NET Core MVC web application for shortest-path calculation, fare estimation, and travel planning on the Cairo Metro network.**

---

## Overview

The Cairo Metro Route Planner computes the optimal route between any two stations across the three-line network (89 stations, 5 transfer points). It uses Dijkstra's algorithm with a priority queue for pathfinding, a tiered pricing engine for fare calculation, and integrates an interactive Leaflet map with GPS-based nearest-station detection.

Built on a three-tier architecture — **Metro** (presentation), **Metro.Core** (business logic), **Metro.Data** (data access) — the application follows MVC, Repository, Dependency Injection, and Service Layer patterns.

---

## Features

| Feature | Description |
|---|---|
| **Shortest Path** | Dijkstra's algorithm with `PriorityQueue<int, int>` min-heap, O((V+E) log V) |
| **Fare Calculation** | Tiered pricing: 1–9 stn = 8 EGP, 10–16 = 10 EGP, 17+ = 15 EGP |
| **Travel Time** | Estimated at 2 minutes per station |
| **Transfer Detection** | Counts line changes by comparing consecutive station `LineId` values |
| **Interactive Map** | Leaflet + OpenStreetMap with station markers |
| **GPS Nearest Station** | Browser Geolocation API + Haversine formula |
| **Searchable Dropdowns** | Tom Select for keyboard-navigable station selection |
| **Memory Caching** | Graph cached in `IMemoryCache` for 30 minutes |
| **Anti-Forgery** | CSRF protection via `[ValidateAntiForgeryToken]` |
| **Data Seeding** | JSON-based seed with duplicate detection, idempotent on restart |

---

## Architecture

```
┌───────────────────────────────────────────────────┐
│              Metro (ASP.NET Core MVC)              │
│   Controllers · ViewModels · Views (Razor) ·       │
│   Program.cs · wwwroot                             │
│   Responsibilities: HTTP handling, validation,     │
│   view rendering                                   │
├───────────────────────────────────────────────────┤
│              Metro.Core (Class Library)             │
│   Entities · Interfaces · Services · DTOs ·        │
│   Exceptions · Graph Models                        │
│   Responsibilities: Business logic, Dijkstra,      │
│   pricing, travel time, transfer detection         │
├───────────────────────────────────────────────────┤
│              Metro.Data (Class Library)             │
│   DbContext · Repositories · Configurations ·      │
│   Migrations · Seed · SeedData (JSON)              │
│   Responsibilities: ORM mapping, data access,      │
│   database initialization                          │
├───────────────────────────────────────────────────┤
│                   SQL Server                        │
│   MetroDb: Lines · Stations · StationConnections · │
│   PricingRules                                     │
└───────────────────────────────────────────────────┘
```

---

## Technology Stack

| Technology | Version | Purpose |
|---|---|---|
| ASP.NET Core MVC | 8.0 | Web framework |
| Entity Framework Core | 8.0 | ORM |
| SQL Server | — | Relational database |
| .NET | 8.0 | Runtime |
| Bootstrap | 5.x | CSS framework |
| Tom Select | 2.x | Searchable dropdowns |
| Leaflet + OpenStreetMap | 1.9.4 | Interactive maps |
| jQuery Validation | 3.x | Client-side validation |
| xUnit + Moq | 2.9.3 / 8.0.1.7 | Unit testing |

---

## Getting Started

### Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- SQL Server (LocalDB, Express, or full instance)
- A database named `MetroDb`

### Setup

```bash
# Clone the repository
git clone <repository-url>
cd "Cairo Metro"

# Restore dependencies
dotnet restore

# Update the connection string in Metro/appsettings.json
# Server=.\\SQLEXPRESS;Database=MetroDb;Trusted_Connection=True;TrustServerCertificate=True;

# Run the application
dotnet run --project Metro
```

The database is seeded automatically on first startup from JSON files in `Metro.Data/SeedData/`. The seed is idempotent — re-running does not duplicate records.

### Migrations

If you need to recreate the database:

```bash
dotnet ef database update --project Metro
```

---

## Project Structure

```
Cairo Metro/
├── Metro/                          # Presentation layer (ASP.NET Core MVC)
│   ├── Controllers/
│   │   ├── RoutesController.cs     # Primary route search (ViewModels + MetroService)
│   │   └── HomeController.cs       # Landing page (ViewBag-based, legacy)
│   ├── ViewModels/
│   │   ├── RouteSearchViewModel.cs # Form input, stations, result, error
│   │   └── StationOptionViewModel.cs
│   ├── Views/
│   │   ├── Routes/Index.cshtml     # Main route search page
│   │   ├── Home/Index.cshtml       # Landing page
│   │   └── Shared/_Layout.cshtml
│   ├── Models/ErrorViewModel.cs
│   ├── Program.cs                  # Entry point, DI, middleware, seeding
│   └── wwwroot/                    # Static assets
│
├── Metro.Core/                     # Business logic layer
│   ├── Entities/
│   │   ├── Station.cs
│   │   ├── Line.cs
│   │   ├── StationConnection.cs
│   │   └── PricingRule.cs
│   ├── Interfaces/                 # 10 service/repository interfaces
│   ├── Services/
│   │   ├── GraphBuilder.cs         # Adjacency list from DB, cached 30 min
│   │   ├── RouteService.cs         # Dijkstra with PriorityQueue
│   │   ├── PricingService.cs       # Tiered fare calculation
│   │   ├── TravelTimeService.cs    # stationCount × 2 minutes
│   │   ├── TransferDetectionService.cs  # Count line changes
│   │   └── MetroService.cs         # Orchestrator
│   ├── DTOs/RouteResultDto.cs
│   └── Exceptions/
│
├── Metro.Data/                     # Data access layer
│   ├── MetroDbContext.cs
│   ├── Configurations/             # Fluent API (4 files)
│   ├── Repositories/               # 4 repository implementations
│   ├── Migrations/
│   ├── Seed/MetroDataSeeder.cs
│   └── SeedData/                   # JSON files (lines, stations, connections, pricing)
│
└── README.md
```

---

## Controllers

| Controller | Route | Purpose |
|---|---|---|
| `RoutesController` | `GET/POST /Routes/Index` | Primary search — ViewModels, `[ValidateAntiForgeryToken]`, `MetroService` |
| `HomeController` | `GET/POST /Home/Index` | Landing page — `ViewBag`, legacy implementation |
| `HomeController` | `GET /Home/Privacy` | Privacy policy |
| `HomeController` | `GET /Home/Error` | Error page, `[ResponseCache(NoStore = true)]` |

---

## Algorithm: Dijkstra's Shortest Path

The graph is an adjacency dictionary: `Dictionary<int, List<Neighbor>>` where each edge has uniform weight 1.

```
distances  ← { stationId → ∞ }  except start = 0
previous   ← { stationId → null }
queue      ← PriorityQueue<int, int> (min-heap on distance)

while queue is not empty:
    current = queue.dequeue()
    if current == destination: break
    for each neighbor of current:
        newDist = distances[current] + 1
        if newDist < distances[neighbor]:
            distances[neighbor] = newDist
            previous[neighbor] = current
            queue.enqueue(neighbor, newDist)

return ReconstructPath(previous, destination)
```  

Complexity: **O((V + E) log V)** — 89 stations, ~182 connections.

The graph is built once and cached for **30 minutes** in `IMemoryCache`.

---

## Database

### Tables

| Table | Rows | Purpose |
|---|---|---|
| `Lines` | 3 | Metro lines (1, 2, 3) |
| `Stations` | 89 | All stations with coordinates and line membership |
| `StationConnections` | ~182 | Directed edges (bidirectional traversal) |
| `PricingRules` | 3 | Fare tiers |

### Key Design Decisions

- **`ValueGeneratedNever()`** on all PKs — IDs come from JSON seed data
- **`DeleteBehavior.Restrict`** on all FKs — prevents accidental cascade deletes
- **Composite index** on `(LineId, Order)` in Stations — optimizes line-ordered queries
- **3NF** — tables are normalized to Third Normal Form

---

## Services

| Service | Responsibility |
|---|---|
| `GraphBuilder` | Builds adjacency dictionary from DB, caches in memory |
| `RouteService` | Runs Dijkstra, returns `List<int>` of station IDs |
| `PricingService` | Matches station count to tier (1–9 → 8, 10–16 → 10, 17+ → 15 EGP) |
| `TravelTimeService` | `stationCount × 2` minutes |
| `TransferDetectionService` | Counts LineId changes along the path |
| `MetroService` | Orchestrator — coordinates all 5 services, returns `RouteResultDto` |

---

## Team

| Name | ID |
|---|---|
| Abd Alrahman Saed Alsayed | 202302377 |
| Mahmoud Mohamed Hegazy | 202302612 |
| Mahmoud Fawzy Mahmoud | 202302484 |
| Ali Yasser Mohamed | 202305666 |
| Abdullah Ibrahiem Gaballah | 202302701 |

**Supervisor:** Dr. Hossam Eladly

**Institution:** Sinai University — Faculty of Information Technology and Computer Science, Department of Information Technology

**Academic Year:** 2025 / 2026

---

## License

This project was developed for academic purposes at Sinai University.
