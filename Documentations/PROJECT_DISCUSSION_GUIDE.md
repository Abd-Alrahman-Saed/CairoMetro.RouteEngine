# CAIRO METRO PROJECT — COMPLETE DISCUSSION GUIDE

> **Warning:** This document is derived entirely from the source code of this project. Study it thoroughly before your viva.

---

# SECTION 1 — PROJECT OVERVIEW

## 1.1 Problem Statement
The Cairo Metro system has 3 lines, 89 stations, and multiple intersecting transfer points. Passengers need to find the **shortest path** between any two stations, know the **fare price**, **estimated travel time**, and **number of transfers**. This system solves that problem with an interactive web application.

## 1.2 Target Users
- Cairo Metro passengers planning trips
- Tourists navigating the metro system
- Anyone needing fare/time/transfer estimates

## 1.3 Main Features
| Feature | Description |
|---|---|
| Shortest Path Calculation | Dijkstra's algorithm on a weighted graph |
| Fare Calculation | Tiered pricing based on station count |
| Travel Time Estimate | 2 minutes per station |
| Transfer Detection | Counts line changes along the path |
| Interactive Map | Leaflet + OpenStreetMap with GPS location |
| Searchable Dropdowns | Tom Select library for station selection |
| GPS Nearest Station | Finds closest station using Haversine formula |
| Data Seeding | JSON-based initial data population |

## 1.4 System Architecture (3-Tier)
```
┌──────────────────────────────────────────────┐
│           Metro (ASP.NET Core MVC)           │
│  Controllers · ViewModels · Views · Program  │
├──────────────────────────────────────────────┤
│            Metro.Core (Class Library)         │
│   Entities · Interfaces · Services · DTOs    │
├──────────────────────────────────────────────┤
│           Metro.Data (Class Library)          │
│  DbContext · Repositories · Migrations · Seed│
└──────────────────────────────────────────────┘
       ┌──────────────────────────┐
       │   SQL Server (MetroDb)   │
       └──────────────────────────┘
```

## 1.5 Request Lifecycle (Actual Example)

User selects "Helwan" → "Sadat" and clicks "Find Route":

```
1. Browser POST /Routes/Index
2. Routing matches {controller=Routes, action=Index}
3. RoutesController.Index(RouteSearchViewModel model) receives form data
4. Controller calls _metroService.GetRouteAsync(1, 19)
5. MetroService:
   a. Validates stations exist via _stationRepository.GetByIdAsync()
   b. Calls _routeService.GetShortestPathAsync(1, 19)
   c. RouteService calls _graphBuilder.BuildGraphAsync()
   d. GraphBuilder queries _stationRepository + _stationConnectionRepository
   e. Dijkstra runs on in-memory graph dictionary
   f. Path returned as List<int> of station IDs
6. MetroService resolves station names, calls:
   g. _travelTimeService.CalculateTravelTime(stationCount)
   h. _transferDetectionService.CountTransfers(stations)
   i. _pricingService.CalculatePrice(stationCount, pricingRules)
7. Returns RouteResultDto
8. Controller maps DTO to ViewModel, returns View(model)
9. Razor renders Index.cshtml with result
10. Browser displays route summary, station path, stats
```

## 1.6 Business Logic Overview

| Logic | Where | Description |
|---|---|---|
| Shortest Path | `RouteService.cs` | Dijkstra's algorithm using PriorityQueue |
| Graph Building | `GraphBuilder.cs` | Builds adjacency list from stations + connections, cached 30 min |
| Pricing | `PricingService.cs` | Matches station count to pricing tier (1-9: 8 EGP, 10-16: 10 EGP, 17+: 15 EGP) |
| Travel Time | `TravelTimeService.cs` | `stationCount * 2` minutes |
| Transfer Count | `TransferDetectionService.cs` | Counts line changes along path |
| Seeding | `MetroDataSeeder.cs` | Reads JSON files, inserts with duplicate detection |

---

# SECTION 2 — MVC ARCHITECTURE

## 2.1 Controllers Analysis

### RoutesController
`Metro/Controllers/RoutesController.cs`

| Aspect | Detail |
|---|---|
| **Purpose** | Primary route search controller using ViewModel pattern |
| **Dependencies** | `IStationRepository`, `IMetroService` |
| **Related Models** | `RouteSearchViewModel`, `StationOptionViewModel`, `RouteResultDto` |
| **Related Views** | `Views/Routes/Index.cshtml` |
| **Related Tables** | Stations, StationConnections, Lines, PricingRules |

### HomeController
`Metro/Controllers/HomeController.cs`

| Aspect | Detail |
|---|---|
| **Purpose** | Landing page route search using ViewBag pattern (older approach) |
| **Dependencies** | `IStationRepository`, `IRouteService`, `IPricingRuleRepository` |
| **Related Views** | `Views/Home/Index.cshtml` |
| **Related Tables** | Stations, PricingRules |

> **IMPORTANT:** Both controllers do essentially the same thing. HomeController uses ViewBag. RoutesController uses proper ViewModels. The RoutesController is the more polished implementation.

## 2.2 Models (Entities)

### Station (`Metro.Core/Entities/Station.cs`)
| Property | Type | Notes |
|---|---|---|
| Id | int | PK, ValueGeneratedNever |
| Name | string | Station name |
| LineId | int | FK → Line |
| Latitude | double | For map display |
| Longitude | double | For map display |
| Order | int | Position on line |
| Line | Line (nav) | Each station belongs to one line |
| FromConnections | ICollection<StationConnection> | Connections originating here |
| ToConnections | ICollection<StationConnection> | Connections ending here |

### Line (`Metro.Core/Entities/Line.cs`)
| Property | Type | Notes |
|---|---|---|
| Id | int | PK, ValueGeneratedNever |
| Name | string | e.g. "Line 1" |
| Color | string | "Red", "Blue", "Green" |
| Stations | ICollection<Station> | Stations on this line |

### StationConnection (`Metro.Core/Entities/StationConnection.cs`)
| Property | Type | Notes |
|---|---|---|
| Id | int | PK, ValueGeneratedNever |
| FromStationId | int | FK → Station |
| ToStationId | int | FK → Station |
| FromStation | Station (nav) | Navigation property |
| ToStation | Station (nav) | Navigation property |

### PricingRule (`Metro.Core/Entities/PricingRule.cs`)
| Property | Type | Notes |
|---|---|---|
| Id | int | PK, ValueGeneratedNever |
| MinStations | int | Lower bound for this tier |
| MaxStations | int | Upper bound for this tier |
| Price | decimal | Fare amount |
| *IsMatch()* | *method* | Returns true if stationCount is in range |

## 2.3 ViewModels

### RouteSearchViewModel (`Metro/ViewModels/RouteSearchViewModel.cs`)
```
FromStationId: int?  ← Form input
ToStationId: int?    ← Form input
Stations: List<StationOptionViewModel>  ← Dropdown data
Result: object?      ← RouteResultDto after successful calculation
ErrorMessage: string?  ← Validation/error feedback
```

### StationOptionViewModel (`Metro/ViewModels/StationOptionViewModel.cs`)
```
Id, Name, LineName, Latitude, Longitude
DisplayName → computed: "Name (LineName)"
```

### ErrorViewModel (`Metro/Models/ErrorViewModel.cs`)
```
RequestId: string?
ShowRequestId: bool (computed)
```

## 2.4 Views

| View | Controller | Model | Purpose |
|---|---|---|---|
| `/Views/Routes/Index.cshtml` | RoutesController | `RouteSearchViewModel` | Main route search page |
| `/Views/Home/Index.cshtml` | HomeController | none (uses ViewBag) | Landing/legacy route search |
| `/Views/Shared/_Layout.cshtml` | — | — | Shared layout |
| `/Views/_ViewStart.cshtml` | — | — | Sets Layout = "_Layout" |
| `/Views/_ViewImports.cshtml` | — | — | Using statements + TagHelpers |
| `/Views/Shared/Error.cshtml` | HomeController | `ErrorViewModel` | Error page |
| `/Views/Shared/_ValidationScriptsPartial.cshtml` | — | — | jQuery Validation scripts |

---

# SECTION 3 — DATABASE ANALYSIS

## 3.1 Entity Relationship Diagram

```
┌───────────┐       ┌───────────────────┐       ┌───────────┐
│   Line    │       │     Station       │       │ Pricing   │
├───────────┤       ├───────────────────┤       │   Rule    │
│ Id (PK)   │◄──────┤ Id (PK)           │       ├───────────┤
│ Name      │       │ Name              │       │ Id (PK)   │
│ Color     │       │ LineId (FK)       │       │ MinSta-   │
│ Stations  │       │ Latitude          │       │ tions     │
└───────────┘       │ Longitude         │       │ MaxSta-   │
                    │ Order             │       │ tions     │
                    │ Line (nav)        │       │ Price     │
                    │ FromConnections   │       └───────────┘
                    │ ToConnections     │
                    └────────┬──────────┘
                             │
                ┌────────────┴────────────┐
                │                         │
     ┌──────────┴──────────┐  ┌──────────┴──────────┐
     │  StationConnection  │  │  StationConnection  │
     │  (FromStation)      │  │  (ToStation)        │
     ├─────────────────────┤  ├─────────────────────┤
     │ Id (PK)             │  │ Id (PK)             │
     │ FromStationId (FK)──┼──│ FromStationId (FK)  │
     │ ToStationId (FK)────┼──│ ToStationId (FK)    │
     │ FromStation (nav)   │  │ FromStation (nav)   │
     │ ToStation (nav)     │  │ ToStation (nav)     │
     └─────────────────────┘  └─────────────────────┘
```

## 3.2 Relationships Explained

### Line → Station (One-to-Many)
```
Line 1 ──┬── Helwan
          ├── Ain Helwan
          ├── ...
          └── New El-Marg
```
**Why:** One line has many stations. Each station belongs to exactly one line.

### Station → StationConnection (One-to-Many, bidirectional)
```
Station (Sadat, Line 1) ──┬── FromConnections → (Sadat, Line 1 → Nasser)
                          └── ToConnections → (Orabi → Sadat, Line 1)
```
**Why:** A station can have many outgoing connections (FromConnections) and many incoming connections (ToConnections). This enables bidirectional graph traversal.

### Transfer Connections (Many-to-Many via Junction Table)
Lines intersect at transfer stations:
```
Sadat (Line 1, Id=19) ←→ Sadat (Line 2, Id=46)
Shohadaa (Line 1, Id=22) ←→ Shohadaa (Line 2, Id=43)
Attaba (Line 2, Id=44) ←→ Attaba (Line 3, Id=74)
Nasser (Line 1, Id=20) ←→ Nasser (Line 3, Id=75)
Cairo University (Line 2, Id=50) ←→ Cairo University (Line 3, Id=89)
```
**Why:** These connections allow the graph algorithm to find paths that cross between different metro lines.

## 3.3 Table: Stations
| Column | Type | Constraints |
|---|---|---|
| Id | int | PK, NOT NULL, ValueGeneratedNever |
| Name | nvarchar(max) | NOT NULL |
| LineId | int | FK → Lines(Id), NOT NULL, Restrict delete |
| Latitude | float | NOT NULL |
| Longitude | float | NOT NULL |
| Order | int | NOT NULL |

**Index:** `IX_Stations_LineId_Order` (composite) — for efficient ordering by line.

## 3.4 Table: Lines
| Column | Type | Constraints |
|---|---|---|
| Id | int | PK, NOT NULL, ValueGeneratedNever |
| Name | nvarchar(100) | Required, Max 100 |
| Color | nvarchar(50) | Required, Max 50 |

## 3.5 Table: StationConnections
| Column | Type | Constraints |
|---|---|---|
| Id | int | PK, NOT NULL, ValueGeneratedNever |
| FromStationId | int | FK → Stations(Id), Restrict delete |
| ToStationId | int | FK → Stations(Id), Restrict delete |

**Indexes:** `IX_StationConnections_FromStationId`, `IX_StationConnections_ToStationId`

## 3.6 Table: PricingRules
| Column | Type | Constraints |
|---|---|---|
| Id | int | PK, NOT NULL, ValueGeneratedNever |
| MinStations | int | NOT NULL |
| MaxStations | int | NOT NULL |
| Price | decimal(18,2) | NOT NULL |

---

# SECTION 4 — DBCONTEXT ANALYSIS

## 4.1 MetroDbContext (`Metro.Data/MetroDbContext.cs`)

```csharp
public class MetroDbContext : DbContext
{
    public DbSet<Station> Stations { get; set; }
    public DbSet<Line> Lines { get; set; }
    public DbSet<StationConnection> StationConnections { get; set; }
    public DbSet<PricingRule> PricingRules { get; set; }
}
```

### Each DbSet explained:

| DbSet | Why it exists |
|---|---|
| `DbSet<Station>` | CRUD for metro stations; most-queried table |
| `DbSet<Line>` | CRUD for metro lines; referenced by Station |
| `DbSet<StationConnection>` | Graph edges connecting stations |
| `DbSet<PricingRule>` | Fare tier configuration |

## 4.2 OnModelCreating

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(MetroDbContext).Assembly);
    base.OnModelCreating(modelBuilder);
}
```

This **automatically discovers** all `IEntityTypeConfiguration<T>` classes in the assembly. Any new configuration class added to the project is picked up automatically.

## 4.3 Fluent API Configurations

### StationConfiguration
```csharp
builder.HasKey(s => s.Id);
builder.Property(s => s.Id).ValueGeneratedNever();     // IDs from JSON files
builder.HasIndex(s => new { s.LineId, s.Order });     // Composite index for ordering
builder.HasOne(s => s.Line)
       .WithMany(l => l.Stations)
       .HasForeignKey(s => s.LineId)
       .OnDelete(DeleteBehavior.Restrict);             // Prevents cascade delete
```

### LineConfiguration
```csharp
builder.HasKey(l => l.Id);
builder.Property(l => l.Id).ValueGeneratedNever();
builder.Property(l => l.Name).IsRequired().HasMaxLength(100);
builder.Property(l => l.Color).IsRequired().HasMaxLength(50);
```

### StationConnectionConfiguration
```csharp
builder.HasKey(sc => sc.Id);
builder.Property(sc => sc.Id).ValueGeneratedNever();
builder.HasIndex(sc => sc.FromStationId);
builder.HasIndex(sc => sc.ToStationId);
// Two separate FK relationships:
builder.HasOne(sc => sc.FromStation).WithMany(s => s.FromConnections)
       .HasForeignKey(sc => sc.FromStationId).OnDelete(DeleteBehavior.Restrict);
builder.HasOne(sc => sc.ToStation).WithMany(s => s.ToConnections)
       .HasForeignKey(sc => sc.ToStationId).OnDelete(DeleteBehavior.Restrict);
```

### PricingRuleConfiguration
```csharp
builder.HasKey(p => p.Id);
builder.Property(p => p.Id).ValueGeneratedNever();
```

## 4.4 Why ValueGeneratedNever?
All entities use `ValueGeneratedNever()` on their Id properties. This is because the seed data comes from JSON files with **pre-defined IDs**. EF Core does not auto-generate these IDs.

## 4.5 Why DeleteBehavior.Restrict?
Prevents accidental deletion of a Line or Station if any child records reference it. Protects referential integrity.

## 4.6 Migrations

| Migration | Date | What it does |
|---|---|---|
| `20260303055823_InitialCreation` | 2026-03-03 | Creates all 4 tables with FKs and indexes |
| `20260410190322_SeedData` | 2026-04-10 | Empty (Up/Down are blank) — seeding is done in code |
| `20260429102840_InitialCreate` | 2026-04-29 | Empty — likely a merge artifact |

### Potential Examiner Questions:

**Q: Why are the later migrations empty?**
A: The `SeedData` and `InitialCreate` migrations have empty `Up()` and `Down()` methods. This indicates they were likely auto-generated by EF Core tooling as scaffolding placeholders or merge artifacts. The actual data seeding happens at runtime in `Program.cs` via `MetroDataSeeder.SeedAsync()`.

**Q: Why does StationConnection have two foreign keys to Station?**
A: Each connection is directional: `FromStationId` → `ToStationId`. This allows both forward and backward traversal. The graph builder creates bidirectional edges by reading connections in both directions.

**Q: What does OnDelete(DeleteBehavior.Restrict) mean?**
A: If you try to delete a Station that has connections, EF Core will throw a constraint violation instead of cascading the delete or setting FK to NULL.

---

# SECTION 5 — CONTROLLER DEFENSE GUIDE

## 5.1 RoutesController

**File:** `Metro/Controllers/RoutesController.cs`

### Dependency Injection
```csharp
private readonly IStationRepository _stationRepository;
private readonly IMetroService _metroService;

public RoutesController(IStationRepository stationRepository, IMetroService metroService)
{
    _stationRepository = stationRepository;
    _metroService = metroService;
}
```
**Why DI:** Controllers should not create dependencies. DI enables loose coupling, testability, and centralized lifecycle management.

### Action 1: Index (GET)

| Aspect | Detail |
|---|---|
| **Action Name** | `Index` |
| **HTTP Verb** | `[HttpGet]` |
| **Parameters** | None |
| **LINQ Queries** | `_stationRepository.GetAllAsync()` → `stations.Select(s => new StationOptionViewModel {...}).OrderBy(s => s.Name).ToList()` |
| **Models** | `RouteSearchViewModel`, `StationOptionViewModel` |
| **Tables** | Stations (eager loaded with Line via `.Include(s => s.Line)`) |
| **Business Logic** | Loads stations for dropdown, maps to ViewModel with `DisplayName` |
| **Returned** | `View(viewModel)` with empty form |
| **View** | `Views/Routes/Index.cshtml` |

### Action 2: Index (POST)

| Aspect | Detail |
|---|---|
| **Action Name** | `Index` |
| **HTTP Verb** | `[HttpPost]` |
| **Parameters** | `RouteSearchViewModel model` |
| **Source** | Form submission (model binding) |
| **Validation** | Manual: checks both IDs are not null, checks they differ |
| **Anti-Forgery** | `[ValidateAntiForgeryToken]` |
| **LINQ Queries** | `_metroService.GetRouteAsync(fromId, toId)` → internally calls multiple queries |
| **Models** | `RouteSearchViewModel`, `RouteResultDto` |
| **Tables** | All 4 tables (through service chain) |
| **Security** | No `[Authorize]` — public endpoint |
| **Returned** | Same View with populated `Model.Result` or `Model.ErrorMessage` |

### LoadStationsAsync (Private Helper)
```csharp
private async Task<List<StationOptionViewModel>> LoadStationsAsync()
{
    var stations = await _stationRepository.GetAllAsync();
    return stations.Select(s => new StationOptionViewModel
    {
        Id = s.Id,
        Name = s.Name,
        LineName = s.Line?.Name ?? string.Empty,
        Latitude = s.Latitude,
        Longitude = s.Longitude
    }).OrderBy(s => s.Name).ToList();
}
```
**Key point:** Uses `s.Line?.Name` with null-conditional because Line may be null if `Include` failed (defensive coding).

### Examiner Questions for RoutesController:

**Q: Why do you check `model.FromStationId is null` instead of using `ModelState.IsValid`?**
A: The ViewModel properties are `int?` (nullable int). ModelState validation would not catch null values for nullable types being unset. Manual validation is more explicit.

**Q: What if `_metroService.GetRouteAsync` throws an exception?**
A: The try-catch block catches `Exception ex` and assigns `model.ErrorMessage = $"Could not calculate route: {ex.Message}"`. The user sees a friendly error instead of an exception page.

**Q: Why use `FirstOrDefault` in `s.Line?.Name ?? string.Empty`?**
A: `s.Line` is a navigation property. `FirstOrDefault` is NOT used here. The `?.` operator returns null if `s.Line` is null, and `??` provides a fallback empty string. This prevents NullReferenceException in the View.

**Q: In the StationRepository's `GetAllAsync()`, what does `.Include(s => s.Line)` do?**
A: It performs an SQL `LEFT JOIN` between Stations and Lines tables, loading the related Line entity for each Station in a single query. Without Include, accessing `s.Line` would trigger a separate query (lazy loading) or return null.

**Q: Why is `OrderBy(s => s.Name)` done client-side instead of in SQL?**
A: It IS translated to SQL because the query hasn't been materialized yet (still IQueryable before `.ToList()`). The SQL will have `ORDER BY [s].[Name]`.

## 5.2 HomeController

**File:** `Metro/Controllers/HomeController.cs`

### Dependency Injection
```csharp
private readonly IStationRepository _stationRepository;
private readonly IRouteService _routeService;
private readonly IPricingRuleRepository _pricingRuleRepository;
```

### Action 1: Index (GET)
- Loads stations via ViewBag
- Returns `Views/Home/Index.cshtml`

### Action 2: Index (POST)
| Aspect | Detail |
|---|---|
| **Parameters** | `int fromStationId`, `int toStationId` (from form) |
| **Validation** | Manual: checks `fromStationId == toStationId` |
| **Missing:** | No `[ValidateAntiForgeryToken]` — security concern |
| **Route Calc** | `_routeService.GetShortestPathAsync(fromStationId, toStationId)` |
| **Pricing** | `_pricingRuleRepository.GetAllAsync()` → `.FirstOrDefault(rule => rule.IsMatch(stationCount))?.Price ?? 0` |
| **Result** | Uses ViewBag (not ViewModel) to pass data to view |

### Action 3: Privacy
- Simple view return, no logic

### Action 4: Error
```csharp
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public IActionResult Error()
{
    return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
```
**Why caching attributes:** Prevents the error page from being cached by browsers or proxies.

### Examiner Questions for HomeController:

**Q: HomeController uses ViewBag while RoutesController uses ViewModels. Why the inconsistency?**
A: This is a potential weak point. ViewBag is loosely typed (magic strings) and prone to runtime errors. ViewModels are strongly typed and compile-time safe. The RoutesController approach is superior.

**Q: Does the POST Index in HomeController have anti-forgery protection?**
A: No — there is no `[ValidateAntiForgeryToken]` attribute. This is a security vulnerability. A malicious site could craft a POST request to this endpoint.

**Q: How does the HomeController calculate price?**
A: It calls `pricingRules.FirstOrDefault(rule => rule.IsMatch(stationCount))?.Price ?? 0`. If no rule matches, price defaults to 0.

**Q: What does `Activity.Current?.Id ?? HttpContext.TraceIdentifier` do?**
A: It gets the current activity's TraceId (from DiagnosticSource) for distributed tracing, falling back to the ASP.NET Core request trace identifier.

---

# SECTION 6 — PROGRAM.CS DEFENSE

**File:** `Metro/Program.cs`

## 6.1 Line-by-Line Analysis

```csharp
// Line 10
var builder = WebApplication.CreateBuilder(args);
```
Creates the application builder with default configuration (appsettings.json, environment variables, etc.).

### Services Registration (Lines 13-36)

```csharp
// Line 15
builder.Services.AddControllersWithViews();
```
Registers MVC services: controller activation, view rendering, model binding, validation, and Razor. **If removed, the app would not serve MVC pages.**

```csharp
// Lines 18-21
builder.Services.AddDbContextPool<MetroDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")
    ));
```
- Uses **DbContext Pooling** (`AddDbContextPool` instead of `AddDbContext`)
- Pooling reuses context instances, improving performance
- Connection string: `Server=.\\SQLEXPRESS;Database=MetroDb;Trusted_Connection=True;TrustServerCertificate=True;`
- **If removed, no database access would work.**

```csharp
// Lines 25-28 — Repository Registration
builder.Services.AddScoped<IStationRepository, StationRepository>();
builder.Services.AddScoped<ILineRepository, LineRepository>();
builder.Services.AddScoped<IStationConnectionRepository, StationConnectionRepository>();
builder.Services.AddScoped<IPricingRuleRepository, PricingRuleRepository>();
```
**Scoped** lifetime: one instance per HTTP request. **If removed, controllers would fail at runtime because DI container cannot resolve constructor parameters.**

```csharp
// Line 29
builder.Services.AddMemoryCache();
```
Registers `IMemoryCache` for in-memory caching. Used by `GraphBuilder` to cache the metro graph for 30 minutes. **If removed, `GraphBuilder` constructor would fail.**

```csharp
// Lines 30-35 — Service Registration
builder.Services.AddScoped<IGraphBuilder, GraphBuilder>();
builder.Services.AddScoped<IRouteService, RouteService>();
builder.Services.AddScoped<IPricingService, PricingService>();
builder.Services.AddScoped<ITravelTimeService, TravelTimeService>();
builder.Services.AddScoped<ITransferDetectionService, TransferDetectionService>();
builder.Services.AddScoped<IMetroService, MetroService>();
```

### Middleware Pipeline (Lines 42-61)

```csharp
// Lines 44-48
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
```
- **Development mode:** Detailed exception pages are shown by default
- **Non-Development:** Redirects to `/Home/Error` and enables HTTP Strict Transport Security

```csharp
// Line 50
app.UseHttpsRedirection();
```
Redirects HTTP to HTTPS. **If removed, the app would still work on HTTP but HTTPS requests would not redirect.**

```csharp
// Line 52
app.UseStaticFiles();
```
Enables serving static files (CSS, JS, images) from `wwwroot`. **If removed, Bootstrap, jQuery, site.css would not load.**

```csharp
// Line 54
app.UseRouting();
```
Adds route matching to the middleware pipeline. **Must be called before UseAuthorization and MapControllerRoute.**

```csharp
// Line 56
app.UseAuthorization();
```
Adds authorization middleware. Currently no [Authorize] attributes exist, but this is prepared for future use.

```csharp
// Lines 58-60
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
```
Default route: `HomeController.Index` is the root URL. `id` is optional.

### Seeding (Lines 63-69)
```csharp
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<MetroDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    await MetroDataSeeder.SeedAsync(context, logger);
}
```
Runs **every time** the app starts. Seed data includes duplicate detection (checks existing IDs before inserting).

## 6.2 Key Architectural Decisions

| Decision | Why |
|---|---|
| `AddDbContextPool` | Better performance; reuses context instances |
| `AddScoped` for all services | One instance per request, thread-safe |
| Cookie without auth middleware | App has no user accounts; prepared for future |
| Seed at startup | Ensures database always has baseline data |

## 6.3 Examiner Questions

**Q: What is the difference between `AddDbContext` and `AddDbContextPool`?**
A: `AddDbContextPool` reuses DbContext instances across requests, reducing instantiation overhead. The pool size is configurable (default 128). Contexts are reset when returned to the pool.

**Q: Why are some services in `Metro.Core.Services` namespace and some in `Metro.Data.Services`?**
A: `RouteService` and `GraphBuilder` are in `Metro.Data.Services` namespace but physically located in `Metro.Core/Services/` folder. This is a naming inconsistency — they should be in `Metro.Core.Services` since they're in the Core project.

**Q: What happens if `SeedAsync` throws an exception on startup?**
A: The application will crash during startup. The `TrySeedStepAsync` method catches individual step exceptions and logs them, but does not rethrow. However, if the error occurs before `app.Run()`, the app won't start.

**Q: Why is there no authentication middleware (app.UseAuthentication)?**
A: The project does not require user authentication — it's a public trip planner. No user accounts, roles, or login functionality exists. The middleware pipeline still has `UseAuthorization()` for future extensibility.

---

# SECTION 7 — DEPENDENCY INJECTION

## 7.1 All Registered Services

| Interface | Implementation | Lifetime | Used By |
|---|---|---|---|
| `IStationRepository` | `StationRepository` | Scoped | HomeController, RoutesController, GraphBuilder, MetroService |
| `ILineRepository` | `LineRepository` | Scoped | (registered but unused in current code) |
| `IStationConnectionRepository` | `StationConnectionRepository` | Scoped | GraphBuilder |
| `IPricingRuleRepository` | `PricingRuleRepository` | Scoped | HomeController, MetroService |
| `IGraphBuilder` | `GraphBuilder` | Scoped | RouteService |
| `IRouteService` | `RouteService` | Scoped | HomeController, MetroService |
| `IPricingService` | `PricingService` | Scoped | MetroService |
| `ITravelTimeService` | `TravelTimeService` | Scoped | MetroService |
| `ITransferDetectionService` | `TransferDetectionService` | Scoped | MetroService |
| `IMetroService` | `MetroService` | Scoped | RoutesController |

## 7.2 Injection Chain

```
RoutesController
  └── IMetroService
        ├── IRouteService
        │     └── IGraphBuilder
        │           ├── IStationRepository
        │           ├── IStationConnectionRepository
        │           └── IMemoryCache (built-in)
        ├── ITravelTimeService
        ├── IPricingService
        ├── ITransferDetectionService
        ├── IStationRepository
        └── IPricingRuleRepository
```

## 7.3 Why Manual Instantiation is Bad

```csharp
// ❌ Bad: manual creation
var repo = new StationRepository(new MetroDbContext(...));
var graphBuilder = new GraphBuilder(repo, connRepo, cache);
var routeService = new RouteService(graphBuilder);
// Everything breaks when constructor changes

// ✅ Good: DI container resolves the entire chain automatically
public RoutesController(IStationRepository repo, IMetroService service)
```

## 7.4 Viva Questions

**Q: What is the difference between AddSingleton, AddScoped, and AddTransient?**
A: Singleton: one instance for the entire application lifetime. Scoped: one instance per HTTP request. Transient: new instance every time requested.

**Q: Which lifetime is used here and why?**
A: All services are Scoped. This is appropriate because each HTTP request should have a fresh DbContext and fresh service instances.

**Q: When would Singleton be dangerous here?**
A: If DbContext were Singleton, multiple requests would share the same context, causing thread-safety issues (concurrent `SaveChanges` conflicts, stale data).

**Q: Could any service here be Singleton safely?**
A: `TravelTimeService` and `PricingService` have no state — they could be Singletons. But keeping all services Scoped is consistent and avoids bugs if state is added later.

---

# SECTION 8 — ENTITY FRAMEWORK CORE

## 8.1 CRUD Operations in This Project

| Operation | Where | Method |
|---|---|---|
| **Read** (all) | StationRepository | `_context.Stations.Include(s => s.Line).ToListAsync()` |
| **Read** (by ID) | StationRepository | `_context.Stations.Include(s => s.Line).FirstOrDefaultAsync(s => s.Id == id)` |
| **Read** (by LineId) | StationRepository | `_context.Stations.Where(s => s.LineId == lineId).OrderBy(s => s.Order).ToListAsync()` |
| **Read** (projected) | StationRepository | `_context.Stations.AsNoTracking().Select(s => new StationGraphNode {...}).ToListAsync()` |
| **Create** (batch) | MetroDataSeeder | `context.Lines.AddRangeAsync(newLines)`, `context.SaveChangesAsync()` |
| **Read** (connections) | StationConnectionRepository | `_context.StationConnections.AsNoTracking().Select(c => new StationConnectionGraphEdge {...}).ToListAsync()` |

## 8.2 Key EF Core Features Used

### AsNoTracking
```csharp
// StationRepository.cs:44
await _context.Stations.AsNoTracking().Select(s => new StationGraphNode{...}).ToListAsync();
```
**Why:** The data is read-only (for graph building). No tracking means faster query execution and less memory usage. The entities are not needed for update operations.

### Include (Eager Loading)
```csharp
// StationRepository.cs:22
await _context.Stations.Include(s => s.Line).FirstOrDefaultAsync(s => s.Id == id);
```
**Why:** Loads the related `Line` entity in the same SQL query (JOIN). Without this, accessing `station.Line` would return null or trigger lazy loading.

### LINQ Projection
```csharp
// StationConnectionRepository.cs:35-41
await _context.StationConnections.AsNoTracking()
    .Select(c => new StationConnectionGraphEdge { FromStationId = c.FromStationId, ToStationId = c.ToStationId })
    .ToListAsync();
```
**Why:** Only selects needed columns (Id fields), not the entire entity. More efficient SQL with SELECT only required columns.

### AddRangeAsync
```csharp
// MetroDataSeeder.cs:74
await context.Lines.AddRangeAsync(newLines, cancellationToken);
```
**Why:** More efficient than individual `AddAsync` calls when inserting multiple entities — sends a single INSERT with multiple rows in one database round-trip.

## 8.3 Viva Questions

**Q: What SQL does `.Include(s => s.Line)` generate?**
A: `SELECT s.*, l.* FROM Stations s LEFT JOIN Lines l ON s.LineId = l.Id`

**Q: What is the difference between `FirstOrDefault` and `First`?**
A: `First` throws `InvalidOperationException` if no match found. `FirstOrDefault` returns `null` (or default). The project uses `FirstOrDefault` with null-check (`?? throw`) for explicit error handling.

**Q: Why is `AsNoTracking` used in `GetAllStationsAsync`?**
A: The graph nodes are read-only DTOs. No tracking reduces memory overhead and improves performance because the change tracker does not monitor these objects.

**Q: What happens if `SaveChangesAsync` is never called?**
A: No data is persisted to the database. All changes remain in memory only.

**Q: Does `_context.Stations.ToListAsync()` load ALL stations into memory?**
A: Yes. After `ToListAsync()`, the query is materialized. For 89 stations this is fine, but for millions of records it would cause memory issues.

---

# SECTION 9 — LINQ ANALYSIS

## 9.1 All LINQ Queries in the Project

### Query 1: GetByIdAsync
```csharp
await _context.Stations.Include(s => s.Line).FirstOrDefaultAsync(s => s.Id == id);
```
**SQL equivalent:**
```sql
SELECT TOP 1 s.*, l.*
FROM Stations s
LEFT JOIN Lines l ON s.LineId = l.Id
WHERE s.Id = @id
```

### Query 2: GetAllAsync (StationRepository)
```csharp
await _context.Stations.Include(s => s.Line).ToListAsync();
```
**SQL:** `SELECT * FROM Stations s LEFT JOIN Lines l ON s.LineId = l.Id`

### Query 3: GetByLineIdAsync
```csharp
await _context.Stations.Where(s => s.LineId == lineId).OrderBy(s => s.Order).ToListAsync();
```
**SQL:** `SELECT * FROM Stations WHERE LineId = @lineId ORDER BY Order`

### Query 4: GetAllStationsAsync (projected graph nodes)
```csharp
await _context.Stations.AsNoTracking()
    .Select(s => new StationGraphNode { Id = s.Id, Name = s.Name, LineId = s.LineId })
    .ToListAsync();
```
**SQL:** `SELECT Id, Name, LineId FROM Stations`

### Query 5: GetAllConnectionsAsync (projected graph edges)
```csharp
await _context.StationConnections.AsNoTracking()
    .Select(c => new StationConnectionGraphEdge { FromStationId = c.FromStationId, ToStationId = c.ToStationId })
    .ToListAsync();
```
**SQL:** `SELECT FromStationId, ToStationId FROM StationConnections`

### Query 6: GetConnectionsByStationIdAsync
```csharp
await _context.StationConnections
    .Where(sc => sc.FromStationId == stationId || sc.ToStationId == stationId)
    .ToListAsync();
```
**SQL:** `SELECT * FROM StationConnections WHERE FromStationId = @id OR ToStationId = @id`

### Query 7: PricingRules GetAllAsync
```csharp
await _context.PricingRules.OrderBy(r => r.MinStations).ToListAsync();
```
**SQL:** `SELECT * FROM PricingRules ORDER BY MinStations`

### Query 8: LineRepository.GetByIdAsync
```csharp
await _context.Lines.Include(l => l.Stations).FirstOrDefaultAsync(l => l.Id == id);
```
**SQL:** `SELECT TOP 1 l.*, s.* FROM Lines l LEFT JOIN Stations s ON l.Id = s.LineId WHERE l.Id = @id`

### Query 9: Seed Duplicate Detection (Stations)
```csharp
var existingIds = (await context.Stations.Select(station => station.Id).ToListAsync()).ToHashSet();
```
**SQL:** `SELECT Id FROM Stations`

### Query 10: LoadStationsAsync (in RoutesController)
```csharp
stations.Select(s => new StationOptionViewModel{...}).OrderBy(s => s.Name).ToList()
```

### Query 11: Path reconstruction (in HomeController Index POST)
```csharp
routeStations = pathIds.Select(id => stations.FirstOrDefault(s => s.Id == id)).Where(s => s != null).ToList()
```
**Note:** This is in-memory LINQ-to-Objects (after `ToListAsync()`).

## 9.2 Examiner Questions

**Q: In Query 10, why is the project calling `.OrderBy(s => s.Name)` after `.Select(...)`?**
A: The order of these calls does not matter for SQL translation. The final SQL will have `ORDER BY [s].[Name]`. Doing Select first reduces the columns before ordering.

**Q: Why does Query 3 use `OrderBy(s => s.Order)` while others don't?**
A: Station order within a line is significant — it defines the sequence for that metro line. Other queries don't need ordering.

**Q: In Query 11, why `.FirstOrDefault(s => s.Id == id)` instead of `.SingleOrDefault`?**
A: IDs are unique (primary key), so both would work. `FirstOrDefault` is slightly more efficient because it stops at the first match without verifying uniqueness.

**Q: Could any of these LINQ queries cause performance issues?**
A: Query 2 loads all 89 stations with their Line data — acceptable. Query 4/5 use `AsNoTracking` with projection — optimal. The seed queries load all IDs to check duplicates — fine for small data sets.

---

# SECTION 10 — VALIDATION & DATA ANNOTATIONS

## 10.1 Current Validation State

**Notable absence:** The project has **almost no data annotations** on models or ViewModels. Validation is done **manually** in controllers.

### Manual Validation in RoutesController (POST Index)
```csharp
if (model.FromStationId is null || model.ToStationId is null)
{
    model.ErrorMessage = "Please select both a departure station and a destination station.";
    return View(model);
}
if (model.FromStationId == model.ToStationId)
{
    model.ErrorMessage = "Departure and destination stations cannot be the same.";
    return View(model);
}
```

### Manual Validation in HomeController (POST Index)
```csharp
if (fromStationId == toStationId)
{
    ViewBag.Error = "Please choose two different stations.";
    return View();
}
```

### Client-Side Validation (JavaScript in Routes/Index.cshtml)
```javascript
document.getElementById('route-search-form').addEventListener('submit', function (e) {
    if (from === to) {
        e.preventDefault();
        showClientError('Departure and destination stations cannot be the same.');
    }
});
```

### HTML5 Validation
```html
<select id="FromStationId" ... required>
```
The `required` attribute on `<select>` elements enforces client-side validation that a non-empty option is selected.

## 10.2 What is Missing

| Validation Type | Missing? | Risk |
|---|---|---|
| `[Required]` on ViewModel | No annotations | ModelState.IsValid is never checked |
| `[Range]` on IDs | No | Invalid IDs pass through |
| `[Compare]` for stations | N/A | Handled manually |
| `[Remote]` validation | No | No AJAX validation |
| `ModelState.IsValid` check | Not used | Relies on manual checks |

## 10.3 Examiner Questions

**Q: Why does the project not use `ModelState.IsValid`?**
A: The ViewModel properties are nullable (`int?`) and the controller performs manual validation. This is a design choice but has a trade-off: data annotations would provide automatic client + server validation.

**Q: What would happen if `[Required]` was added to `FromStationId` and `ToStationId`?**
A: ModelState would automatically check for null values. The controller could then use `if (!ModelState.IsValid) return View(model)` instead of manual null checks. Client-side validation would also work out of the box.

**Q: The `<select>` has `required` attribute. Does this provide server-side protection?**
A: No. `required` is HTML5 client-side validation only. A malicious user can bypass it. Server-side validation (in the controller) is essential.

**Q: What security risk exists because `ModelState.IsValid` is never checked?**
A: If a future developer adds `[Required]` attributes to the ViewModel expecting automatic validation, those would be ignored because `ModelState.IsValid` is never evaluated in the controller action.

---

# SECTION 11 — SECURITY

## 11.1 Current Security Measures

| Measure | Where | Status |
|---|---|---|
| Anti-Forgery Token | `RoutesController.Index POST` | ✅ `[ValidateAntiForgeryToken]` + `@Html.AntiForgeryToken()` |
| HTTPS Redirection | `Program.cs:50` | ✅ `app.UseHttpsRedirection()` |
| HSTS | `Program.cs:47` | ✅ (non-Development only) |
| Error Handling | `Program.cs:46` | ✅ `UseExceptionHandler("/Home/Error")` |
| Response Caching | `HomeController.Error` | ✅ `[ResponseCache(NoStore = true)]` |

## 11.2 Missing Security Measures

| Measure | Missing From | Risk |
|---|---|---|
| `[ValidateAntiForgeryToken]` | `HomeController.Index POST` | CSRF vulnerability |
| Authorization | All endpoints | Public — acceptable for this project |
| Input sanitization | None explicit | Low risk (no database write operations) |
| Rate limiting | Not implemented | Could be abused |

## 11.3 Anti-Forgery Token in RoutesController

**Server-side:**
```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Index(RouteSearchViewModel model)
```

**Client-side:**
```html
<form id="route-search-form" asp-controller="Routes" asp-action="Index" method="post">
    @Html.AntiForgeryToken()
```

**How it works:**
1. Server generates a unique token and embeds it in the form
2. Same token is set as a cookie
3. On POST, server validates that form token matches cookie token
4. Prevents Cross-Site Request Forgery attacks

## 11.4 Overposting Protection

The project uses **ViewModels** (RouteSearchViewModel) which only exposes the properties needed for the form. This is an implicit overposting (mass assignment) protection — a malicious user cannot inject extra properties because the controller only binds the ViewModel.

**Contrast:** HomeController accepts primitive `int fromStationId, int toStationId` parameters — less susceptible to overposting but does not use the ViewModel pattern.

## 11.5 Session

The project does **not** use session state. No `ISession`, no `HttpContext.Session`, no `AddSession()` in Program.cs. This is correct for a stateless trip planner.

## 11.6 Examiner Questions

**Q: Why does HomeController.Index (POST) not have `[ValidateAntiForgeryToken]`?**
A: This is a security gap. Any external website could craft a form that POSTs to `/Home/Index` and make a user submit it, potentially confusing them. RoutesController has proper anti-forgery protection.

**Q: Could a user manipulate the station IDs sent in the POST request?**
A: Yes. The station IDs come from the form submission. A user could modify the HTML or use tools like Postman to send arbitrary IDs. The `GetRouteAsync` validates station existence (`StationNotFoundException`) but does not verify the station belongs to any specific user context (there is none — it's public).

**Q: How does the `@Html.AntiForgeryToken()` helper work?**
A: It generates a hidden input field with a unique token value. On form submission, ASP.NET Core validates this token against a cookie token. If they don't match, the request is rejected with a 400 status.

**Q: What is the risk of no authorization middleware being used?**
A: There is no risk because the app has no authenticated users. Every page is public by design. Adding `[Authorize]` to any controller would immediately block all access since no authentication scheme is configured.

---

# SECTION 12 — RAZOR & VIEWS

## 12.1 Tag Helpers Used

```html
@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
```

### asp-controller, asp-action
```html
<form asp-controller="Routes" asp-action="Index" method="post">
```
Generates: `<form action="/Routes/Index" method="post">`

### asp-append-version
```html
<link rel="stylesheet" href="~/css/site.css" asp-append-version="true" />
```
Appends a hash query string for cache busting. When the file changes, the hash changes, forcing browsers to reload.

### @Html.AntiForgeryToken()
```html
@Html.AntiForgeryToken()
```
Generates hidden anti-forgery input field.

### @Html.ValidationSummary
```html
@Html.ValidationSummary(false, null, new { @class = "" })
```
Displays all validation errors. `false` means exclude property-level errors; only model-level errors are shown.

## 12.2 Layout

**File:** `Views/Shared/_Layout.cshtml`

```
┌─────────────────────────────────────┐
│  RenderBody() from child views      │
│  (content injected here)            │
├─────────────────────────────────────┤
│  Footer: © 2026 - Metro             │
├─────────────────────────────────────┤
│  Scripts: jQuery, Bootstrap, site.js│
│  @RenderSectionAsync("Scripts")     │
└─────────────────────────────────────┘
```

## 12.3 ViewStart / ViewImports

**ViewStart** (`Views/_ViewStart.cshtml`):
```csharp
@{
    Layout = "_Layout";
}
```
Sets the default layout for all views. Individual views can override with `Layout = null`.

**ViewImports** (`Views/_ViewImports.cshtml`):
```csharp
@using Metro
@using Metro.Models
@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
```
Makes namespaces and Tag Helpers available in all views without explicit `@using` statements.

## 12.4 Partial Views

### _ValidationScriptsPartial
```html
<script src="~/lib/jquery-validation/dist/jquery.validate.min.js"></script>
<script src="~/lib/jquery-validation-unobtrusive/dist/jquery.validate.unobtrusive.min.js"></script>
```
Included via:
```html
@section Scripts {
    @{await Html.RenderPartialAsync("_ValidationScriptsPartial");}
}
```

## 12.5 Client-Side Technologies

| Library | Purpose |
|---|---|
| **Tom Select** | Searchable, customizable dropdowns (replaces native `<select>`) |
| **Leaflet + OpenStreetMap** | Interactive map display |
| **jQuery Validation** | Client-side form validation |
| **Geolocation API** | GPS-based nearest station detection |

## 12.6 Examiner Questions

**Q: What does `asp-append-version="true"` do?**
A: It appends a content hash as a query string (e.g., `site.css?v=8h3k2j`). When the file content changes, the hash changes, forcing browsers to download the new version instead of using a cached copy.

**Q: Why is `@Html.ValidationSummary(false, ...)` used instead of `@Html.ValidationSummary(true, ...)`?**
A: The first parameter `false` means show model-level errors AND property-level errors. Since this project validates manually (not via data annotations), model-level errors contain the custom error messages.

**Q: Why are Leaflet and Tom Select loaded from CDN instead of bundled?**
A: Reduces server load, improves caching, and simplifies version management. The trade-off is dependency on external CDN availability.

**Q: How does the `@RenderSectionAsync("Scripts", required: false)` work?**
A: Views can define a `@section Scripts { ... }` block. If present, it's rendered here. If absent, nothing is rendered (because `required: false`).

---

# SECTION 13 — PROJECT FLOW WALKTHROUGH

## 13.1 Scenario 1: User Finds a Route (Happy Path)

### Step 1: User opens the route finder page
```
Browser → GET /Routes/Index
```

### Step 2: RoutesController.Index (GET) executes
1. Creates `RouteSearchViewModel` with empty form data
2. Calls `LoadStationsAsync()`:
   - `StationRepository.GetAllAsync()` → SQL: `SELECT ... FROM Stations ... LEFT JOIN Lines`
   - Maps to `StationOptionViewModel` list with `DisplayName = "Name (LineName)"`
   - Orders alphabetically
3. Returns `View(model)` where `model.Stations` has 89 items

### Step 3: Browser renders the page
1. `_Layout.cshtml` wraps the view
2. `Views/Routes/Index.cshtml` renders:
   - Hero header
   - Form with two Tom Select dropdowns
   - Leaflet map centered on Cairo
   - "Find Route" button

### Step 4: User selects "Helwan" → "Nasser" and clicks "Find Route"

### Step 5: Browser-side validation
1. Client-side JS checks: both values selected? From != To?
2. Tom Select provides searchable dropdown
3. If valid, form submits to POST /Routes/Index
4. Anti-forgery token included automatically

### Step 6: RoutesController.Index (POST) executes
1. Model binding creates `RouteSearchViewModel` from form data
2. `FromStationId = 1` (Helwan), `ToStationId = 20` (Nasser)
3. Manual validation:
   - `FromStationId != null` ✅
   - `ToStationId != null` ✅
   - `FromStationId != ToStationId` (1 != 20) ✅
4. Calls `_metroService.GetRouteAsync(1, 20)`

### Step 7: MetroService.GetRouteAsync executes
1. Validates startId != endId ✅
2. Resolves stations:
   - `StationRepository.GetByIdAsync(1)` → Helwan (Line 1)
   - `StationRepository.GetByIdAsync(20)` → Nasser (Line 1)
3. Calls `RouteService.GetShortestPathAsync(1, 20)`

### Step 8: RouteService.GetShortestPathAsync executes
1. Calls `GraphBuilder.BuildGraphAsync()`
2. Checks cache → miss (first request)
3. Queries all stations and connections
4. Builds adjacency dictionary: `Dictionary<int, List<Neighbor>>`
5. Caches for 30 minutes
6. Runs Dijkstra's algorithm:
```
distances = {1:0, 2:∞, 3:∞, ..., 19:∞, 20:∞, ...}
previous = {1:null, 2:null, ..., 20:null, ...}
queue = [(1, 0)]

Process 1: neighbors → (2, weight=1), distance 2 = 1, previous[2]=1
Process 2: neighbors → (1, 3), distance 3 = 2, previous[3]=2
Process 3: ...
... eventually reaches station 20
```
7. Reconstructs path: `[1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20]`
8. Returns `List<int>` with 20 station IDs

### Step 9: MetroService processes results
1. Resolves path station names via `GetByIdAsync` for each ID
2. `travelTime = 20 * 2 = 40` minutes
3. `transfers = 0` (all on Line 1)
4. Pricing rules:
   - Rule 1: 1-9 stations = 8 EGP → no match (20 stations)
   - Rule 2: 10-16 stations = 10 EGP → no match
   - Rule 3: 17-999 stations = 15 EGP → ✅ MATCH
   - Price = 15 EGP
5. Returns `RouteResultDto`

### Step 10: Controller prepares view
1. `model.Result = RouteResultDto`
2. `model.Stations` reloaded for dropdown persistence
3. Returns `View(model)`

### Step 11: Razor renders result
1. Detects `result != null` → shows result card
2. Renders: From → To pills
3. Renders 4 stat cards: Stations (20), Transfers (0), Price (15.00 EGP), Est. Time (40 min)
4. Renders ordered station list with numbered dots
5. JS scrolls result into view smoothly

### Step 12: User sees complete route information

## 13.2 Scenario 2: GPS Nearest Station

1. User clicks "Use My Location" button
2. Browser requests geolocation permission
3. JavaScript `Geolocation.getCurrentPosition()` gets coordinates
4. Haversine formula calculates distance to each station:
```javascript
function haversineDistance(lat1, lon1, lat2, lon2) {
    const R = 6371; // Earth radius in km
    // ... spherical trigonometry
    return distance in km;
}
```
5. Nearest station selected in dropdown via Tom Select API
6. Map shows: user marker, station marker, dashed red line between them
7. Map view fits both markers

## 13.3 Scenario 3: Error — Same Station Selected

1. User selects same station for both dropdowns
2. Client-side JS prevents submission, shows error banner
3. If JS disabled, server-side catches: `fromId == toId`
4. Returns `model.ErrorMessage = "Departure and destination stations cannot be the same."`
5. Error displayed in styled red message box

## 13.4 Scenario 4: Error — Station Not Found

1. User modifies HTML to send invalid station ID (e.g., 9999)
2. `MetroService.GetRouteAsync(9999, 20)`
3. `StationRepository.GetByIdAsync(9999)` returns null
4. `?? throw new StationNotFoundException("Station with ID 9999 was not found.")`
5. Controller catches exception
6. `model.ErrorMessage = "Could not calculate route: Station with ID 9999 was not found."`
7. User sees error message without stack trace

---

# SECTION 14 — TOP 100 LIKELY EXAMINER QUESTIONS

## Architecture & Design

**Q1: What problem does this system solve?**
A: It calculates the shortest path between any two Cairo Metro stations, including travel time, fare, and transfer information, via an interactive web interface.

**Q2: Why is the project split into three layers?**
A: Separation of concerns. Metro (UI/presentation), Metro.Core (business logic/domain), Metro.Data (data access). This enables independent testing, maintenance, and potential replacement of any layer.

**Q3: What design patterns are used?**
A: MVC (Controllers, Views, Models), Repository Pattern (IStationRepository, etc.), Dependency Injection, Service Layer Pattern, DTO Pattern.

**Q4: Why use interfaces like IStationRepository instead of concrete classes?**
A: For loose coupling, testability (easy mocking), and the ability to swap implementations without changing consumers.

**Q5: What would you change if you had to add a new metro line?**
A: Add the line JSON data to lines.json, add station JSON data, add connections JSON data, and run the application — the seed system would detect new entries and insert them.

## Controllers

**Q6: What is the difference between RoutesController and HomeController?**
A: RoutesController uses proper ViewModels (`RouteSearchViewModel`) while HomeController uses `ViewBag`. RoutesController has `[ValidateAntiForgeryToken]` on POST; HomeController does not. RoutesController uses `IMetroService` (orchestrator); HomeController uses individual services directly.

**Q7: Why does RoutesController.Index (GET) not accept any parameters?**
A: The GET action only loads the form with station dropdowns. No route calculation happens until the form is submitted via POST.

**Q8: What would happen if you removed the try-catch block from RoutesController.Index (POST)?**
A: Any exception (StationNotFoundException, InvalidRouteException, database error) would propagate as a 500 error page. For end users, this means an unfriendly error experience.

**Q9: Why is LoadStationsAsync called twice in the POST action (success and error paths)?**
A: The `Stations` list must be repopulated before returning the View, regardless of success or failure. Without it, the dropdown would be empty.

**Q10: Why does HomeController use ViewBag instead of a ViewModel?**
A: This is a design inconsistency. ViewBag is loosely typed and error-prone at runtime. The RoutesController approach is better.

## Models & Entities

**Q11: Why do all entities have private setters?**
A: To enforce immutability and encapsulation. Properties can only be set through constructors or EF Core's private constructor support.

**Q12: Why is there a private parameterless constructor on every entity?**
A: EF Core requires it for materialization. When EF Core reads data from the database, it creates objects using the parameterless constructor, then sets properties via reflection.

**Q13: Why does Station have both FromConnections and ToConnections?**
A: A station can be the start of multiple connections (outgoing) and the end of multiple connections (incoming). This bidirectional navigation enables the graph building.

**Q14: Why does PricingRule have an IsMatch() method inside the entity?**
A: Domain logic placed in the domain entity (rich domain model). The `IsMatch` method checks if a station count falls within the rule's range.

**Q15: What is the purpose of the Order property on Station?**
A: It defines the sequence of stations within a metro line, used for ordering the display and ensuring the graph structure reflects the actual track layout.

## Database & EF Core

**Q16: Why does every Id use ValueGeneratedNever()?**
A: Because the seed data provides specific IDs from JSON files. Auto-generated IDs would conflict with the predefined IDs.

**Q17: What would happen if you removed `OnDelete(DeleteBehavior.Restrict)`?**
A: EF Core would use Cascade delete by default. Deleting a Station would delete all its connections. This is dangerous for the data integrity.

**Q18: Why is there a composite index on (LineId, Order)?**
A: To optimize queries that filter stations by line and order them by position (e.g., displaying all stations of Line 1 in order).

**Q19: Could you add lazy loading to this project?**
A: Yes, by installing `Microsoft.EntityFrameworkCore.Proxies` and adding `UseLazyLoadingProxies()`. However, this is not recommended due to the N+1 query problem.

**Q20: What is the difference between `.Include()` and `.ThenInclude()`?**
A: `.Include()` loads a related entity. `.ThenInclude()` loads a nested related entity (e.g., Station → Line → ...). This project does not use ThenInclude.

## LINQ

**Q21: What SQL does `_context.Stations.Include(s => s.Line).FirstOrDefaultAsync(s => s.Id == id)` generate?**
A: 
```sql
SELECT TOP 1 s.Id, s.Name, s.LineId, s.Latitude, s.Longitude, s.Order,
             l.Id, l.Name, l.Color
FROM Stations s
LEFT JOIN Lines l ON s.LineId = l.Id
WHERE s.Id = @id
```

**Q22: In the seed method, why is `.ToHashSet()` used?**
A: `HashSet<T>` provides O(1) lookup for checking if an ID already exists. Using a List would be O(n) for each check.

**Q23: Why use `AsNoTracking()` in `GetAllStationsAsync`?**
A: The graph nodes are read-only. No tracking means the change tracker doesn't monitor them, reducing memory usage and improving query speed.

**Q24: How does `_context.PricingRules.OrderBy(r => r.MinStations).ToListAsync()` sort?**
A: It orders by the minimum station count ascending (1, 10, 17). This is important for matching the correct pricing tier.

**Q25: In `LoadStationsAsync`, why use `.OrderBy(s => s.Name)`?**
A: To display stations alphabetically in the dropdown for easier user navigation.

## Dependency Injection

**Q26: Why is `IMemoryCache` registered without explicit AddScoped?**
A: `AddMemoryCache()` registers `IMemoryCache` as a Singleton by default. This is correct because the cache should be shared across all requests.

**Q27: What is the dependency injection chain for RoutesController?**
A: RoutesController → IMetroService → IRouteService (→ IGraphBuilder → IStationRepository + IStationConnectionRepository + IMemoryCache) + ITravelTimeService + IPricingService + ITransferDetectionService + IStationRepository + IPricingRuleRepository.

**Q28: If `IMetroService` was not registered, what error would occur?**
A: At runtime, when `RoutesController` is activated, the DI container would throw `InvalidOperationException: Unable to resolve service for type 'IMetroService'`.

**Q29: Why are repositories registered as Scoped?**
A: Each HTTP request gets a fresh instance. This ensures each request has its own DbContext, preventing concurrent access issues.

**Q30: Could `TravelTimeService` be Singleton?**
A: Yes, it has no state. But keeping all services Scoped is consistent and prevents bugs if state is added later.

## Algorithm

**Q31: What algorithm is used for shortest path finding?**
A: Dijkstra's algorithm, implemented with a `PriorityQueue<int, int>` (min-heap) for O((V+E) log V) complexity.

**Q32: Why Dijkstra instead of BFS?**
A: Dijkstra handles weighted edges. Even though all edges have weight 1, the implementation is ready for future weighted scenarios (e.g., express connections with different travel times).

**Q33: How does the graph determine if two stations are connected?**
A: Via `StationConnection` entities. Each connection has `FromStationId` and `ToStationId`. The `GraphBuilder` creates bidirectional edges in the adjacency dictionary.

**Q34: How does the edgeSet HashSet prevent duplicate edges?**
A: Before adding an edge, it checks if `"{fromStationId}-{toStationId}"` exists in the set. If yes, the edge is skipped. This prevents duplicate connections.

**Q35: What is the time complexity of the route finding?**
A: O((V + E) log V) where V = vertices (stations) and E = edges (connections). With 89 stations and ~172 connections, it's extremely fast.

**Q36: How are transfer stations represented in the graph?**
A: Individual connections exist between the same-name station on different lines (e.g., Sadat on Line 1 ↔ Sadat on Line 2). The graph treats them as separate nodes connected by an edge.

**Q37: What happens if there is no path between two stations?**
A: Dijkstra completes without reaching the destination. `distances[endId]` remains `int.MaxValue`, and the code throws `InvalidRouteException`.

## Security

**Q38: How is CSRF prevented in RoutesController?**
A: `[ValidateAntiForgeryToken]` on the POST action + `@Html.AntiForgeryToken()` in the form. The server validates that the form token matches the cookie token.

**Q39: Why is there no CSRF protection on HomeController.Index (POST)?**
A: This is a security gap. The `[ValidateAntiForgeryToken]` attribute is missing on that action.

**Q40: Could a user see sensitive data by manipulating the URL?**
A: No public endpoints expose sensitive data. Both controllers are read/search only.

**Q41: How does the application protect against overposting?**
A: RoutesController uses ViewModels which only expose expected properties. HomeController uses primitive parameters. Both prevent extra field injection.

**Q42: Does the application have authentication?**
A: No. There are no user accounts, no login pages, no `[Authorize]` attributes. The app is completely public.

## Views & UI

**Q43: What does `@Html.ValidationSummary(false, null, new { @class = "" })` do?**
A: It renders all validation error messages from ModelState. The first parameter false means include both model-level and property-level errors.

**Q44: Why is Tom Select used instead of native `<select>`?**
A: Tom Select provides search functionality for long lists (89 stations). Native select dropdowns are difficult to search on mobile.

**Q45: How is the Leaflet map centered on Cairo?**
A: `L.map('map').setView([30.0444, 31.2357], 11)` — these coordinates are the Sadat station location, which is the geographic center of the metro network.

**Q46: What happens if the CDN for Leaflet is unavailable?**
A: The map would not render. Users would see an empty gray div (background-color: #e5e5e5). The route search form would still work.

**Q47: How does the Haversine formula work for GPS?**
A: It calculates the great-circle distance between two points on a sphere using latitude/longitude. The formula: `a = sin²(Δlat/2) + cos(lat1)·cos(lat2)·sin²(Δlon/2)`, `c = 2·atan2(√a, √(1-a))`, `d = R·c` where R = 6371 km.

**Q48: Why is `asp-append-version="true"` on CSS files?**
A: Cache busting. When the file changes, the hash query string changes, forcing browsers to download the new version.

**Q49: What is the purpose of `@RenderSectionAsync("Scripts", required: false)` in the layout?**
A: It allows individual views to inject JavaScript at the end of the page (before closing `</body>`), improving page load performance.

**Q50: How does the form know which controller/action to submit to?**
A: `asp-controller="Routes" asp-action="Index"` Tag Helpers generate the correct form action URL.

## Validation

**Q51: Why is there no data annotation validation on ViewModels?**
A: The project uses manual validation. This works but misses the benefits of automatic client + server validation that data annotations provide.

**Q52: What would adding `[Required]` to `FromStationId` achieve?**
A: It would add server-side validation that the value is not null, and client-side validation via jquery-validation-unobtrusive.

**Q53: Is the `required` HTML attribute on `<select>` sufficient for security?**
A: No. HTML5 validation is client-side only. A malicious user can bypass it. Server-side validation is essential.

**Q54: How does the client-side JavaScript prevent same-station selection?**
A: It intercepts the form submit event, checks if from equals to, calls `e.preventDefault()`, and shows an error banner.

**Q55: What happens if JavaScript is disabled and user selects same station?**
A: Server-side validation catches it: `if (model.FromStationId == model.ToStationId)` returns an error message.

## Graph & Data Structures

**Q56: How is the graph stored in memory?**
A: As `Dictionary<int, List<Neighbor>>` where key = station ID, value = list of neighbors. Each `Neighbor` has a StationId and Weight.

**Q57: Why is the graph cached for 30 minutes?**
A: Metro stations and connections rarely change. Caching avoids rebuilding the graph on every request. 30 minutes is a reasonable trade-off between freshness and performance.

**Q58: How does the PriorityQueue work in Dijkstra?**
A: It's a min-heap. Elements are dequeued in order of their priority (distance from start). The smallest distance is always processed first.

**Q59: What is the ReconstructPath method doing?**
A: Starting from the end station, it follows the `previous` dictionary back to the start station, reversing the path to get the correct order.

**Q60: What happens in BuildGraphAsync if a connection references a non-existent station?**
A: The `AddEdge` method checks if the station exists in the graph dictionary. If not, it creates an empty entry for it. This is defensive.

## Services

**Q61: What is the role of MetroService?**
A: It's a Facade/Orchestrator service that coordinates multiple specialist services (RouteService, TravelTimeService, PricingService, TransferDetectionService) to produce a complete RouteResultDto.

**Q62: Why is PricingService a separate service?**
A: Single Responsibility Principle. Pricing logic is independent of route finding and can be tested, modified, or replaced separately.

**Q63: What does TransferDetectionService do?**
A: It counts how many times the passenger must change lines by comparing consecutive stations' LineId values.

**Q64: What would happen if TravelTimeService received a stationCount of 0?**
A: It would return 0 minutes (0 * 2 = 0). The guard clause checks for negative values but allows zero.

**Q65: Why does GraphBuilder create both directions for each connection?**
A: The metro is bidirectional — trains travel both ways. Each connection from A→B and B→A ensures the graph is undirected.

## Seeding

**Q66: Why are JSON files used for seed data?**
A: JSON is easy to edit, maintain, and version control. It also allows domain experts to update station/line data without touching C# code.

**Q67: What prevents duplicate seeding if the app restarts?**
A: The seed methods check existing IDs first: `existingIds = (await context.Stations.Select(s => s.Id).ToListAsync()).ToHashSet()`. Only new IDs are inserted.

**Q68: Why is GroupBy used in SeedConnectionsAsync?**
A: `connections.GroupBy(c => c.Id).Select(g => g.First())` removes duplicate connection entries with the same ID from the JSON file, preventing primary key conflicts.

**Q69: What would happen if a line referenced in stations.json doesn't exist?**
A: The `Where(station => station.LineId > 0 && existingLineIds.Contains(station.LineId))` filter would skip those stations. They would not be seeded.

**Q70: Why is the seed path `Path.Combine(AppContext.BaseDirectory, "SeedData")`?**
A: JSON files are copied to the output directory (bin). `AppContext.BaseDirectory` points to the bin folder, where the files are located.

## Potential Issues

**Q71: The RoutesController.cs is excluded from compilation in the .csproj file. Why?**
A: Line 17-18 of Metro.csproj: `<Compile Remove="Controllers\RoutesController.cs" />`. This is likely a mistake or intentional exclusion. If you're being examined on this, the RoutesController may not actually work. The examiner may ask: "Why is your RoutesController not compiled?"

**Q72: Why is `ILineRepository` registered but never used in any controller or service?**
A: It's registered in Program.cs but never injected anywhere. This is dead code.

**Q73: Why are RouteService and GraphBuilder in Metro.Data.Services namespace but in Metro.Core folder?**
A: Namespace inconsistency. They should be in `Metro.Core.Services` to match their physical location.

**Q74: Why does the project have both migration-based and runtime-based seeding?**
A: The `SeedData` migration is empty. All actual seeding is done at runtime via `MetroDataSeeder`. The empty migration is likely an artifact.

**Q75: Why is there no error handling for the case when both dropdowns are left empty?**
A: Manual validation covers null checks on both FromStationId and ToStationId. If either is null, an error message is shown.

## Business Logic

**Q76: How is the price calculated for a trip?**
A: The station count is compared to pricing rules. Rules: 1-9 stations = 8 EGP, 10-16 = 10 EGP, 17+ = 15 EGP.

**Q77: What would happen if pricingRules.json is empty?**
A: `pricingRules.FirstOrDefault(rule => rule.IsMatch(stationCount))` returns null. The null-conditional `?.Price ?? 0` sets price to 0.

**Q78: How is travel time calculated?**
A: `stationCount * 2` minutes. This assumes 2 minutes per station, which is a simplification.

**Q79: Does transfer time affect travel time?**
A: No. The travel time is purely `stationCount * 2`. Transfers (waiting for next train) are not included.

**Q80: Why is the result type `object?` in RouteSearchViewModel?**
A: The comment says "Kept as object per spec so the View can cast as needed." It's cast back to `RouteResultDto` in the View.

## Program.cs

**Q81: What is the difference between `AddDbContext` and `AddDbContextPool`?**
A: `AddDbContextPool` reuses context instances. When a request ends, the context is reset and returned to the pool. This reduces overhead.

**Q82: What happens if the connection string is wrong?**
A: The app will start but any database query will throw `SqlException`. The error handler would catch it and show an error page.

**Q83: Why is `UseAuthorization()` called if no authorization is configured?**
A: It's prepared for future use. Adding `[Authorize]` attributes later would work without changing the middleware pipeline.

**Q84: Why does `app.UseHttpsRedirection()` come before `UseStaticFiles()`?**
A: Middleware order matters. HTTPS redirection should happen early. Static files should be served after redirection to avoid serving files over HTTP.

**Q85: What would happen if `AddControllersWithViews()` was removed?**
A: The app would compile but all controller actions would return 404. MVC services are not registered.

## EF Core & Migrations

**Q86: What tool is used to create migrations?**
A: `dotnet ef migrations add MigrationName` or Visual Studio's Package Manager Console: `Add-Migration MigrationName`.

**Q87: How does EF Core map Station.Id to the database?**
A: By convention, properties named `Id` or `{EntityName}Id` become primary keys. The `ValueGeneratedNever()` configuration tells EF Core not to auto-generate values.

**Q88: Why does the migration create indexes on FromStationId and ToStationId?**
A: Because the configuration calls `builder.HasIndex(sc => sc.FromStationId)` and `builder.HasIndex(sc => sc.ToStationId)`. These indexes speed up JOIN and WHERE queries.

**Q89: What is the ModelSnapshot file used for?**
A: `MetroDbContextModelSnapshot.cs` contains the current state of the model. EF Core uses it to detect changes when creating new migrations.

**Q90: How would you add a new entity (e.g., TrainSchedule) to this project?**
A: Create the entity class in Metro.Core, add DbSet to MetroDbContext, optionally add configuration, create a migration via `dotnet ef migrations add AddTrainSchedule`, and update the database.

## General

**Q91: What testing strategy is used?**
A: The csproj includes xUnit, Moq.EntityFrameworkCore, and Microsoft.NET.Test.Sdk packages, suggesting unit tests were planned or created but I don't see test files in the project.

**Q92: What is the target framework?**
A: .NET 8.0. (Some bin folders show net10.0 but the .csproj specifies net8.0.)

**Q93: How does the URL routing work for the default page?**
A: The default route pattern `{controller=Home}/{action=Index}/{id?}` means visiting the root URL `http://localhost:5279/` maps to `HomeController.Index()`.

**Q94: What is the purpose of `launchSettings.json`?**
A: It configures development-time settings: application URLs, environment variables, and IIS Express settings.

**Q95: Why is `ASPNETCORE_ENVIRONMENT` set to "Development"?**
A: In Development mode, detailed error pages are shown, and `UseExceptionHandler` is not used. This helps during development.

## Relationships

**Q96: What type of relationship exists between Station and Line?**
A: Many-to-One. Many stations belong to one line. `Line.Stations` is the collection side; `Station.Line` is the reference side.

**Q97: What type of relationship exists between Station and StationConnection?**
A: One-to-Many (two of them). One station has many FromConnections and many ToConnections.

**Q98: Why is StationConnection not a Many-to-Many between Station and itself?**
A: The current design uses StationConnection as a distinct entity with its own Id. This is more flexible and allows storing additional data on connections (e.g., travel time, direction) in the future.

**Q99: How are transfer stations represented in the database?**
A: Transfer stations are duplicate entries in the Stations table with different IDs, different LineId values, but the same name. Connections in StationConnections link these separate station records.

**Q100: What would happen if a Station is deleted from the database?**
A: Due to `DeleteBehavior.Restrict`, EF Core would throw a `DbUpdateException` if any Connection references the station. The deletion would be blocked.

---

# SECTION 15 — WEAK POINTS

> 🔴 **High Probability Discussion Topics**

## 15.1 CRITICAL: RoutesController Excluded from Compilation

**File:** `Metro/Metro.csproj` (Line 17-18)

```xml
<Compile Remove="Controllers\RoutesController.cs" />
```

**Problem:** The `RoutesController.cs` file is explicitly excluded from compilation. It's added as a `<None>` item instead.

**Impact:** The RoutesController and all its functionality (the primary route search page) does not actually work when the project is built. The `/Routes/Index` endpoint would return 404.

**How to answer:**
- "This is a configuration error in the .csproj file."
- "The `<Compile Remove>` directive prevents the C# compiler from including the file."
- "It should be removed or the file should be included as a Compile item."
- "The fact that `<None Include="Controllers\RoutesController.cs" />` is also present confirms it should be a code file."

## 15.2 HomeController Missing Anti-Forgery Token

**File:** `Metro/Controllers/HomeController.cs` (Line 31-32)

```csharp
[HttpPost]
public async Task<IActionResult> Index(int fromStationId, int toStationId)
```

**Problem:** No `[ValidateAntiForgeryToken]` attribute on the POST action.

**Impact:** The form is vulnerable to Cross-Site Request Forgery. A malicious site could submit a POST request on behalf of an unsuspecting user.

## 15.3 No ModelState.IsValid Check

**Problem:** Neither controller uses `ModelState.IsValid` to validate input. Validation is entirely manual. This means:
1. Data annotations on ViewModels would be ignored
2. Client-side validation from annotations would not be triggered server-side
3. No standardized validation pattern

## 15.4 Namespace Inconsistency

**File Locations:**
- `Metro.Core\Services\RouteService.cs` → namespace `Metro.Data.Services`
- `Metro.Core\Services\GraphBuilder.cs` → namespace `Metro.Data.Services`

**Problem:** These files are physically in `Metro.Core/Services/` but their declared namespace is `Metro.Data.Services`. This is confusing and violates project conventions.

## 15.5 Dead Code: ILineRepository

**File:** `Metro.Data/Repositories/LineRepository.cs`

**Problem:** `ILineRepository` and `LineRepository` are registered in DI but never injected into any controller or service. The `GetByIdAsync` method with `.Include(l => l.Stations)` is never called.

## 15.6 Legacy Empty Migrations

**Files:**
- `20260410190322_SeedData.cs` — empty Up/Down
- `20260429102840_InitialCreate.cs` — empty Up/Down

**Problem:** These migrations exist but do nothing. They are likely scaffolding artifacts or failed migration attempts. They could confuse developers about the actual database state.

## 15.7 Pricing Rules Hardcoded Assumptions

**File:** `Metro.Data/SeedData/pricingRules.json`

```
1-9 stations → 8 EGP
10-16 stations → 10 EGP
17+ stations → 15 EGP
```

**Problem:** The maximum tier uses 999 as the upper bound. This is an arbitrary limit. If the metro expands beyond 999 stations, this breaks.

## 15.8 ViewBag Usage in HomeController

**File:** `Metro/Views/Home/Index.cshtml`

**Problem:** Heavy use of `ViewBag.Stations`, `ViewBag.RouteStations`, `ViewBag.StationCount`, `ViewBag.Price`, `ViewBag.Error`, `ViewBag.FromStationId`, `ViewBag.ToStationId`. These are magic strings with no compile-time checking. A typo like `ViewBag.StationCount` vs `ViewBag.StationCountt` would silently fail (return null).

## 15.9 No Lazy Loading Configuration

**Problem:** The project does not use lazy loading proxies. Navigation properties loaded via `.Include()` work, but accessing an unloaded navigation property without explicit Include would return null (not throw, just silently null).

## 15.10 Duplicate Route Search Pages

**Problem:** The project has two separate implementations for the same feature:
- HomeController/Index.cshtml (older, ViewBag-based)
- RoutesController/Index.cshtml (newer, ViewModel-based)

This confuses users and doubles maintenance.

## 15.11 Missing GeoJSON Data for Map Lines

**Problem:** The Leaflet map shows markers but does not draw the actual metro line routes on the map. The GPS "Use My Location" feature only draws a dashed line from user to nearest station, not the full route.

## 15.12 No Pagination or Filtering for Stations

**Problem:** The station dropdown loads all 89 stations. While Tom Select provides search, a future system with hundreds of stations would need server-side pagination or filtering.

---

# SECTION 16 — FINAL QUICK REVIEW SHEET

> ⏱️ Read this 30 minutes before your discussion.

## Architecture
```
3-Tier: Metro (Web) → Metro.Core (Business) → Metro.Data (Data)
Patterns: MVC, Repository, DI, Service Layer, DTO
Target: .NET 8.0, SQL Server, EF Core 8
```

## Database (4 Tables)
| Table | Purpose | Key Relationships |
|---|---|---|
| Lines | Metro lines (3 lines) | 1→Many to Stations |
| Stations | All stations (89) | Many→1 to Lines, 1→Many to Connections |
| StationConnections | Graph edges (bidirectional) | FK to Stations (From/To) |
| PricingRules | Fare tiers (3 tiers) | Independent |

## Transfer Stations (5 pairs)
- Sadat (L1↔L2): ID 19 ↔ 46
- Shohadaa (L1↔L2): ID 22 ↔ 43
- Attaba (L2↔L3): ID 44 ↔ 74
- Nasser (L1↔L3): ID 20 ↔ 75
- Cairo University (L2↔L3): ID 50 ↔ 89

## Controllers (2)
| Controller | Key Actions | Assessment |
|---|---|---|
| RoutesController | GET/POST Index | ✅ ViewModels, ✅ Anti-Forgery, ✅ MetroService |
| HomeController | GET/POST Index | ❌ ViewBag, ❌ No Anti-Forgery, ❌ Manual services |

## Key Services Chain
```
RoutesController → IMetroService
  ├── IRouteService → IGraphBuilder (Dijkstra + PriorityQueue)
  ├── ITravelTimeService (stations × 2 min)
  ├── IPricingService (tiered rules)
  ├── ITransferDetectionService (line changes)
  └── IStationRepository + IPricingRuleRepository
```

## Graph Algorithm: Dijkstra
```
Dictionary<int, List<Neighbor>> graph
PriorityQueue<int, int> (stationId, distance)
Key methods: BuildGraphAsync, GetShortestPathAsync, ReconstructPath
Complexity: O((V+E) log V)
Cache: IMemoryCache, 30 min TTL
Edge weight: 1 (uniform)
```

## Pricing Tiers
| Stations | Price |
|---|---|
| 1 – 9 | 8 EGP |
| 10 – 16 | 10 EGP |
| 17+ | 15 EGP |

## Program.cs Essentials
- `AddControllersWithViews()` — MVC
- `AddDbContextPool<MetroDbContext>` — EF Core + pooling
- `AddScoped` for all repos and services
- `AddMemoryCache()` — graph caching
- Middleware: HttpsRedirection → StaticFiles → Routing → Authorization → MapControllerRoute
- Database seeding at startup

## Most Likely Questions
1. **"Explain the architecture"** — 3-tier, MVC, Repository, DI
2. **"How does route finding work?"** — Dijkstra on adjacency graph
3. **"Why ValueGeneratedNever?"** — Seeding with predefined IDs
4. **"Why two controllers doing the same thing?"** — RoutesController is improved version
5. **"RoutesController excluded from compilation?"** — csproj error, `<Compile Remove>`
6. **"How are transfers detected?"** — Compare consecutive stations' LineId
7. **"Why is there no authentication?"** — Public trip planner, no user accounts
8. **"How does GPS nearest station work?"** — Haversine formula
9. **"Why 3 layers?"** — Separation of concerns, testability
10. **"What would you improve?"** — Fix csproj, remove HomeController duplication, add ModelState validation

## Key EF Core Features Used
| Feature | Where |
|---|---|
| `.Include()` | StationRepository (eager loading Line) |
| `.AsNoTracking()` | Graph node/edge queries |
| `.Select()` projection | DTO mapping in queries |
| `ValueGeneratedNever()` | All entity configurations |
| `ApplyConfigurationsFromAssembly()` | DbContext OnModelCreating |
| `DeleteBehavior.Restrict` | All FK relationships |

## Security Status
| Measure | Present? |
|---|---|
| Anti-Forgery (RoutesController) | ✅ |
| Anti-Forgery (HomeController) | ❌ |
| HTTPS Redirection | ✅ |
| HSTS | ✅ (prod) |
| Exception Handler | ✅ |
| Authorization | ❌ (not needed) |
| Overposting Protection | ✅ (ViewModels) |

## LINQ Patterns to Memorize
```csharp
// Include for eager loading
_context.Stations.Include(s => s.Line).FirstOrDefaultAsync(...)

// Projection with AsNoTracking
_context.Stations.AsNoTracking().Select(s => new DTO{...}).ToListAsync()

// Ordering
_context.PricingRules.OrderBy(r => r.MinStations).ToListAsync()

// Client-side mapping after materialization
stations.Select(s => new StationOptionViewModel{...}).OrderBy(s => s.Name).ToList()

// Duplicate detection
existingIds.ToHashSet()  // O(1) lookup
```

---

# SECTION 16 — MOCK ORAL EXAMINATION

> Simulated university project discussion. Questions ordered from easy → hard.
> Each question includes: **Expected Student Answer** + **Follow-up Questions**.

---

## LEVEL 1 — EASY (20 Questions)

---

### Question 1.1 — Project Purpose

**Examiner:** Tell me, what is this project about? What problem does it solve?

**Expected Answer:**
"This project is a Cairo Metro route planner web application. It solves the problem of helping passengers find the shortest path between any two metro stations across the three Cairo Metro lines. The system calculates the optimal route, the ticket price based on how many stations you travel, the estimated travel time, and how many times you need to transfer between lines. It also includes an interactive map with GPS to find the nearest station."

**Follow-up:**
- Q: Who are the intended users?
- A: "Any Cairo Metro passenger. There's no login or authentication — it's a fully public trip planner."
- Q: How many stations and lines does the system currently support?
- A: "89 stations across 3 lines — Line 1 (Helwan to New El-Marg), Line 2 (Shubra to El-Monib), and Line 3 (Adly Mansour extending to Cairo University)."

---

### Question 1.2 — MVC Architecture

**Examiner:** Can you explain the MVC pattern as used in this project?

**Expected Answer:**
"MVC stands for Model-View-Controller. In this project, the **Models** are our entity classes like Station, Line, StationConnection, and PricingRule in the Metro.Core project. The **Views** are the Razor .cshtml files like Index.cshtml in the Views folder. The **Controllers** are RoutesController and HomeController. When a user visits the route search page, the browser sends a request. The routing system maps it to the correct controller action. The controller works with services and repositories to get data, builds a ViewModel, and passes it to the View to render HTML."

**Follow-up:**
- Q: Where are the Views physically located?
- A: "Under the Views folder, organized by controller name — Views/Routes/ for RoutesController and Views/Home/ for HomeController. The shared layout is in Views/Shared/."
- Q: What is the role of _ViewStart.cshtml?
- A: "It sets the default layout to _Layout.cshtml for all views, so each view doesn't have to specify the layout manually."

---

### Question 1.3 — Database Tables

**Examiner:** What tables are in the database?

**Expected Answer:**
"There are four tables. **Lines** stores the three metro lines with their name and color. **Stations** stores all 89 stations with their name, coordinates, the line they belong to, and their order on that line. **StationConnections** stores which stations are connected to which — these are the edges in our graph. And **PricingRules** stores the fare tiers — three rules that map station count ranges to prices."

**Follow-up:**
- Q: Which table has the most records?
- A: "StationsConnections. There are 89 stations but 182 connections — each connection appears twice (bidirectional), plus the transfer connections between lines."
- Q: How are the tables related?
- A: "Station has a foreign key to Line. StationConnection has two foreign keys to Station — one for the from station and one for the to station."

---

### Question 1.4 — What is DbContext?

**Examiner:** What is the DbContext in this project and what does it do?

**Expected Answer:**
"The DbContext is the `MetroDbContext` class in the Metro.Data project. It inherits from `DbContext` and contains four `DbSet` properties — one for each entity: Stations, Lines, StationConnections, and PricingRules. It acts as the bridge between our C# code and the SQL Server database. When we query `_context.Stations.ToListAsync()`, EF Core translates that into SQL, executes it against the database, and maps the results back to Station objects."

**Follow-up:**
- Q: Where is the DbContext configured to connect to the database?
- A: "In Program.cs, using `AddDbContextPool<MetroDbContext>` with the connection string from appsettings.json: `Server=.\\SQLEXPRESS;Database=MetroDb;Trusted_Connection=True`."
- Q: Why AddDbContextPool instead of AddDbContext?
- A: "Pooling reuses DbContext instances across requests instead of creating new ones each time, which improves performance under load."

---

### Question 1.5 — Controllers Overview

**Examiner:** How many controllers does the project have and what are their names?

**Expected Answer:**
"Two controllers. **HomeController** which handles the landing page and has an older route search implementation using ViewBag. And **RoutesController** which has the main route search page with proper ViewModels, anti-forgery protection, and uses MetroService as an orchestrator."

**Follow-up:**
- Q: Why do both controllers seem to do the same thing?
- A: "That's actually a design inconsistency. RoutesController is the improved version with better practices. HomeController appears to be an earlier implementation that wasn't removed."
- Q: Which one should the user actually use?
- A: "RoutesController. It has proper ViewModels, anti-forgery protection, and a cleaner architecture."

---

### Question 1.6 — What is Dependency Injection?

**Examiner:** Can you explain how Dependency Injection is used in this project?

**Expected Answer:**
"Dependency Injection is used extensively. Instead of controllers creating their dependencies with `new`, they declare them in the constructor and the ASP.NET Core DI container provides them automatically. For example, `RoutesController` asks for `IStationRepository` and `IMetroService` in its constructor. These are registered in Program.cs with `AddScoped`. The container resolves the entire chain — it creates the repository, which needs a DbContext, and the MetroService, which needs five other services — all automatically."

**Follow-up:**
- Q: What would happen if you forgot to register IStationRepository?
- A: "The application would compile but at runtime you would get an `InvalidOperationException` saying the DI container cannot resolve IStationRepository."
- Q: Why are all services registered as Scoped?
- A: "Scoped means one instance per HTTP request. This ensures each request gets a fresh DbContext. Singleton would be dangerous because multiple requests would share the same context, causing thread-safety issues."

---

### Question 1.7 — What is a ViewModel?

**Examiner:** What is the purpose of RouteSearchViewModel?

**Expected Answer:**
"RouteSearchViewModel is in the Metro/ViewModels folder. It's a class designed specifically for the route search form. It contains `FromStationId` and `ToStationId` as nullable integers for the form inputs, a `Stations` list for populating the dropdowns, a `Result` property of type `object` to hold the route calculation result, and an `ErrorMessage` string for validation feedback. It packages everything the view needs in one strongly-typed object instead of using ViewBag."

**Follow-up:**
- Q: Why is Result typed as `object` instead of `RouteResultDto`?
- A: "That's a design choice mentioned in the code comments. The View casts it back to `RouteResultDto` when rendering. It would be better to use the concrete type directly."
- Q: What is StationOptionViewModel for?
- A: "It represents a single station in the dropdown with Id, Name, LineName, coordinates, and a computed `DisplayName` property that shows 'Name (LineName)' for display."

---

### Question 1.8 — Route Configuration

**Examiner:** How does the application know which controller to call when a user visits the root URL?

**Expected Answer:**
"In Program.cs, there's a call to `MapControllerRoute` with a default pattern of `{controller=Home}/{action=Index}/{id?}`. The `controller=Home` part means if no controller is specified, it defaults to HomeController. So visiting the root URL `http://localhost:5279/` maps to `HomeController.Index()`."

**Follow-up:**
- Q: What URL would call RoutesController.Index?
- A: "`/Routes/Index` or just `/Routes` since Index is the default action."
- Q: What does the `{id?}` part mean?
- A: "It means an optional `id` parameter. If provided, it's passed to the action method. None of the actions in this project use it currently."

---

### Question 1.9 — Seed Data

**Examiner:** How is initial data loaded into the database?

**Expected Answer:**
"The `MetroDataSeeder` class in Metro.Data/Seed reads JSON files from the SeedData folder — lines.json, stations.json, connections.json, and pricingRules.json. It deserializes them and inserts the records into the database. This runs every time the application starts in Program.cs, but it has duplicate detection — it checks which IDs already exist and only inserts new ones."

**Follow-up:**
- Q: Which folder are the JSON files copied to at build time?
- A: "They're copied to the output directory (bin folder) because the .csproj has `CopyToOutputDirectory: PreserveNewest`."
- Q: Why use JSON instead of hardcoding the data in C#?
- A: "JSON is easier to edit and maintain. Domain experts can update station data without touching C# code."

---

### Question 1.10 — What is Program.cs?

**Examiner:** What is the role of Program.cs in this project?

**Expected Answer:**
"Program.cs is the application entry point. It creates the WebApplication builder, registers all services (MVC, DbContext, repositories, services, memory cache), configures the middleware pipeline (HTTPS redirection, static files, routing, authorization), maps the default route, and seeds the database. Then it calls `app.Run()` to start the web server."

**Follow-up:**
- Q: What would happen if you removed `AddControllersWithViews()`?
- A: "The application would compile but every URL would return 404 because MVC services wouldn't be registered."
- Q: Why does seeding happen inside `using (var scope = ...)`?
- A: "Because `MetroDbContext` is registered as scoped. We need to create a new scope to resolve scoped services from the root container."

---

### Question 1.11 — What is a Navigation Property?

**Examiner:** Can you give me an example of a navigation property in this project?

**Expected Answer:**
"In the `Station` entity, the `Line` property is a navigation property. It's of type `Line` — not a database column but a reference to the related entity. When we use `.Include(s => s.Line)` in our query, EF Core joins the Stations and Lines tables and populates this property. Without Include, accessing `station.Line` would return null."

**Follow-up:**
- Q: What navigation properties does Station have?
- A: "Three: `Line` (the line it belongs to), `FromConnections` (connections starting at this station), and `ToConnections` (connections ending at this station)."
- Q: Are they all the same type of relationship?
- A: "`Line` is a Many-to-One navigation. `FromConnections` and `ToConnections` are One-to-Many navigation properties."

---

### Question 1.12 — What is a DTO?

**Examiner:** What is RouteResultDto and why is it used?

**Expected Answer:**
"RouteResultDto is in Metro.Core/DTOs. DTO stands for Data Transfer Object. It's a simple class with properties like `Stations` (list of station names), `FromStationName`, `ToStationName`, `StationCount`, `Transfers`, `Price`, and `EstimatedTimeMinutes`. It's used to transfer route calculation results between the service layer and the controller without exposing entity objects. It only carries the data the View needs — nothing more."

**Follow-up:**
- Q: Why not just pass the entity objects directly?
- A: "Entities contain more data than needed and create a tight coupling between the data layer and the presentation layer. DTOs are lightweight and you can shape them exactly for your needs."
- Q: Where is RouteResultDto populated?
- A: "In `MetroService.GetRouteAsync()`. After calculating all the route details, it creates and returns a RouteResultDto with the results."

---

### Question 1.13 — Application Settings

**Examiner:** What is stored in appsettings.json?

**Expected Answer:**
"The main appsettings.json has three sections: Logging configuration (log level defaults), AllowedHosts set to asterisk meaning any host can access, and the ConnectionStrings section with the DefaultConnection pointing to a local SQL Express instance with database name MetroDb using Windows Authentication."

**Follow-up:**
- Q: Is there a separate Development settings file?
- A: "Yes, appsettings.Development.json which only overrides the logging levels. The connection string is in the main appsettings.json."
- Q: What does `TrustServerCertificate=True` mean?
- A: "It tells SQL Server to trust the server certificate without validation. This is common in development environments but should be configured properly in production."

---

### Question 1.14 — Static Files

**Examiner:** How does the application serve CSS and JavaScript files?

**Expected Answer:**
"Through `app.UseStaticFiles()` in Program.cs. This middleware serves files from the `wwwroot` folder. The project uses Bootstrap, jQuery, and site.css from wwwroot/lib and wwwroot/css. Additionally, Leaflet and Tom Select are loaded from CDN in the views."

**Follow-up:**
- Q: What would happen if you removed `UseStaticFiles()`?
- A: "The page would render but without styling or client-side functionality — no Bootstrap layout, no map, no searchable dropdowns."
- Q: What does `asp-append-version="true"` on the CSS link do?
- A: "It appends a content-based hash query string. When the file changes, the hash changes, forcing browsers to download the new version instead of using a cached one."

---

### Question 1.15 — Model Binding

**Examiner:** How does the RoutesController receive form data when the user submits?

**Expected Answer:**
"When the user submits the form, the browser sends a POST request with `FromStationId` and `ToStationId` values. ASP.NET Core's model binding automatically creates a `RouteSearchViewModel` object and maps the form field names to the ViewModel properties. The `[HttpPost]` action `Index(RouteSearchViewModel model)` receives this populated object."

**Follow-up:**
- Q: What if the form field names didn't match the ViewModel properties?
- A: "Model binding would fail to populate those properties. The matching is case-insensitive by default in ASP.NET Core."
- Q: Does the HomeController use model binding too?
- A: "It uses simple parameter binding — `int fromStationId, int toStationId`. The form field names must match these parameter names."

---

### Question 1.16 — What is `async` / `await`?

**Examiner:** The code uses async and await everywhere. Why?

**Expected Answer:**
"Because all database operations are I/O bound — they involve network calls to SQL Server. Using `async` and `await` with methods like `ToListAsync()` and `SaveChangesAsync()` prevents the web server thread from blocking while waiting for the database. This allows the thread to handle other requests, improving scalability."

**Follow-up:**
- Q: Can you name three async methods used in the project?
- A: "`GetAllAsync()`, `GetByIdAsync()`, `SaveChangesAsync()` in the repositories. Also `GetRouteAsync()` in MetroService and `BuildGraphAsync()` in GraphBuilder."
- Q: What would happen if you called `.Result` or `.Wait()` instead of `await`?
- A: "It can cause deadlocks in certain synchronization contexts and also blocks the thread, defeating the purpose of async."

---

### Question 1.17 — What is the `private set` on entities?

**Examiner:** Why do all entity properties have `private set`?

**Expected Answer:**
"This is to enforce encapsulation and immutability. The properties can only be set through the constructor when creating a new entity. For example, `new Station(id, name, lineId, latitude, longitude, order)`. EF Core can still set them because it uses the private constructor or reflection to materialize objects from the database."

**Follow-up:**
- Q: Why is there a private parameterless constructor?
- A: "EF Core needs it. When it reads data from the database, it creates the object using the parameterless constructor and then sets the properties via reflection."
- Q: Could you make the setters public instead?
- A: "Technically yes, but that would break encapsulation. Any code could change a station's name or ID, which could lead to inconsistencies."

---

### Question 1.18 — What is the `Order` property?

**Examiner:** Station has an `Order` property. What is it for?

**Expected Answer:**
"The `Order` property defines the position of a station on its metro line. For example, on Line 1, Helwan has Order 1, Ain Helwan has Order 2, and so on until New El-Marg with Order 35. It's used in `GetByLineIdAsync` where stations are sorted `.OrderBy(s => s.Order)` to display them in the correct sequence."

**Follow-up:**
- Q: Is there a database index on this?
- A: "Yes, a composite index on (LineId, Order) to optimize queries that filter by line and sort by order."
- Q: What would happen if two stations on the same line had the same Order value?
- A: "Their relative order would be undefined. There's no unique constraint on the combination, so it would allow duplicates but the ordering would be arbitrary."

---

### Question 1.19 — Error Handling

**Examiner:** How does the application handle errors?

**Expected Answer:**
"There are two layers. First, in RoutesController, the POST action has a try-catch that catches exceptions from MetroService and displays a friendly error message. Second, Program.cs configures `UseExceptionHandler("/Home/Error")` for production, which redirects to the Error view. The Error action also uses `[ResponseCache(NoStore = true)]` to prevent caching of error pages."

**Follow-up:**
- Q: What happens in development mode with errors?
- A: "In Development mode, `UseExceptionHandler` is not applied, so ASP.NET Core shows the detailed developer exception page with stack traces. This is configured by the `if (!app.Environment.IsDevelopment())` check."
- Q: What does the Error ViewModel contain?
- A: "It has a `RequestId` string and a computed `ShowRequestId` boolean. It shows the current request's trace identifier for debugging."

---

### Question 1.20 — Launch Settings

**Examiner:** What is launchSettings.json used for?

**Expected Answer:**
"It configures development-time profiles. The project has three profiles: `http` on port 5279, `https` on ports 7105 and 5279, and an `IIS Express` profile. All profiles set the environment to Development and launch the browser automatically. The https profile uses both HTTP and HTTPS URLs."

**Follow-up:**
- Q: Which profile is used when you press F5 in Visual Studio?
- A: "The first profile listed, which is `http`. But Visual Studio typically picks the `https` profile."
- Q: Can you change the port?
- A: "Yes, by editing the `applicationUrl` value in launchSettings.json."

---

## LEVEL 2 — MEDIUM (30 Questions)

---

### Question 2.1 — RoutesController.Index (GET) Explained

**Examiner:** Walk me through what happens when a user visits `/Routes/Index`.

**Expected Answer:**
"The request hits the `RoutesController.Index()` GET action. First, it creates a new `RouteSearchViewModel`. Then it calls the private helper `LoadStationsAsync()`, which goes to `StationRepository.GetAllAsync()`. That method uses EF Core to query all stations with `.Include(s => s.Line)` — so it SQL joins the Lines table. The results are mapped to `StationOptionViewModel` objects with the computed `DisplayName` and ordered alphabetically by name. Finally, the view model with the stations list is passed to the `Views/Routes/Index.cshtml` view, which renders the form with two Tom Select dropdowns, the Leaflet map, and the Find Route button."

**Follow-up:**
- Q: Why does `LoadStationsAsync` map to a ViewModel instead of using the entity directly?
- A: "Because the view only needs Id, Name, LineName, and coordinates — not all entity properties. Also, the `DisplayName` computed property is needed for the dropdown. ViewModels let us shape data exactly for the view."
- Q: What SQL query does the repository generate?
- A: "`SELECT s.Id, s.Name, s.LineId, s.Latitude, s.Longitude, s.Order, l.Id, l.Name, l.Color FROM Stations s LEFT JOIN Lines l ON s.LineId = l.Id`."

---

### Question 2.2 — RoutesController.Index (POST) Explained

**Examiner:** Now walk me through what happens when the user clicks Find Route.

**Expected Answer:**
"The browser POSTs the form to the same URL. Model binding creates a `RouteSearchViewModel` with the selected station IDs. The action first manually validates: checks both IDs are not null, and that they're different. If validation fails, it sets an error message and returns the view. If valid, it calls `_metroService.GetRouteAsync(fromId, toId)`. If successful, the returned `RouteResultDto` is assigned to `model.Result`. If an exception occurs, it's caught and a friendly error message is shown. In both cases, the stations list is reloaded for the dropdowns."

**Follow-up:**
- Q: Why is the stations list reloaded even on error?
- A: "Because the view always needs the stations list to render the dropdowns. If we don't reload it, `Model.Stations` would be empty and the dropdowns would break."
- Q: What is the `[ValidateAntiForgeryToken]` attribute doing?
- A: "It validates that the anti-forgery token from the form matches the token in the cookie, preventing Cross-Site Request Forgery attacks."

---

### Question 2.3 — Dijkstra Implementation

**Examiner:** You used Dijkstra's algorithm for route finding. Explain how it works in your code.

**Expected Answer:**
"The algorithm is in `RouteService.GetShortestPathAsync()`. First, we call `GraphBuilder.BuildGraphAsync()` to get the adjacency dictionary. We initialize all distances to infinity except the start station which is 0. We use a `PriorityQueue<int, int>` to always process the closest unvisited station. For each neighbor, we calculate a new distance and if it's shorter than the current one, we update and push to the queue. When we reach the destination, we call `ReconstructPath()` which follows the `previous` dictionary from the end station back to the start, then reverses the list to get the correct order."

**Follow-up:**
- Q: What is the time complexity?
- A: "O((V + E) log V) where V is the number of stations and E is the number of connections. With 89 stations and about 182 connections, it runs in milliseconds."
- Q: Why use Dijkstra instead of BFS since all edges have weight 1?
- A: "Dijkstra works with weighted graphs. Even though all current edges have weight 1, the implementation is ready for future scenarios where some connections might have different weights — like express connections with different travel times."

---

### Question 2.4 — GraphBuilder Caching

**Examiner:** How does GraphBuilder use caching and why?

**Expected Answer:**
"GraphBuilder uses `IMemoryCache` injected through the constructor. When `BuildGraphAsync()` is called, it first checks if the graph exists in cache using the key `"metro_graph"`. If found, it returns the cached graph. If not, it queries all stations and connections from the database, builds the adjacency dictionary, stores it in cache with a 30-minute expiration, and returns it. This avoids rebuilding the graph from scratch on every request, since the metro structure rarely changes."

**Follow-up:**
- Q: Why 30 minutes? Why not 24 hours?
- A: "30 minutes is a balance between performance and freshness. If station data changes, the cache will refresh within 30 minutes. A longer duration would risk serving stale data."
- Q: What type of cache is IMemoryCache?
- A: "It's an in-memory cache stored on the web server. It's registered as Singleton by `AddMemoryCache()`. The cached data is lost if the application restarts."

---

### Question 2.5 — Transfer Detection

**Examiner:** How does the system detect when a passenger needs to transfer?

**Expected Answer:**
"The `TransferDetectionService` in Metro.Core/Services has a `CountTransfers` method. It receives the list of stations along the route in order. It iterates from the second station onwards, comparing each station's `LineId` with the previous station's `LineId`. Every time they differ, it increments the transfer count. For example, if you go from Sadat on Line 1 (ID 19) to Sadat on Line 2 (ID 46), the LineId changes from 1 to 2, so one transfer is counted."

**Follow-up:**
- Q: What if someone stays on the same line the whole trip?
- A: "The transfer count would be 0. The loop would find no LineId changes. This is correct — no transfer needed."
- Q: Could this logic ever double-count a transfer at the same station?
- A: "No, because each adjacent pair is compared once. If the route goes Line 1 → Line 2 → Line 1, that's two transfers. Each line change increments once."

---

### Question 2.6 — Pricing Calculation

**Examiner:** How is the ticket price determined?

**Expected Answer:**
"The `PricingService.CalculatePrice` method takes the station count and the list of pricing rules. It iterates through the rules and finds the first one where `IsMatch(stationCount)` returns true. Each rule has a MinStations and MaxStations range. Rule 1 covers 1 to 9 stations and costs 8 EGP. Rule 2 covers 10 to 16 stations and costs 10 EGP. Rule 3 covers 17 to 999 stations and costs 15 EGP. If no rule matches, it throws an exception."

**Follow-up:**
- Q: What does the `IsMatch` method look like?
- A: "It's defined on the PricingRule entity itself: `return stationCount >= MinStations && stationCount <= MaxStations`."
- Q: What happens if you travel 1000 stations?
- A: "Rule 3 has a MaxStations of 999, so 1000 would not match any rule and the service would throw an `InvalidOperationException`. This is a limitation — the upper bound should be `int.MaxValue`."

---

### Question 2.7 — Travel Time Calculation

**Examiner:** How is estimated travel time calculated?

**Expected Answer:**
"The `TravelTimeService` has a constant `MinutesPerStation = 2`. Its `CalculateTravelTime` method simply returns `stationCount * 2`. So if you travel through 20 stations, the estimated time is 40 minutes. There's a guard clause that throws if stationCount is negative."

**Follow-up:**
- Q: Is this realistic? Do all stations take exactly 2 minutes?
- A: "It's a simplification. In reality, travel times vary between stations. However, for a university project, this provides a reasonable estimate. The service layer makes it easy to replace with a more sophisticated calculation later."
- Q: Does transfer time affect the estimate?
- A: "Currently no. The estimate is purely based on station count. Waiting time for the next train during a transfer is not included."

---

### Question 2.8 — Include vs No Tracking

**Examiner:** In `StationRepository`, you have methods using both `.Include()` and `.AsNoTracking()`. Why the difference?

**Expected Answer:**
"`.Include(s => s.Line)` is used in `GetByIdAsync` and `GetAllAsync` because these methods return full `Station` objects with their related `Line` data. The caller may access `station.Line.Name`, which requires the Line to be loaded. `.AsNoTracking()` is used in `GetAllStationsAsync` because we're projecting to `StationGraphNode` — a lightweight DTO. We don't need change tracking because the data is read-only for graph building. AsNoTracking improves performance by skipping the change tracker."

**Follow-up:**
- Q: Is it safe to use AsNoTracking if we might later update the data?
- A: "No. AsNoTracking entities are not tracked by the context, so `SaveChangesAsync` would not detect changes to them. You would need to explicitly attach them or requery."
- Q: Which is faster?
- A: "AsNoTracking is faster because the change tracker doesn't monitor the returned objects. The projection (Select) also reduces the columns returned."

---

### Question 2.9 — StationConnection Bidirectional Design

**Examiner:** Why does StationConnection have both FromStationId and ToStationId? Why not just store undirected connections?

**Expected Answer:**
"The connection is directional — it represents an edge from one station to another. However, the GraphBuilder adds both directions explicitly so the graph is effectively undirected. Each connection in the JSON file has a corresponding reverse connection (e.g., connection ID 1: station 1→2, ID 2: station 2→1). This design allows for future scenarios where travel might be one-directional — for example, if a line has a one-way section due to construction."

**Follow-up:**
- Q: Why does GraphBuilder.AddEdge also check both directions?
- A: "It's defensive. Even if only one direction exists in the JSON, AddEdge creates both directions by being called twice: once for From→To and once for To→From. The `edgeSet` HashSet prevents duplicate edges."
- Q: Does the UI show direction?
- A: "No. The route result just lists stations in order. The directionality is internal to the algorithm."

---

### Question 2.10 — HomeController GET vs POST

**Examiner:** What are the differences between the GET and POST Index actions in HomeController?

**Expected Answer:**
"The GET action simply loads all stations into ViewBag and returns the view to display the form. The POST action receives the selected station IDs, validates they're different, calculates the route using `_routeService.GetShortestPathAsync()`, resolves station names, calculates pricing using `_pricingRuleRepository`, and stores everything in ViewBag properties like `ViewBag.RouteStations`, `ViewBag.StationCount`, `ViewBag.Price`. Then the view checks `ViewBag.RouteStations != null` to decide whether to show the result section."

**Follow-up:**
- Q: Why does the POST action load stations again?
- A: "The ViewBag.Stations data is lost after a POST. The view needs it to render the dropdowns. So it must be reloaded."
- Q: What happens if `_routeService.GetShortestPathAsync` throws?
- A: "There's no try-catch. The exception would propagate to the error handler middleware, and the user would see the error page instead of a friendly message."

---

### Question 2.11 — Data Annotations Absence

**Examiner:** I notice there are almost no data annotations like `[Required]` on your models. Why?

**Expected Answer:**
"This project uses manual validation inside the controller actions instead of data annotations. The RoutesController checks for null values and same-station selection explicitly with if-statements. This works but it's a design choice that trades off the convenience of automatic validation for explicit control. Adding `[Required]` attributes would enable automatic client-side and server-side validation."

**Follow-up:**
- Q: What would you need to change to use data annotations?
- A: "Add `[Required]` to `FromStationId` and `ToStationId` in RouteSearchViewModel, then check `ModelState.IsValid` in the controller instead of manual null checks. Also add validation CSS classes to the view."
- Q: What client-side library would handle the validation?
- A: "jQuery Validation and jQuery Unobtrusive Validation, which are already included via the `_ValidationScriptsPartial` partial view."

---

### Question 2.12 — Lambda Expression in Pricing

**Examiner:** In HomeController, you have `.FirstOrDefault(rule => rule.IsMatch(stationCount))`. Explain this line.

**Expected Answer:**
"`pricingRules` is a `List<PricingRule>`. `FirstOrDefault` takes a lambda expression `rule => rule.IsMatch(stationCount)` as a predicate. It iterates through the list and returns the first PricingRule where `IsMatch` returns true — meaning the station count falls within that rule's Min-Max range. If no rule matches, it returns null. The `?.Price ?? 0` is a null-conditional operator: if the result is null, price defaults to 0."

**Follow-up:**
- Q: Could there be overlapping ranges in the pricing rules?
- A: "Currently no — 1-9, 10-16, 17-999 are non-overlapping. But if there were overlaps, `FirstOrDefault` would return the first match. `OrderBy(r => r.MinStations)` ensures rules are checked from lowest to highest."
- Q: What if `pricingRules` is empty?
- A: "`.Any()` would be false, so `FirstOrDefault` would match nothing. The `?.Price ?? 0` would set price to 0. The route would be free."

---

### Question 2.13 — Path Reconstruction

**Examiner:** How does `ReconstructPath` work in RouteService?

**Expected Answer:**
"`ReconstructPath` is a static method that takes the `previous` dictionary and the `endId`. The `previous` dictionary maps each station to the station we came from to reach it via the shortest path. Starting from the end station, we repeatedly follow the `previous` pointer until we reach null (the start station). Each station ID is added to a list. Finally, we reverse the list so it goes from start to end."

**Follow-up:**
- Q: Why is it static?
- A: "It doesn't use any instance state — it only works with its parameters. Making it static communicates that it's a pure function without side effects."
- Q: What would happen if the path doesn't exist?
- A: "The code checks if `distances[endId] == int.MaxValue` before calling ReconstructPath and throws `InvalidRouteException`. If somehow ReconstructPath was called with an unreachable destination, the `previous` chain would eventually reach null and produce an incomplete path."

---

### Question 2.14 — Seed Duplicate Detection

**Examiner:** How does the seed system avoid inserting duplicate records if the app restarts?

**Expected Answer:**
"Each seed method first queries the existing IDs from the database: `existingIds = (await context.Stations.Select(s => s.Id).ToListAsync()).ToHashSet()`. Then it filters the seed data: `.Where(station => !existingStationIds.Contains(station.Id))`. Only stations with IDs not already in the database are added. If all data already exists, the newLines list is empty and the method returns early without calling SaveChanges."

**Follow-up:**
- Q: Why use `ToHashSet()` instead of just a list?
- A: "`HashSet<T>` has O(1) lookup time. A list has O(n). For 89 records it doesn't matter much, but it's good practice for larger datasets."
- Q: What if the seed data has an ID of 0 or negative?
- A: "There's a filter `.Where(station => station.Id > 0)` that excludes non-positive IDs."

---

### Question 2.15 — Connections Seed Grouping

**Examiner:** Why does the connection seeding use `.GroupBy(connection => connection.Id).Select(group => group.First())`?

**Expected Answer:**
"This handles duplicate connection IDs in the JSON file. The `GroupBy` groups all entries with the same ID together, and `Select(group => group.First())` takes only the first one from each group. This ensures we never try to insert two connections with the same primary key, which would cause a database constraint violation."

**Follow-up:**
- Q: Why would there be duplicates in the JSON file?
- A: "The connections.json has 182 entries. Each physical connection appears twice (A→B and B→A). But they have different IDs — one for each direction. So duplicates aren't from directionality. They could be from manual editing errors in the JSON."
- Q: Is there a better way to handle this?
- A: "Using a HashSet of IDs before insertion would be cleaner and more explicit about the deduplication intent."

---

### Question 2.16 — Entity Private Constructors

**Examiner:** Why does the `Station` entity have both a parameterized constructor and a private parameterless constructor?

**Expected Answer:**
"The parameterized constructor `Station(int id, string name, int lineId, ...)` is used by the application code to create new Station instances — for example, in the seed methods. The private parameterless constructor `private Station() { }` is required by EF Core. When EF Core materializes query results from the database, it needs a constructor to create the object. It can use the private one because EF Core can access private members through reflection."

**Follow-up:**
- Q: Could you make the parameterless constructor public?
- A: "Yes, but that would allow any code to create an invalid Station without setting properties. Making it private enforces that proper construction goes through the parameterized constructor."
- Q: What if you removed the private constructor entirely?
- A: "EF Core would throw an exception when trying to materialize query results because it can't find a suitable constructor."

---

### Question 2.17 — MetroService as Facade

**Examiner:** What is the role of MetroService and why is it designed this way?

**Expected Answer:**
"MetroService is an orchestrator — it implements the Facade pattern. Instead of the controller calling multiple services directly, the controller calls one method: `GetRouteAsync`. MetroService internally coordinates five different services: RouteService for path finding, TravelTimeService for time estimate, PricingService for fare, TransferDetectionService for transfer count, and StationRepository for resolving station names. This simplifies the controller and centralizes the route calculation logic."

**Follow-up:**
- Q: What if you wanted to add a new feature like wheelchair-accessible routes?
- A: "I would create a new service, inject it into MetroService, and add the new data to the RouteResultDto. The controller wouldn't need to change at all."
- Q: Why is this better than putting all logic in the controller?
- A: "Separation of concerns. Each service has a single responsibility. They can be unit tested independently. The controller stays thin and focused on HTTP concerns."

---

### Question 2.18 — Index in StationConnection

**Examiner:** Why does StationConnection have indexes on FromStationId and ToStationId?

**Expected Answer:**
"The configuration creates indexes on both foreign key columns. This speeds up queries that filter by station — for example, `GetConnectionsByStationIdAsync` queries `WHERE FromStationId = @id OR ToStationId = @id`. Without indexes, SQL Server would need to scan the entire table. Also, the GraphBuilder queries all connections at once, but having indexes is still beneficial for any lookup by station."

**Follow-up:**
- Q: Is there a performance concern with too many indexes?
- A: "Indexes speed up reads but slow down writes (INSERT/UPDATE/DELETE) because the index must be maintained. Since this system rarely writes data (only at seeding), the read performance benefit outweighs the write cost."
- Q: What is the composite index on Station (LineId, Order) for?
- A: "It optimizes the `GetByLineIdAsync` query which filters by LineId and orders by Order — the exact columns in the index."

---

### Question 2.19 — DeleteBehavior.Restrict

**Examiner:** All foreign keys use `DeleteBehavior.Restrict`. Why?

**Expected Answer:**
"`DeleteBehavior.Restrict` prevents accidental cascade deletes. If someone tries to delete a Line that has stations, the database will throw a foreign key constraint error instead of silently deleting all those stations. Similarly, you can't delete a Station that has connections. This protects data integrity. The default behavior in EF Core is Cascade, which would be dangerous here."

**Follow-up:**
- Q: What is the difference between Restrict and SetNull?
- A: "Restrict prevents the delete entirely. SetNull would set the foreign key to NULL on the related records, which would break the data because a station must belong to a line."
- Q: How would you delete a Line if needed?
- A: "You would need to manually delete or reassign all its stations first, then delete the line. This is intentional — it forces explicit handling."

---

### Question 2.20 — Exception Types

**Examiner:** What custom exception classes does the project have?

**Expected Answer:**
"Two custom exceptions in Metro.Core/Exceptions. `StationNotFoundException` is thrown when a station ID cannot be found in the database. `InvalidRouteException` is thrown when no route exists between two stations or when the start and end stations are the same. Both extend `Exception` with constructors for message and inner exception."

**Follow-up:**
- Q: Where is StationNotFoundException thrown?
- A: "In MetroService when `GetByIdAsync` returns null — `?? throw new StationNotFoundException(...)`. Also in RouteService when a station ID is not found in the graph dictionary."
- Q: Why create custom exceptions instead of using built-in ones?
- A: "They make error handling more specific. The controller can catch these and show meaningful messages. They also make the code more readable — you know exactly what went wrong."

---

### Question 2.21 — JavaScript Form Validation

**Examiner:** What client-side validation exists before the form is submitted?

**Expected Answer:**
"There are two layers. First, the HTML `<select>` elements have the `required` attribute — the browser won't submit with an empty value. Second, JavaScript intercepts the form's submit event and checks if both stations are selected and are different. If they're the same, `e.preventDefault()` stops submission and a styled error banner is shown above the submit button."

**Follow-up:**
- Q: Can a user bypass client-side validation?
- A: "Yes. By disabling JavaScript or using tools like curl or Postman. That's why server-side validation in the controller is essential."
- Q: What validation happens if JavaScript is disabled?
- A: "The `required` HTML attribute still provides basic browser validation. The server-side code in RoutesController checks for null values and same-station selection."

---

### Question 2.22 — Haversine Formula

**Examiner:** Explain the Haversine formula used in the GPS feature.

**Expected Answer:**
"The Haversine formula calculates the great-circle distance between two points on a sphere using their latitude and longitude. In our JavaScript code, it takes the user's GPS coordinates and each station's coordinates. It converts everything from degrees to radians, calculates the differences, applies the Haversine trigonometric formula, and returns the distance in kilometers. We find the station with the minimum distance and auto-select it in the dropdown."

**Follow-up:**
- Q: Why not use a simpler approximation?
- A: "Haversine is accurate for any two points on Earth, regardless of distance. Simpler formulas like the Pythagorean approximation are only accurate for small distances and break down near the poles."
- Q: How does the map show the nearest station?
- A: "It places two markers — one for the user's location, one for the station. A dashed red polyline connects them. Then `map.fitBounds()` adjusts the view to show both markers."

---

### Question 2.23 — Tom Select

**Examiner:** Why did you use the Tom Select library for the dropdowns?

**Expected Answer:**
"Tom Select transforms the standard HTML `<select>` into a searchable dropdown. With 89 stations in the list, a native dropdown would be difficult to navigate — the user would have to scroll through all 89 options. Tom Select lets them type a station name to filter the list. It also supports keyboard navigation and has a clean, customizable UI."

**Follow-up:**
- Q: How is Tom Select initialized?
- A: "In the JavaScript section of the view: `new TomSelect('#FromStationId', { create: false, sortField: { field: 'text', direction: 'asc' } })`."
- Q: What if the CDN is unavailable?
- A: "The dropdowns would fall back to native HTML `<select>` elements. They'd still work but without search functionality."

---

### Question 2.24 — Application URLs

**Examiner:** What URLs would the application respond to?

**Expected Answer:**
"The application responds to several URLs based on the default route. `/` or `/Home` goes to `HomeController.Index`. `/Home/Privacy` goes to the Privacy page. `/Routes` or `/Routes/Index` goes to `RoutesController.Index` which accepts both GET and POST. `/Home/Error` is the error page. The default route pattern is `{controller=Home}/{action=Index}/{id?}`, so any controller name and action name combination maps to the appropriate endpoint."

**Follow-up:**
- Q: What would `/Routes/Index/5` do?
- A: "The `5` would be bound to the optional `id` parameter. But RoutesController.Index doesn't accept an `id` parameter, so it would be ignored. No error would occur."
- Q: How would you add a new page?
- A: "Create a new action method in an existing controller or create a new controller, add a corresponding view, and the routing would automatically map to it."

---

### Question 2.25 — ErrorViewModel ShowRequestId

**Examiner:** Why does ErrorViewModel have a computed `ShowRequestId` property?

**Expected Answer:**
"It computes `!string.IsNullOrEmpty(RequestId)`. In the Error view, it's used to conditionally display the Request ID: `@if (Model.ShowRequestId)`. If the RequestId is null or empty, the paragraph with the request ID code is not rendered. This prevents showing an empty or meaningless identifier."

**Follow-up:**
- Q: Where does the RequestId come from?
- A: "From `Activity.Current?.Id ?? HttpContext.TraceIdentifier`. `Activity.Current` is from System.Diagnostics for distributed tracing. If that's null, it falls back to the ASP.NET Core request's unique trace identifier."
- Q: Could you always show the RequestId?
- A: "Yes, but it's less user-friendly. Showing a null or empty ID would confuse users. The computed property keeps the view clean."

---

### Question 2.26 — ViewBag in Home View

**Examiner:** The Home/Index.cshtml uses ViewBag extensively. How does the view know what data to expect?

**Expected Answer:**
"The view is loosely typed — there's no `@model` directive. It accesses `ViewBag.Stations`, `ViewBag.RouteStations`, `ViewBag.StationCount`, etc. There's no compile-time guarantee these exist. If a developer misspells `ViewBag.StationCount` as `ViewBag.StationCountt`, it silently returns null without any error. The code has null checks like `@if (stations != null)` to handle missing data safely."

**Follow-up:**
- Q: What happens if the controller forgets to set ViewBag.Stations?
- A: "The foreach loop `foreach (var station in stations)` where stations is null would throw a NullReferenceException at runtime. However, the view checks `@if (stations != null)` before iterating."
- Q: How is this different from the Routes/Index.cshtml approach?
- A: "Routes/Index uses `@model RouteSearchViewModel` — strongly typed. All properties are known at compile time. IntelliSense works. No magic strings."

---

### Question 2.27 — Transfer Station Visualization

**Examiner:** How does the Home/Index.cshtml visualize transfer stations in the route result?

**Expected Answer:**
"The view has complex Razor logic. It first identifies transfer stations by comparing consecutive stations' LineId values. Then it groups stations by line — when the LineId changes, it starts a new group. Each group is displayed as a separate section with a colored header (red for Line 1, blue for Line 2, green for Line 3). Stations at the start of a new group or identified as transfers show a 'Transfer' badge."

**Follow-up:**
- Q: What is the line color mapping logic?
- A: "`var lineClass = lineId == 1 ? "line-red" : lineId == 2 ? "line-blue" : "line-green"`. This is hardcoded and would need to be updated if a new line is added."
- Q: What CSS classes control the colors?
- A: "`.line-red span { background: #e63946 }`, `.line-blue span { background: #2563eb }`, `.line-green span { background: #10b981 }` — defined in the view's style block."

---

### Question 2.28 — Route Display in Routes View

**Examiner:** How does Routes/Index.cshtml display the route result?

**Expected Answer:**
"The view casts `Model.Result` to `RouteResultDto` at the top: `var result = Model.Result as RouteResultDto`. It checks `if (result != null)` to conditionally render the result card. The result card shows endpoint pills (From → To), a summary grid with four stats (station count, transfers, price in EGP, estimated time in minutes), and an ordered station path list with numbered dots connected by a vertical gradient track line."

**Follow-up:**
- Q: Why cast `Model.Result` at the top of the view?
- A: "Because the ViewModel defines `Result` as `object?`. Casting once at the top avoids casting it multiple times in the view. The `as` keyword returns null if the cast fails rather than throwing."
- Q: What animation does the result card use?
- A: "A CSS `@keyframes slideUp` animation — it fades in and slides up from 28px below over 0.4 seconds using a cubic bezier easing curve."

---

### Question 2.29 — Line Registration Status

**Examiner:** I notice `ILineRepository` is registered in DI but never used. Why is that?

**Expected Answer:**
"That's dead code. It's registered in Program.cs but no controller or service currently injects it. It was likely intended for a feature like displaying line details. The `LineRepository.GetByIdAsync` with `.Include(l => l.Stations)` would be useful for showing all stations on a specific line, but that feature isn't implemented in the UI."

**Follow-up:**
- Q: Should you remove it?
- A: "Either remove it to clean up dead code, or keep it if there are plans to add a line details page. Having unused code can confuse developers."
- Q: Would the application crash because of it?
- A: "No. Registration without usage is harmless. The DI container only resolves services when they're actually needed."

---

### Question 2.30 — Validation Error Display

**Examiner:** How are validation errors shown to the user in the Routes view?

**Expected Answer:**
"Two types of errors. First, `ModelState` errors are shown via `@Html.ValidationSummary(false, ...)` which renders model-level validation errors inside a yellow warning box with an alert icon. Second, the `Model.ErrorMessage` string is shown when not null or empty, in a red error box with a circle-exclamation icon. Additionally, client-side JavaScript can show an error banner for same-station selection without a server round-trip."

**Follow-up:**
- Q: Why have both ModelState validation and ErrorMessage?
- A: "ErrorMessage is the custom error message set manually in the controller. ModelState errors would be used if data annotations were present. Currently, the manual validation sets ErrorMessage, so the ModelState section is rarely triggered."
- Q: What CSS style is used for the error box?
- A: "`.error-message-box` — red background (#fff0f0), red left border, red text. It uses flexbox to align the SVG icon with the message text."

---

## LEVEL 3 — HARD (30 Questions)

---

### Question 3.1 — RoutesController Excluded from Build

**Examiner:** If I open the `.csproj` file, I see `<Compile Remove="Controllers\RoutesController.cs" />`. Can you explain this?

**Expected Answer:**
"Yes, this is in the Metro.csproj file. The `<Compile Remove>` directive explicitly excludes the RoutesController.cs file from compilation. This means the C# compiler does not compile this file. However, there's also a `<None Include="Controllers\RoutesController.cs" />` that includes it as a content item (not compiled). This means the RoutesController — which is the main controller for the route search functionality — would not work. The `/Routes/Index` endpoint would return a 404 error."

**Follow-up:**
- Q: Was this intentional?
- A: "I believe it's a configuration error. Perhaps someone was testing something and accidentally excluded it. Or it might have been excluded because there are two controllers doing the same thing and they wanted to disable one. Regardless, it should be fixed by removing the `<Compile Remove>` line."
- Q: How would you fix it?
- A: "Remove lines 17-18 from Metro.csproj: `<Compile Remove="Controllers\RoutesController.cs" />`. Also remove the `<None Include="Controllers\RoutesController.cs" />` on line 26-27, because the file would be automatically included as a Compile item by convention."

---

### Question 3.2 — HomeController Missing Anti-Forgery

**Examiner:** The HomeController's POST Index action doesn't have `[ValidateAntiForgeryToken]`. What is the risk?

**Expected Answer:**
"The risk is Cross-Site Request Forgery (CSRF). An attacker could create a malicious website with a form that automatically submits to our `/Home/Index` endpoint. If a user visits the attacker's site while logged into our app, the form could be submitted without the user's knowledge. However, in this project there's no authentication, so the practical risk is low — the attacker could only calculate routes, not modify data. But it's still a security best practice violation, and RoutesController has the protection, so HomeController should too."

**Follow-up:**
- Q: How would you add it?
- A: "Add `[ValidateAntiForgeryToken]` attribute to the POST action, and add `@Html.AntiForgeryToken()` inside the form in Views/Home/Index.cshtml."
- Q: Since there's no authentication, is this really a problem?
- A: "It's still a coding standard issue. Future developers might copy the HomeController pattern and forget anti-forgery in other forms. Consistency is important."

---

### Question 3.3 — Why No ModelState.IsValid?

**Examiner:** Why does the project never check `ModelState.IsValid`?

**Expected Answer:**
"The project relies on manual validation rather than data annotations. The controller checks conditions explicitly with if-statements. The `ModelState.IsValid` property would only be useful if we had `[Required]` or other validation attributes on the ViewModel. Since we don't, ModelState would always be valid even with bad input. The manual approach works but is less elegant — it doesn't integrate with client-side validation, and it requires explicit error message handling."

**Follow-up:**
- Q: What would you need to change to use ModelState.IsValid?
- A: "Add `[Required(ErrorMessage = "Please select a station.")]` to `FromStationId` and `ToStationId` in the ViewModel. Then replace the manual null checks with `if (!ModelState.IsValid) return View(model)`."
- Q: Would adding data annotations affect the database?
- A: "No. Data annotations like `[Required]` on ViewModels are purely for validation. They don't affect the database schema. Database constraints are configured via Fluent API in the entity configurations."

---

### Question 3.4 — The `?.` and `??` Operators

**Examiner:** In `LoadStationsAsync`, you use `s.Line?.Name ?? string.Empty`. Explain both operators.

**Expected Answer:**
"The `?.` is the null-conditional operator. If `s.Line` is null (because the Include failed or Line wasn't loaded), it short-circuits and returns null instead of throwing a NullReferenceException when accessing `.Name`. The `??` is the null-coalescing operator. If the left side (the result of `s.Line?.Name`) is null, it provides the fallback value `string.Empty`. So the final result is either the station's line name or an empty string."

**Follow-up:**
- Q: Why can `s.Line` be null if you used Include?
- A: "Include should load it, but defensive coding prevents crashes if something goes wrong. For example, if there's a station with a LineId that doesn't reference an existing line, the LEFT JOIN would return null for the Line columns."
- Q: What is the alternative without these operators?
- A: "A ternary: `s.Line != null ? s.Line.Name : string.Empty`. The `?.` and `??` operators are more concise."

---

### Question 3.5 — The `as` Keyword in View

**Examiner:** In Routes/Index.cshtml, why use `Model.Result as RouteResultDto` instead of `(RouteResultDto)Model.Result`?

**Expected Answer:**
"The `as` keyword performs a safe cast. If `Model.Result` is not a `RouteResultDto` (for example, if it's null or a different type), `as` returns null without throwing an exception. A direct cast with parentheses `(RouteResultDto)Model.Result` would throw an `InvalidCastException` if the types don't match. Since `Model.Result` is typed as `object`, using `as` is safer in a Razor view where unhandled exceptions crash the page."

**Follow-up:**
- Q: Could you avoid this cast entirely?
- A: "Yes. Change the `Result` property type from `object?` to `RouteResultDto?` in RouteSearchViewModel. Then no cast would be needed and the view would be type-safe."
- Q: What if `Result` is null?
- A: "The check `if (result != null)` before the result card section handles that. If null, the result section is not rendered."

---

### Question 3.6 — Dijkstra PriorityQueue

**Examiner:** Explain how the `PriorityQueue` is used in the Dijkstra implementation.

**Expected Answer:**
"The `PriorityQueue<int, int>` is a C# built-in min-heap. The first type parameter is the element (station ID), and the second is the priority (current distance from start). We initialize with the start station at priority 0. In each iteration, `TryDequeue` gives us the station with the smallest distance. If the dequeued distance is larger than the recorded distance, we skip it (this handles stale entries). For each neighbor, if we find a shorter path, we update the distances dictionary and enqueue the neighbor with the new distance."

**Follow-up:**
- Q: Why check `if (currentDistance > distances[currentStationId]) continue`?
- A: "Because the PriorityQueue may contain stale entries. When we find a shorter path to a station, we enqueue it again with a lower priority. The old, higher-priority entry is still in the queue. When dequeued, it has a larger distance than the current best, so we skip it."
- Q: Could you use a SortedSet instead?
- A: "Yes, but PriorityQueue is simpler and more efficient. It was introduced in .NET 6 and is specifically designed for this use case."

---

### Question 3.7 — Edge Weight Significance

**Examiner:** All your graph edges have weight 1. Why use Dijkstra at all? BFS would work.

**Expected Answer:**
"That's correct. With uniform weight 1, BFS would also find the shortest path in terms of station count. However, using Dijkstra provides several advantages. First, it makes the system extensible — if we later add different edge weights (e.g., 3 minutes for a long connection between distant stations, or extra weight for transfers), Dijkstra handles it without modification. Second, the PriorityQueue implementation is still O((V+E) log V) which is efficient enough. Third, it demonstrates a more general algorithm suitable for real-world metro systems where connections have varying travel times."

**Follow-up:**
- Q: What would be the BFS time complexity?
- A: "O(V + E) — slightly faster than Dijkstra's O((V+E) log V). But for 89 stations, the difference is negligible."
- Q: Could you add weight to transfer connections to make them less desirable?
- A: "Yes. In the GraphBuilder, when creating edges between transfer stations (e.g., Sadat L1 → Sadat L2), assign a higher weight like 5. Dijkstra would naturally prefer staying on the same line with weight 1 edges."

---

### Question 3.8 — MemoryCache Lifetime

**Examiner:** The graph is cached for 30 minutes. What happens if station data changes in the database during that time?

**Expected Answer:**
"The cached graph becomes stale. If a new station is added or a connection is modified, the in-memory cache still has the old graph. The system would continue to calculate routes using outdated data for up to 30 minutes. This is acceptable because metro station data changes very infrequently. If real-time updates were needed, we could reduce the cache duration, implement cache invalidation, or use SQL dependency tracking."

**Follow-up:**
- Q: Is IMemoryCache shared across all users?
- A: "Yes. It's registered as a Singleton by `AddMemoryCache()`. All users share the same cached graph. This is correct — the graph is the same for everyone."
- Q: What if the application restarts?
- A: "The cache is lost on restart. The first request after startup will rebuild the graph from the database, which is why we have the cache-miss logic."

---

### Question 3.9 — GraphBuilder Edge Deduplication

**Examiner:** How does the GraphBuilder prevent duplicate edges?

**Expected Answer:**
"It uses a `HashSet<string>` called `edgeSet`. Before adding an edge from A to B, it checks if the key `"A-B"` exists in the set. If it does, the edge is skipped. If not, the edge is added and the key is inserted into the set. This ensures that even if the database has duplicate connections (which it shouldn't, but the deduplication is defensive), the graph has only one edge per direction per connection."

**Follow-up:**
- Q: Why use a string key instead of a tuple?
- A: "A string like `"19-46"` is simple and works well as a HashSet key. A `ValueTuple<int,int>` would work too but string concatenation is straightforward."
- Q: Does this prevent both directions of the same connection?
- A: "No. `"19-46"` and `"46-19"` are different strings. Both directions are allowed. The deduplication prevents adding `"19-46"` twice."

---

### Question 3.10 — Seed Transaction Safety

**Examiner:** What happens if the seeding process fails halfway through?

**Expected Answer:**
"The seeding has four separate steps: lines, stations, connections, and pricing rules. Each step is wrapped in `TrySeedStepAsync` which catches exceptions and logs them. If step 1 (lines) succeeds but step 2 (stations) fails, the lines are already committed to the database because `SaveChangesAsync` is called after each step. This means the database could be in an inconsistent state — lines exist but no stations. The `TrySeedStepAsync` logs the error but doesn't roll back previous steps."

**Follow-up:**
- Q: How could you make it transactional?
- A: "Use `context.Database.BeginTransaction()` before all steps and `Commit()` after all succeed, or `Rollback()` if any step fails. This ensures all-or-nothing seeding."
- Q: Why wasn't a transaction used?
- A: "Likely for simplicity. Since seeding happens at startup and the JSON data is static, failure is rare. But for production robustness, a transaction would be better."

---

### Question 3.11 — Station Entity Encapsulation

**Examiner:** The `Line` navigation property on `Station` has a private set. How does EF Core populate it?

**Expected Answer:**
"EF Core uses its own internal mechanisms to set navigation properties even with private setters. It can use the constructor, field-based configuration, or reflection to assign values. When you use `.Include(s => s.Line)`, EF Core executes a JOIN query and materializes the results. It can bypass the private setter because it owns the entity's lifecycle. The private setter just prevents application code from reassigning the navigation property after construction."

**Follow-up:**
- Q: Could you prevent EF Core from accessing private setters?
- A: "Yes, by using field-backed properties with `builder.Property(...).UsePropertyAccessMode(PropertyAccessMode.Field)`. But the default behavior allows EF to use properties with any access modifier."
- Q: Is this good practice?
- A: "It's a common pattern in Domain-Driven Design. It ensures the entity controls its own state, while EF Core's infrastructure can still materialize objects from the database."

---

### Question 3.12 — JSON PropertyName in StationConnection

**Examiner:** StationConnection's `FromStationId` has `[JsonPropertyName("fromStationId")]` but `ToStationId` doesn't. Why?

**Expected Answer:**
"This appears to be an inconsistency. The `[JsonPropertyName("fromStationId")]` attribute on `FromStationId` specifies the JSON key name when serializing/deserializing. However, the `ToStationId` property doesn't have this attribute. Looking at the seed data, the ConnectionSeedModel has `[JsonPropertyName("id")]`, `[JsonPropertyName("fromStationId")]`, `[JsonPropertyName("toStationId")]` on all three properties. The StationConnection entity seems to have the attribute on only one property by mistake — it should either have it on all or none."

**Follow-up:**
- Q: Does this cause any actual bug?
- A: "No, because the seed data uses ConnectionSeedModel, not StationConnection directly, for deserialization. StationConnection is only used by EF Core, not by JSON serialization. So the attribute is unnecessary on StationConnection."
- Q: Why does it exist then?
- A: "Probably copied from the seed model or leftover from an earlier design. It's harmless but shows inconsistent attention to detail."

---

### Question 3.13 — Seed Validation Logic

**Examiner:** In SeedStationsAsync, you check `station.LineId > 0 && existingLineIds.Contains(station.LineId)`. Why both checks?

**Expected Answer:**
"The `station.LineId > 0` check filters out invalid IDs (0 or negative). The `existingLineIds.Contains(station.LineId)` check ensures the referenced line actually exists in the database before inserting the station. This is a referential integrity guard — if the JSON references a non-existent line, that station is skipped instead of causing a foreign key violation."

**Follow-up:**
- Q: Why not just let the database FK constraint catch it?
- A: "The FK constraint would throw an exception, which would be caught by TrySeedStepAsync and logged. But pre-checking gives a cleaner outcome — the valid stations are still seeded, and only the invalid ones are skipped."
- Q: Could a station be seeded with a LineId that doesn't exist?
- A: "No, because the database has a foreign key constraint with `DeleteBehavior.Restrict`. It wouldn't allow it."

---

### Question 3.14 — `AsNoTracking` in Graph Queries

**Examiner:** Why does `GetAllStationsAsync` use `AsNoTracking()` but `GetAllAsync` doesn't?

**Expected Answer:**
"`GetAllAsync` returns full `Station` entities with their `Line` navigation properties loaded via Include. These might be used for display or further operations that require tracking. `GetAllStationsAsync` is specifically for graph building — it projects to `StationGraphNode` which is a read-only DTO. Since graph nodes are never updated or saved, tracking is wasteful. AsNoTracking tells EF Core not to monitor these objects, reducing memory overhead and speeding up the query."

**Follow-up:**
- Q: How much faster is AsNoTracking?
- A: "Typically 10-20% faster for read-only queries. The change tracker doesn't need to store snapshots of the returned objects. For 89 stations, the difference is small but it's good practice."
- Q: What if you later wanted to update a graph node?
- A: "You would need to query it again without AsNoTracking, or explicitly attach it to the context and mark it as modified."

---

### Question 3.15 — Overlapping Controller Responsibilities

**Examiner:** Both controllers can calculate routes. If a user visits the home page, they get one experience. If they visit `/Routes`, they get another. Why the inconsistency?

**Expected Answer:**
"This is a design flaw. Having two implementations for the same feature creates confusion and maintenance burden. The RoutesController is the newer, better implementation with proper ViewModels, anti-forgery protection, and service orchestration. The HomeController appears to be an earlier prototype. Ideally, the Home page should either redirect to Routes/Index or the HomeController should be refactored to use the same ViewModel and MetroService pattern as RoutesController."

**Follow-up:**
- Q: Which implementation should a new developer extend?
- A: "RoutesController. It follows better architecture patterns. The HomeController should be considered legacy."
- Q: How would you consolidate them?
- A: "Redirect `/` to `/Routes/Index`, or refactor HomeController to use RouteSearchViewModel and MetroService, removing the duplicate logic."

---

### Question 3.16 — Nullable Reference Types

**Examiner:** The project enables nullable reference types. How does this affect the code?

**Expected Answer:**
"The `<Nullable>enable</Nullable>` setting in the .csproj means reference types like `string` are treated as non-nullable by default. Properties like `Name { get; private set; }` in entities would generate warnings if not initialized. The entities don't have `= string.Empty` defaults, which means the compiler generates warnings. In the ViewModels, `ErrorMessage` is `string?` (nullable) while `Name` in StationOptionViewModel is `string` (non-nullable with `= string.Empty`)."

**Follow-up:**
- Q: Why does `Station.Name` not have `= string.Empty`?
- A: "Because the constructor always sets it: `Station(int id, string name, ...)`. The compiler recognizes that the constructor initializes all non-nullable properties before the object is used."
- Q: What if you called the private parameterless constructor?
- A: "The `Name` property would be null at runtime, which violates the nullable contract. That's why the default constructor is private — to prevent external code from creating objects in an invalid state."

---

### Question 3.17 — HomeController Route Station Resolution

**Examiner:** In HomeController's POST, you resolve station names. But you do it client-side in memory. Explain.

**Expected Answer:**
"The code does: `pathIds.Select(id => stations.FirstOrDefault(s => s.Id == id)).Where(s => s != null).ToList()`. This is an in-memory LINQ-to-Objects operation because `stations` was already materialized via `await _stationRepository.GetAllAsync()`. For each station ID in the path, it does a linear search through the stations list. For 89 stations and a path of 20 stations, this is 20 linear searches — efficient enough, but the RoutesController's approach of resolving through MetroService is cleaner."

**Follow-up:**
- Q: What is the time complexity of this?
- A: "O(S × P) where S is total stations (89) and P is path length (about 20). So about 1780 comparisons. Negligible."
- Q: How could you optimize this?
- A: "Convert stations to a `Dictionary<int, Station>` using `.ToDictionary(s => s.Id)` for O(1) lookups."

---

### Question 3.18 — Transfer Detection in View (Razor)

**Examiner:** The Home/Index.cshtml has transfer detection logic in the view. Is this appropriate?

**Expected Answer:**
"No, it violates the separation of concerns. The view's responsibility is rendering HTML, not implementing business logic. The transfer detection (`routeStations[i].LineId != routeStations[i-1].LineId`) and line grouping logic should be in a service or at least in the controller. The HomeController calculates the station count and price but leaves transfer detection to the view. RoutesController handles this properly by using `TransferDetectionService` in the service layer."

**Follow-up:**
- Q: Why might the original developer have put it in the view?
- A: "Probably for speed during prototyping — putting logic in the view is faster initially but harder to maintain long-term."
- Q: What's the risk of logic in the view?
- A: "It can't be unit tested. It's harder to read. If the same logic is needed elsewhere, it has to be duplicated."

---

### Question 3.19 — Composite Index Benefits

**Examiner:** The composite index on `(LineId, Order)` — what queries does it benefit?

**Expected Answer:**
"It benefits the `GetByLineIdAsync` query: `_context.Stations.Where(s => s.LineId == lineId).OrderBy(s => s.Order).ToListAsync()`. The index covers both the WHERE clause (LineId filter) and the ORDER BY clause (Order sorting) in a single index. SQL Server can seek to the first station of that line and scan sequentially through the ordered stations without a separate sort operation."

**Follow-up:**
- Q: Would the index still be used if you queried by Order alone?
- A: "No, because Order is the second column in the composite index. A query filtering only by Order would not benefit from this index. You'd need a separate index on Order."
- Q: Why is this important for the metro system?
- A: "Displaying stations in order on a metro line is a core feature. Without the index, SQL Server would need to filter by LineId and then sort all stations by Order, which is slower for large datasets."

---

### Question 3.20 — Seed Connection Referential Integrity

**Examiner:** The connections seed validates `existingStationIds.Contains(connection.FromStationId)`. Why is this needed?

**Expected Answer:**
"This validates that both endpoints of a connection exist as stations before inserting the connection. If a connection references station ID 999 which doesn't exist, the foreign key constraint would fail. By pre-checking, we gracefully skip invalid connections while still seeding valid ones. The same validation is applied to both FromStationId and ToStationId."

**Follow-up:**
- Q: What order must the seed steps run?
- A: "Lines first, then Stations (since stations reference lines), then Connections (since connections reference stations), then PricingRules last (independent). The seed method executes them in this order."
- Q: What if the order was wrong?
- A: "If connections were seeded before stations, the FK check would fail and all connections would be skipped."

---

### Question 3.21 — Model Binding Safety

**Examiner:** Could a malicious user send a POST request with manipulated station IDs that don't exist?

**Expected Answer:**
"Yes, absolutely. Model binding simply maps form values to the ViewModel properties. A user can use browser developer tools to change the `<option>` values, or use tools like Postman to send arbitrary IDs. The MetroService validates that the stations exist — `GetByIdAsync` returns null for non-existent IDs, which triggers `StationNotFoundException`. So the system is protected against invalid station IDs through service-layer validation."

**Follow-up:**
- Q: Is there any risk of SQL injection through the ID parameter?
- A: "No. The IDs are integers. EF Core uses parameterized queries, so `where s.Id == id` becomes `WHERE s.Id = @p0` — safe from SQL injection."
- Q: What if someone sends a non-integer value?
- A: "Model binding would fail because the ViewModel expects `int?`. The controller would receive a null value, which is handled by the null check."

---

### Question 3.22 — Multiple Empty Migrations

**Examiner:** Why are there empty migrations in the project?

**Expected Answer:**
"The migration `20260410190322_SeedData` and `20260429102840_InitialCreate` have empty `Up()` and `Down()` methods. They don't create or modify any database objects. This likely happened because someone ran `Add-Migration` without any model changes. They might have intended to add seed logic inside the migration but then chose the runtime seeding approach instead. These empty migrations don't cause harm but clutter the Migrations folder."

**Follow-up:**
- Q: Could you remove them safely?
- A: "Yes, delete the empty migration files and the corresponding Designer.cs files. Then run `dotnet ef migrations remove` to clean up the snapshot if needed."
- Q: How does the real migration `InitialCreation` differ?
- A: "The `InitialCreation` migration (2026-03-03) contains the actual table creation code — CREATE TABLE for all four tables with columns, constraints, foreign keys, and indexes."

---

### Question 3.23 — TravelTimeService Constraint

**Examiner:** `TravelTimeService` throws for negative station count. Could `stationCount` ever be negative?

**Expected Answer:**
"Technically no, because `stationCount` comes from `stationsOnPath.Count` after the path is found by Dijkstra. If a path is found, it has at least one station (the start). Dijkstra returns `List<int>` which has at least the start and end stations, so Count is at least 2. The guard clause is defensive programming — protecting against edge cases that might never happen but could cause confusing errors if they did."

**Follow-up:**
- Q: Is this good practice or over-engineering?
- A: "It's good defensive practice. If someone later modifies MetroService to call CalculateTravelTime with different data, the guard clause prevents silent incorrect results."
- Q: But doesn't Dijkstra guarantee a path of length ≥ 1?
- A: "If startId == endId, Dijkstra returns `new List<int> { startId }` (count = 1). The MetroService's validation catches this case before calling TravelTimeService though."

---

### Question 3.24 — Price Calculation Edge Cases

**Examiner:** What edge cases could break the price calculation?

**Expected Answer:**
"Several. If `pricingRules` is empty, `FirstOrDefault` returns null and `?.Price ?? 0` returns 0 — a free trip. If a station count is exactly between ranges — it can't be, since they're contiguous (1-9, 10-16, 17-999). If station count is 0, no rule matches. The PricingService throws `ArgumentException` for stationCount ≤ 0. If station count is 1000, no rule matches because the max is 999, so it throws `InvalidOperationException`."

**Follow-up:**
- Q: Should the maximum be int.MaxValue instead of 999?
- A: "Yes, that would be more robust. 999 is an arbitrary limit. Using `int.MaxValue` would cover any future expansion."
- Q: How are the rules ordered?
- A: "The repository queries `.OrderBy(r => r.MinStations)`, ensuring rules are checked from the lowest station count range upward."

---

### Question 3.25 — JSON Deserialization Case

**Examiner:** The SeedData JSON files use PascalCase property names like "Id", "Name". How does deserialization work?

**Expected Answer:**
"The `MetroDataSeeder` configures `JsonSerializerOptions` with `PropertyNameCaseInsensitive = true`. This means the JSON property names can be PascalCase (as in our files) and the deserializer will match them case-insensitively to the C# model properties. Without this option, the default System.Text.Json behavior is case-sensitive, requiring exact name matching or `[JsonPropertyName]` attributes."

**Follow-up:**
- Q: Which seed model uses `[JsonPropertyName]`?
- A: "`ConnectionSeedModel` uses `[JsonPropertyName("id")]`, `[JsonPropertyName("fromStationId")]`, `[JsonPropertyName("toStationId")]` with camelCase. This is because the connections.json uses camelCase property names."
- Q: Why is there an inconsistency in casing?
- A: "The stations.json, lines.json, and pricingRules.json use PascalCase. The connections.json uses camelCase. This is inconsistent but handled by the case-insensitive settings and the explicit attributes."

---

### Question 3.26 — Leaflet Map Type

**Examiner:** You use a map in your application. What tile source does Leaflet use?

**Expected Answer:**
"Leaflet is configured with OpenStreetMap tiles via the URL `https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png`. The `{s}` parameter randomly selects a subdomain for load balancing. `{z}` is the zoom level, and `{x}/{y}` are tile coordinates. The initial view is centered on Cairo at coordinates 30.0444, 31.2357 (Sadat station) at zoom level 11."

**Follow-up:**
- Q: Does this work offline?
- A: "No. OpenStreetMap tiles require internet access. If offline, the map area would show a gray background."
- Q: Why not use a different map provider?
- A: "OpenStreetMap is free and open-source, no API key required. Google Maps would require billing setup."

---

### Question 3.27 — `FirstOrDefault` vs `First` Usage

**Examiner:** The project uses `FirstOrDefault` consistently. When would you use `First` instead?

**Expected Answer:**
"`First` throws `InvalidOperationException` if no matching element is found. `FirstOrDefault` returns null (or default). In this project, `FirstOrDefault` is used because we expect a result might not exist — for example, when looking up a pricing rule. We then null-check and handle the missing case explicitly with `?.Price ?? 0`. If we used `First` when no rule matches, the exception would propagate up, which is less controlled."

**Follow-up:**
- Q: Could any `FirstOrDefault` be safely replaced with `SingleOrDefault`?
- A: "In `GetByIdAsync`, yes — station IDs are unique. `SingleOrDefault` would verify uniqueness. But `FirstOrDefault` is slightly more efficient because it stops at the first match without scanning for duplicates."
- Q: Why not use `FindAsync(id)` which is optimized for primary key lookups?
- A: "`FindAsync` first checks the local cache (tracked entities). It doesn't support `.Include()`. To also load the Line navigation property, we need the query approach with Include and FirstOrDefault."

---

### Question 3.28 — Route Calculation Error Handling

**Examiner:** In RoutesController, you catch `Exception ex`. Is this too broad?

**Expected Answer:**
"Yes, catching `Exception` is broad. It catches everything — StationNotFoundException, InvalidRouteException, database connection failures, null reference exceptions, everything. A better approach would be to catch specific exception types separately. For example, catch `StationNotFoundException` to show "Station not found", catch `InvalidRouteException` to show "No route available", and let unexpected exceptions propagate to the error handler. Currently, a bug like a NullReferenceException would be silently caught and shown as "Could not calculate route" which hides the real problem."

**Follow-up:**
- Q: How would you improve it?
- A: "Add multiple catch blocks: `catch (StationNotFoundException ex) { ... }`, `catch (InvalidRouteException ex) { ... }`, `catch (Exception ex) { ... }` for truly unexpected errors. Log the unexpected ones."
- Q: What happens currently with a database timeout?
- A: "It's caught by the generic catch block, and the user sees 'Could not calculate route: timeout expired'. The message is user-friendly but the error type is not distinguished."

---

### Question 3.29 — RouteService Namespace Issue

**Examiner:** `RouteService` has namespace `Metro.Data.Services` but it's in the `Metro.Core` project. Can you explain?

**Expected Answer:**
"This is a namespace inconsistency. The file `RouteService.cs` is physically located in `Metro.Core/Services/` but its declared namespace is `Metro.Data.Services`. The `GraphBuilder.cs` has the same issue. All other services in the Core folder use `Metro.Core.Services`. This would not cause a compile error — namespaces are independent of folder structure — but it's confusing and violates the project's own conventions. It makes it look like these services belong to the Data layer when they actually contain business logic."

**Follow-up:**
- Q: Does this affect how the services are registered in DI?
- A: "No. DI registration uses `services.AddScoped<IRouteService, RouteService>()`, which works regardless of the namespace. The `using Metro.Data.Services` import in Program.cs resolves correctly."
- Q: Why does RouteService depend on IGraphBuilder (from Metro.Core) but live in Metro.Data.Services namespace?
- A: "This appears to be a copy-paste error or an incomplete refactoring. The namespace should be `Metro.Core.Services`."

---

### Question 3.30 — LineRepository Include

**Examiner:** `LineRepository.GetByIdAsync` uses `.Include(l => l.Stations)`. Is this necessary?

**Expected Answer:**
"It loads all stations belonging to that line in the same query via a JOIN. Whether it's necessary depends on usage. Currently, `LineRepository` is not injected anywhere, so the query never runs. If it were used to display a line's stations, then Include would be essential — without it, `line.Stations` would be null or trigger a separate query. The Include ensures all related stations are loaded eagerly in a single database round trip."

**Follow-up:**
- Q: What if only the line name is needed without stations?
- A: "Then the Include is wasteful. It loads potentially 35 station records when only the line name is needed. The Include should be used selectively based on the use case."
- Q: Could this cause performance issues?
- A: "For 35 stations, no. For a line with thousands of stations, yes. The N+1 problem is avoided by using Include, but all station data is loaded into memory even if not needed."

---

## LEVEL 4 — EXAMINER CHALLENGE (20 Questions)

---

### Question 4.1 — Database Design Justification

**Examiner:** Why did you design `StationConnection` as a separate table instead of using a many-to-many relationship between stations?

**Expected Answer:**
"Designing StationConnection as a separate entity with its own Id provides several advantages. First, it allows us to store additional data on each connection in the future — like travel time, distance, or whether the connection is wheelchair accessible. Second, it separates the graph edge from the station entity, following the single responsibility principle. A many-to-many via convention would use a join table automatically, but we couldn't add our own properties to it. The current design also allows us to easily query connections by station ID using the FromStationId and ToStationId indexes."

**Follow-up:**
- Q: Could you have used a join table with `ManyToMany` convention?
- A: "In EF Core 5+, we could use `modelBuilder.Entity<Station>().HasMany(s => s.Connections).WithMany()`. But that would create an implicit join table without our own Id or extra properties. The explicit entity is more flexible."
- Q: Why does each connection appear twice (bidirectional) in the data?
- A: "Because the GraphBuilder expects directed edges. The JSON stores both A→B and B→A. The HashSet in GraphBuilder prevents duplicates. This design makes the graph structure explicit and allows one-directional connections in the future if needed."

---

### Question 4.2 — Graph Storage Alternative

**Examiner:** Your graph is stored as `Dictionary<int, List<Neighbor>>` in memory. What alternative data structures could you have used?

**Expected Answer:**
"Several alternatives exist. An adjacency matrix `int[,]` would give O(1) edge lookup but O(V²) memory — for 89 stations, a 89×89 matrix uses about 31,000 entries, acceptable. A list of edges with a lookup index could work but would be slower for Dijkstra. A `Dictionary<int, HashSet<int>>` would be similar but without edge weights. The adjacency list (our current approach) is the most common choice for Dijkstra because it provides O(V+E) memory, fast neighbor iteration, and supports weights naturally. For a sparse graph like a metro system with about 2 connections per station on average, adjacency lists are optimal."

**Follow-up:**
- Q: How does your implementation handle the graph in terms of memory?
- A: "We store about 89 entries in the dictionary, each with a list of 2-5 neighbors. Each Neighbor has a StationId and Weight (int each). Total memory is roughly 89 × avg_neighbors × 8 bytes ≈ a few kilobytes — negligible."
- Q: What if Cairo Metro expanded to 500 stations?
- A: "Still fine. 500 stations with ~2 connections each = ~1000 edges. The adjacency list would grow linearly. Dijkstra would still run in milliseconds."

---

### Question 4.3 — Alternative Algorithm Discussion

**Examiner:** Could you have used A* instead of Dijkstra? What are the tradeoffs?

**Expected Answer:**
"Yes, A* could be used. A* uses a heuristic (estimated remaining distance) to guide the search toward the destination, potentially exploring fewer nodes than Dijkstra. Since our stations have coordinates (latitude/longitude), we could use the Haversine distance as the heuristic. A* is optimal if the heuristic is admissible (never overestimates). The tradeoff: A* might be faster for long routes but requires computing the heuristic for each node. For 89 stations, Dijkstra is already fast enough, and Dijkstra guarantees finding the shortest path without needing a heuristic function."

**Follow-up:**
- Q: Would A* be better for a larger system like the London Underground?
- A: "Yes, for a system with hundreds of stations, A* with a geographic heuristic would explore significantly fewer nodes. But the heuristic must be admissible to guarantee optimality."
- Q: Could you implement A* easily with the current code structure?
- A: "Yes. The heuristic function would calculate the Haversine distance from each station to the destination. The PriorityQueue priority would be `distanceFromStart + heuristicToEnd`. The rest of the algorithm stays the same."

---

### Question 4.4 — Static File Organization

**Examiner:** Your project references Bootstrap and jQuery from `wwwroot/lib`. But other libraries come from CDN. Why the mixed approach?

**Expected Answer:**
"Bootstrap and jQuery are included as lib files, likely because they were part of the default ASP.NET Core MVC template. Leaflet and Tom Select are loaded from CDN, probably because they were added later and the developer chose CDN for simplicity. The mixed approach has tradeoffs — local files work offline but increase the project size; CDN files reduce server load but require internet access. A production system should standardize on one approach."

**Follow-up:**
- Q: Which approach is better for a university project?
- A: "CDN is simpler — no need to manage library files. But if the examination might be done offline, local files are safer. For the discussion, mention that both work and explain the tradeoffs."
- Q: How does `asp-append-version` interact with CDN files?
- A: "It only works with local files served through the static file middleware. CDN files have their own caching headers controlled by the CDN provider."

---

### Question 4.5 — Overposting Vulnerability

**Examiner:** Could a malicious user submit extra form fields to manipulate the application?

**Expected Answer:**
"With RoutesController — no. The action parameter is `RouteSearchViewModel model`, which has only `FromStationId` and `ToStationId` as form inputs. Even if extra fields are sent, model binding only populates the ViewModel's properties. This is implicit overposting protection. With HomeController — also no, because it uses primitive `int` parameters. Extra fields are ignored by model binding. However, if we used entity objects directly (e.g., `Station station`), a user could potentially set properties like `Id` or `Order` that they shouldn't. Using ViewModels or primitives prevents this."

**Follow-up:**
- Q: What if someone sends `FromStationId` with a value that's not an integer?
- A: "Model binding would fail to parse it. For the ViewModel with `int?`, the property would remain null, and the validation check would catch it. For HomeController's `int` parameter, model binding would trigger a model error, but since `ModelState.IsValid` is never checked, the method would receive 0 (default)."
- Q: Is 0 a valid station ID?
- A: "No. Stations have IDs starting from 1. A value of 0 would likely cause `StationNotFoundException` when the service tries to resolve it."

---

### Question 4.6 — Database Migrations Strategy

**Examiner:** Your seed data is loaded at runtime. Why not seed within the migration itself?

**Expected Answer:**
"Using `migrationBuilder.Sql("INSERT INTO ...")` inside the `Up()` method is an alternative approach. However, our approach of runtime seeding from JSON files has advantages. First, the JSON files are version-controlled separately from migrations and can be updated without generating new migrations. Second, the seed logic includes duplicate detection — checking existing IDs — which is harder to do in raw SQL within a migration. Third, the JSON data is easy for non-developers to read and edit."

**Follow-up:**
- Q: What if the JSON files get out of sync with the database schema?
- A: "That's a risk. If we add a new column to the Station entity but don't update stations.json, the seed data would be incomplete but wouldn't crash because the model handles default values. A migration-based seed would guarantee schema sync."
- Q: Which approach is more common in production?
- A: "Migration-based seeding is more common because it guarantees seed data is applied exactly when the schema changes. Runtime seeding is more flexible but requires careful management."

---

### Question 4.7 — No Testing Strategy

**Examiner:** I see test packages (xUnit, Moq) referenced but no test files. Why?

**Expected Answer:**
"The .csproj includes `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`, and `Moq.EntityFrameworkCore` packages, which suggests unit tests were intended. However, no test project or test files are present in the solution. This could mean tests were planned but not implemented, or they were in a separate project that's not included. For the discussion, I would acknowledge this as an area for improvement — testing the services (especially RouteService's Dijkstra) and controllers would increase confidence in the correctness."

**Follow-up:**
- Q: What would you test first?
- A: "The RouteService.GetShortestPathAsync — test known routes between stations and verify the path is correct. Then MetroService.GetRouteAsync to verify the full orchestration. Then the controllers with mocked services."
- Q: How would you mock IStationRepository?
- A: "Using Moq: `var mockRepo = new Mock<IStationRepository>(); mockRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(new Station(...));`"

---

### Question 4.8 — Station Lookup Inefficiency

**Examiner:** In MetroService, you call `GetByIdAsync` for every station on the path. That's N+1 database queries. Is this a problem?

**Expected Answer:**
"Yes, this is the N+1 query problem. For a path with 20 stations, we execute 20 separate database queries plus the pricing rules query. This is inefficient. An optimization would be to create a repository method `GetByIdsAsync(List<int> ids)` that queries all stations with a single `WHERE Id IN (...)`. Alternatively, we could pre-load all stations with `GetAllAsync()` once and do the lookups in memory using a Dictionary<int, Station>."

**Follow-up:**
- Q: Why was this approach taken?
- A: "Probably for simplicity during development. For 89 stations and typical path lengths of 20-30 stations, the performance impact is minimal. But it's definitely an area for optimization."
- Q: How would you implement the optimization?
- A: "In StationRepository: `return await _context.Stations.Where(s => ids.Contains(s.Id)).Include(s => s.Line).ToListAsync();` which generates `WHERE Id IN (@p0, @p1, @p2, ...)`. Then MetroService would build a dictionary from the result."

---

### Question 4.9 — Dependency Injection Depth

**Examiner:** Your dependency chain is 6 levels deep (Controller → MetroService → RouteService → GraphBuilder → Repository → DbContext). Is this too much?

**Expected Answer:**
"Six levels is acceptable but approaching the edge of complexity. The chain reflects proper separation of concerns — each layer has a specific responsibility. MetroService orchestrates, RouteService implements the algorithm, GraphBuilder manages the data structure, Repositories handle data access. However, the deep chain does make it harder to understand the full flow and slower to instantiate. A potential improvement would be to have GraphBuilder inject the repositories directly into RouteService, removing one level. But the current design is clean and testable."

**Follow-up:**
- Q: What is the performance cost of deep DI resolution?
- A: "The DI container resolves the entire chain at once when RouteServiceController is first requested. For a web application, this is a one-time cost per request measured in microseconds. The benefits of modularity far outweigh this cost."
- Q: Could you use the Service Locator pattern instead?
- A: "That's an anti-pattern. It hides dependencies and makes testing harder. Constructor injection (what we use) makes dependencies explicit."

---

### Question 4.10 — No Index on Stations.Name

**Examiner:** The Stations table has no index on the `Name` column, yet you order stations by Name. Why?

**Expected Answer:**
"That's a valid observation. The `OrderBy(s => s.Name)` in `LoadStationsAsync` is an in-memory operation after data is already loaded from the database. The SQL query doesn't have an ORDER BY for name — it orders after materialization. If we wanted SQL-level ordering, we'd need to add it before `.ToListAsync()`. But since the project currently loads all stations and then sorts alphabetically, an index on Name would not help because the data is already in memory. If we wanted to optimize, we could add the ordering to the LINQ query so SQL Server handles it, and then an index on Name would improve performance."

**Follow-up:**
- Q: Does `.OrderBy(s => s.Name)` in LoadStationsAsync happen in SQL or memory?
- A: "It happens in memory. The `stations` variable is already a `List<Station>` from the materialized `GetAllAsync()` result. LINQ-to-Objects then sorts in memory. To make it SQL-level, we'd need to move OrderBy before .ToListAsync() in the repository method."
- Q: For 89 stations, does this matter?
- A: "Not really. 89 items sorted in memory takes microseconds. But it's good to know the difference between client-side and server-side evaluation."

---

### Question 4.11 — Configuration vs Data Annotations

**Examiner:** You use Fluent API configurations instead of data annotations on entities. Why?

**Expected Answer:**
"Fluent API provides more control than data annotations. Some configurations can only be done via Fluent API — like composite indexes (`HasIndex(s => new { s.LineId, s.Order })`), delete behavior (`OnDelete(DeleteBehavior.Restrict)`), and multiple foreign key relationships with separate navigation properties. Data annotations like `[Required]`, `[MaxLength]` are simpler but mix persistence concerns into the domain entities. With Fluent API, entity classes remain clean POCOs (Plain Old CLR Objects)."

**Follow-up:**
- Q: How did you organize the Fluent API code?
- A: "Each entity has its own configuration class implementing `IEntityTypeConfiguration<T>`. They're applied automatically in OnModelCreating via `ApplyConfigurationsFromAssembly()`. This is the recommended approach for clean separation."
- Q: Why use `ApplyConfigurationsFromAssembly` instead of adding each individually?
- A: "It automatically discovers all configuration classes. If we add a new entity with its configuration, it's picked up without modifying the DbContext. This follows the Open/Closed Principle."

---

### Question 4.12 — JSON Seed File Inconsistency

**Examiner:** I notice `connections.json` uses camelCase property names while `stations.json` uses PascalCase. Why?

**Expected Answer:**
"This is an inconsistency in the data files. The `connections.json` has `"fromStationId"` and `"toStationId"` (camelCase) while `stations.json` has `"Id"`, `"Name"` (PascalCase). The code handles both because `ConnectionSeedModel` has explicit `[JsonPropertyName]` attributes matching the camelCase names, while `StationSeedModel` relies on the case-insensitive deserializer setting. The inconsistency likely comes from different people creating the files or copying from different sources."

**Follow-up:**
- Q: Which approach is more robust?
- A: "Using explicit `[JsonPropertyName]` attributes is more robust because it works regardless of the deserializer settings. Relying on `PropertyNameCaseInsensitive` works but is an implicit convention."
- Q: Should you standardize?
- A: "Yes. JSON convention is camelCase. All files should use camelCase, and all seed models should use `[JsonPropertyName]` attributes consistently."

---

### Question 4.13 — Leaflet Map Z-Index Bug

**Examiner:** The Home view's Leaflet map has `z-index: -10`. What problem does this solve?

**Expected Answer:**
"The `z-index: -10` on the map container is used to fix a stacking context issue. The map tiles and controls might otherwise appear above other page elements like the navigation overlay. Setting a low z-index pushes the map behind other content. However, this is a fragile fix. A better approach would be to properly manage the z-index stacking context by setting the map container's z-index appropriately relative to its siblings."

**Follow-up:**
- Q: What could go wrong with `z-index: -10`?
- A: "The map markers, popups, and controls have their own z-index values. If the container has z-index -10, some map UI elements might not display correctly or be clickable."
- Q: How would you fix this properly?
- A: "Remove the negative z-index. Instead, set the map container to `position: relative; z-index: 1;` and ensure surrounding elements have appropriate z-index values. Or use CSS isolation with `isolation: isolate;`."

---

### Question 4.14 — Missing GPS Error State

**Examiner:** The "Use My Location" button uses a loading overlay. What happens if the user denies location permission?

**Expected Answer:**
"The geolocation API has error handling. If the user denies permission, the error callback fires with `error.code == error.PERMISSION_DENIED`. The error message 'Location permission was denied.' is shown in the map error box. The loading overlay is removed via `resetLocationState()`. The button is re-enabled. However, on the Home view, there's a potential issue — the map overlay has `z-index: 1000` and `display: flex` by default when the `active` class is added, but if there's an error, the overlay is hidden. The user can retry by clicking the button again."

**Follow-up:**
- Q: Are there any other GPS error codes handled?
- A: "Yes. `POSITION_UNAVAILABLE` and `TIMEOUT` are handled with a generic message. The `enableHighAccuracy: true` and 10-second timeout are configured."
- Q: What if JavaScript is disabled entirely?
- A: "The GPS feature is unavailable. But the rest of the application — the route search form and map display — still works. The button becomes a non-functional element."

---

### Question 4.15 — PricingRule.IsMatch as Entity Method

**Examiner:** You put `IsMatch()` logic inside the `PricingRule` entity. Is this the right place for business logic?

**Expected Answer:**
"Yes, it's appropriate. `IsMatch` is a simple query method that operates purely on the entity's own data — it checks if a station count falls within `MinStations` and `MaxStations`. This is an example of a rich domain model where the entity contains behavior, not just data. Making it a method on the entity follows the Tell-Don't-Ask principle. Alternative is to have a PricingRuleService that performs the check, but for such simple logic, keeping it on the entity is clean and testable."

**Follow-up:**
- Q: What if pricing rules become complex (e.g., peak/off-peak, discounts)?
- A: "Then move the logic to a separate `PricingStrategy` service. The entity would still hold the configuration data, but complex business rules would be in the service layer."
- Q: Does having logic in the entity violate any principles?
- A: "No. Domain-Driven Design encourages embedding domain logic in entities. The issue would be if the entity also had persistence concerns mixed in, but ours only has the one query method."

---

### Question 4.16 — URL Manipulation Risk

**Examiner:** What happens if a user visits `/Routes/Index` with a direct GET request after another user's POST?

**Expected Answer:**
"Nothing unusual. The GET action always returns a fresh form with the station list. Session state is not used in this project, so no data leaks between users. Each request is independent. The only issue would be if someone bookmarked a POST URL — which generally doesn't work because POST requests can't be bookmarked in the same way."

**Follow-up:**
- Q: Could a user see another user's route result?
- A: "No. The route result is calculated per-request and stored in the ViewModel returned to that specific user. There's no shared cache of route results. However, the graph cache is shared (memory cache). So if one request triggers graph building, the next request uses the cached graph — but the graph is the same for everyone."
- Q: Is caching a security concern?
- A: "No, because the graph is public data. There are no user-specific results stored in the cache."

---

### Question 4.17 — No Request Logging

**Examiner:** There's no logging of route searches. How would you debug issues in production?

**Expected Answer:**
"That's a valid concern. Currently, there's no structured logging of route searches. If a user reports an issue with a specific route, we can't reproduce it easily. An `ILogger<RoutesController>` could be injected into the controller to log search attempts, validation failures, and exceptions. The `MetroDataSeeder` already uses `ILogger`, so the pattern exists. In production, logs could be sent to a file, database, or monitoring service."

**Follow-up:**
- Q: What specific events would you log?
- A: "When a route is requested (with station IDs), when validation fails, when an exception occurs, and the route result summary (path length, price). But NOT station coordinates or other sensitive data (though there's no sensitive data here)."
- Q: HomeController doesn't have ILogger either. Would you add it?
- A: "Yes, if HomeController is kept, it should also have logging. But ideally, we'd consolidate to RoutesController and add logging there."

---

### Question 4.18 — Database Connection Security

**Examiner:** The connection string uses Windows Authentication (`Trusted_Connection=True`). What are the security implications?

**Expected Answer:**
"Windows Authentication means the application connects to SQL Server using the Windows identity of the application pool or process. In development, this is convenient — it uses the developer's Windows credentials. In production, it means the web server's machine account (e.g., `NETWORK SERVICE`) needs SQL Server login rights. The `TrustServerCertificate=True` means it accepts the server's SSL certificate without validation, which is fine for local development but should use a proper certificate in production."

**Follow-up:**
- Q: What would you change for production?
- A: "Use a specific SQL Server login with limited permissions instead of Windows Auth, store the connection string in User Secrets or Azure Key Vault (not appsettings.json), and set `TrustServerCertificate=False` with a valid certificate."
- Q: Is the connection string checked into source control?
- A: "Yes, it's in appsettings.json which is committed to the git repository. For a university project this is acceptable, but for production, it should be excluded or stored in secure configuration."

---

### Question 4.19 — Future Enhancement Architecture

**Examiner:** If the Ministry of Transport asked you to add real-time train tracking, how would you extend this system?

**Expected Answer:**
"I would add a new entity `TrainSchedule` with properties like `StationId`, `LineId`, `ArrivalTime`, `DepartureTime`, `Direction`. Add a `DbSet<TrainSchedule>` to the DbContext with appropriate configuration. Create a `RealTimeService` that queries schedules for the calculated route. Inject it into MetroService and add schedule information to the RouteResultDto. On the frontend, display the suggested departure time and train intervals. The architecture supports this because MetroService is designed as an extensible orchestrator — I'd just add another service to the chain."

**Follow-up:**
- Q: Would the Dijkstra algorithm change for time-based routing?
- A: "Possibly. If we want the quickest route considering train schedules (not just station count), the edge weights could represent real travel times between stations. We might also use A* for better performance with time-dependent data."
- Q: How would you handle real-time updates?
- A: "Use SignalR for WebSocket-based real-time communication. Push schedule updates and delays to connected clients without requiring page refreshes."

---

### Question 4.20 — Final Critical Question

**Examiner:** Overall, what are the three most critical improvements this project needs?

**Expected Answer:**
"First, **fix the compilation issue** — the RoutesController.cs is excluded by the .csproj configuration, which means the entire route search feature doesn't work. Remove the `<Compile Remove>` directive. Second, **consolidate the two controllers** — having HomeController and RoutesController doing the same thing is confusing and doubles maintenance. Keep RoutesController (it has better architecture) and redirect the home page to it. Third, **add proper validation** — implement `[Required]` data annotations on ViewModels, check `ModelState.IsValid` in controllers, and add `[ValidateAntiForgeryToken]` to HomeController. These three changes would dramatically improve reliability, security, and maintainability."

**Follow-up:**
- Q: What about testing?
- A: "Adding unit tests for the RouteService algorithm and the service layer would be the fourth priority. But the three changes above address critical functional and structural issues first."
- Q: No database concerns?
- A: "The database design is solid — proper foreign keys, appropriate indexes, delete protection. The SQL Server with EF Core setup is appropriate for this scale. I'd only suggest removing the empty migrations to keep the project clean."

---

*End of MOCK ORAL EXAMINATION — Study these questions and answers thoroughly. Good luck!*
