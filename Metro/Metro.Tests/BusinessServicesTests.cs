using Metro.Core.DTOs;
using Metro.Core.Entities;
using Metro.Core.Exceptions;
using Metro.Core.Interfaces;
using Metro.Core.Services;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Metro.Tests;

// ── MetroService ───────────────────────────────────────────────
public class MetroServiceTests
{
    private readonly ITestOutputHelper _out;

    public MetroServiceTests(ITestOutputHelper output) => _out = output;

    // ── helpers ──────────────────────────────────────────────────
    private static Station MakeStation(int id, int lineId) =>
        new(id, $"Station {id}", lineId, 0, 0, id);

    private static List<PricingRule> DefaultRules() => new()
    {
        new PricingRule(1, 1,   9,   8),
        new PricingRule(2, 10, 16,  10),
        new PricingRule(3, 17, 999, 15),
    };

    private MetroService BuildSut(
        Mock<IRouteService>? routeSvc = null,
        Mock<IStationRepository>? stationRepo = null,
        Mock<IPricingRuleRepository>? pricingRepo = null)
    {
        routeSvc ??= new Mock<IRouteService>();
        stationRepo ??= new Mock<IStationRepository>();
        pricingRepo ??= new Mock<IPricingRuleRepository>();

        return new MetroService(
            routeSvc.Object,
            new TravelTimeService(),
            new PricingService(),
            new TransferDetectionService(),
            stationRepo.Object,
            pricingRepo.Object);
    }

    // ═══════════════════════════════════════════════════════════════
    // Happy path — full DTO (same line, no transfer)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetRouteAsync_ValidRoute_ReturnsPopulatedDto()
    {
        // ── Inputs ───────────────────────────────────────────────
        int startId = 1, endId = 3;
        var path = new List<int> { 1, 2, 3 };

        _out.WriteLine("=== GetRouteAsync_ValidRoute_ReturnsPopulatedDto ===");
        _out.WriteLine($"  Input  → startId : {startId}");
        _out.WriteLine($"  Input  → endId   : {endId}");
        _out.WriteLine($"  Input  → path    : [{string.Join(", ", path)}]");
        _out.WriteLine($"  Input  → all stations on line 1 (no transfer)");

        // ── Arrange ───────────────────────────────────────────────
        var routeSvc = new Mock<IRouteService>();
        var stationRepo = new Mock<IStationRepository>();
        var pricingRepo = new Mock<IPricingRuleRepository>();

        stationRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeStation(1, lineId: 1));
        stationRepo.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(MakeStation(2, lineId: 1));
        stationRepo.Setup(r => r.GetByIdAsync(3)).ReturnsAsync(MakeStation(3, lineId: 1));
        routeSvc.Setup(r => r.GetShortestPathAsync(startId, endId)).ReturnsAsync(path);
        pricingRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(DefaultRules());

        var sut = BuildSut(routeSvc, stationRepo, pricingRepo);

        // ── Act ───────────────────────────────────────────────────
        RouteResultDto actual = await sut.GetRouteAsync(startId, endId);

        // ── Expected values ───────────────────────────────────────
        string expectedFrom = "Station 1";
        string expectedTo = "Station 3";
        int expectedCount = 3;
        int expectedXfer = 0;        // same line
        int expectedTime = 6;        // 3 × 2 min
        decimal expectedPrice = 8m;       // rule 1: 1-9 stations → 8 EGP
        var expectedStations = new List<string> { "Station 1", "Station 2", "Station 3" };

        // ── Output ────────────────────────────────────────────────
        _out.WriteLine("");
        _out.WriteLine($"  {"Field",-25} {"Expected",-25} {"Actual",-25}");
        _out.WriteLine($"  {new string('-', 77)}");
        _out.WriteLine($"  {"FromStationName",-25} {expectedFrom,-25} {actual.FromStationName,-25}");
        _out.WriteLine($"  {"ToStationName",-25} {expectedTo,-25} {actual.ToStationName,-25}");
        _out.WriteLine($"  {"StationCount",-25} {expectedCount,-25} {actual.StationCount,-25}");
        _out.WriteLine($"  {"Transfers",-25} {expectedXfer,-25} {actual.Transfers,-25}");
        _out.WriteLine($"  {"EstimatedTimeMinutes",-25} {expectedTime,-25} {actual.EstimatedTimeMinutes,-25}");
        _out.WriteLine($"  {"Price (EGP)",-25} {expectedPrice,-25} {actual.Price,-25}");
        _out.WriteLine($"  {"Stations",-25} [{string.Join(", ", expectedStations)}]  →  [{string.Join(", ", actual.Stations)}]");

        // ── Assert ────────────────────────────────────────────────
        Assert.Equal(expectedFrom, actual.FromStationName);
        Assert.Equal(expectedTo, actual.ToStationName);
        Assert.Equal(expectedCount, actual.StationCount);
        Assert.Equal(expectedXfer, actual.Transfers);
        Assert.Equal(expectedTime, actual.EstimatedTimeMinutes);
        Assert.Equal(expectedPrice, actual.Price);
        Assert.Equal(expectedStations, actual.Stations);
    }

    // ═══════════════════════════════════════════════════════════════
    // Happy path — cross-line transfer
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetRouteAsync_CrossLineRoute_CountsTransfer()
    {
        // ── Inputs ───────────────────────────────────────────────
        int startId = 1, endId = 4;
        var path = new List<int> { 1, 2, 3, 4 };

        _out.WriteLine("=== GetRouteAsync_CrossLineRoute_CountsTransfer ===");
        _out.WriteLine($"  Input  → startId : {startId}");
        _out.WriteLine($"  Input  → endId   : {endId}");
        _out.WriteLine($"  Input  → path    : [{string.Join(", ", path)}]");
        _out.WriteLine($"  Input  → Stations 1-2 on line 1 | Stations 3-4 on line 2 → 1 transfer");

        // ── Arrange ───────────────────────────────────────────────
        var routeSvc = new Mock<IRouteService>();
        var stationRepo = new Mock<IStationRepository>();
        var pricingRepo = new Mock<IPricingRuleRepository>();

        stationRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeStation(1, lineId: 1));
        stationRepo.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(MakeStation(2, lineId: 1));
        stationRepo.Setup(r => r.GetByIdAsync(3)).ReturnsAsync(MakeStation(3, lineId: 2));
        stationRepo.Setup(r => r.GetByIdAsync(4)).ReturnsAsync(MakeStation(4, lineId: 2));
        routeSvc.Setup(r => r.GetShortestPathAsync(startId, endId)).ReturnsAsync(path);
        pricingRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(DefaultRules());

        var sut = BuildSut(routeSvc, stationRepo, pricingRepo);

        // ── Act ───────────────────────────────────────────────────
        RouteResultDto actual = await sut.GetRouteAsync(startId, endId);

        // ── Expected values ───────────────────────────────────────
        int expectedXfer = 1;
        int expectedCount = 4;
        int expectedTime = 8;   // 4 × 2 min

        // ── Output ────────────────────────────────────────────────
        _out.WriteLine("");
        _out.WriteLine($"  {"Field",-25} {"Expected",-15} {"Actual",-15}");
        _out.WriteLine($"  {new string('-', 55)}");
        _out.WriteLine($"  {"Transfers",-25} {expectedXfer,-15} {actual.Transfers,-15}");
        _out.WriteLine($"  {"StationCount",-25} {expectedCount,-15} {actual.StationCount,-15}");
        _out.WriteLine($"  {"EstimatedTimeMinutes",-25} {expectedTime,-15} {actual.EstimatedTimeMinutes,-15}");

        // ── Assert ────────────────────────────────────────────────
        Assert.Equal(expectedXfer, actual.Transfers);
        Assert.Equal(expectedCount, actual.StationCount);
        Assert.Equal(expectedTime, actual.EstimatedTimeMinutes);
    }

    // ═══════════════════════════════════════════════════════════════
    // Validation — start == end
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetRouteAsync_SameStartAndEnd_ThrowsInvalidRouteException()
    {
        int startId = 5, endId = 5;

        _out.WriteLine("=== GetRouteAsync_SameStartAndEnd_ThrowsInvalidRouteException ===");
        _out.WriteLine($"  Input    → startId: {startId}, endId: {endId}");
        _out.WriteLine($"  Expected → throws InvalidRouteException");

        var sut = BuildSut();

        var ex = await Assert.ThrowsAsync<InvalidRouteException>(
            () => sut.GetRouteAsync(startId, endId));

        _out.WriteLine($"  Actual   → {ex.GetType().Name}: \"{ex.Message}\"");
    }

    // ═══════════════════════════════════════════════════════════════
    // Validation — start station not in DB
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetRouteAsync_StartStationNotFound_ThrowsStationNotFoundException()
    {
        int startId = 99, endId = 1;

        _out.WriteLine("=== GetRouteAsync_StartStationNotFound_ThrowsStationNotFoundException ===");
        _out.WriteLine($"  Input    → startId: {startId} (not in DB), endId: {endId}");
        _out.WriteLine($"  Expected → throws StationNotFoundException");

        var stationRepo = new Mock<IStationRepository>();
        stationRepo.Setup(r => r.GetByIdAsync(startId)).ReturnsAsync((Station?)null);

        var sut = BuildSut(stationRepo: stationRepo);

        var ex = await Assert.ThrowsAsync<StationNotFoundException>(
            () => sut.GetRouteAsync(startId, endId));

        _out.WriteLine($"  Actual   → {ex.GetType().Name}: \"{ex.Message}\"");
    }

    // ═══════════════════════════════════════════════════════════════
    // Validation — end station not in DB
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetRouteAsync_EndStationNotFound_ThrowsStationNotFoundException()
    {
        int startId = 1, endId = 99;

        _out.WriteLine("=== GetRouteAsync_EndStationNotFound_ThrowsStationNotFoundException ===");
        _out.WriteLine($"  Input    → startId: {startId} (exists), endId: {endId} (not in DB)");
        _out.WriteLine($"  Expected → throws StationNotFoundException");

        var stationRepo = new Mock<IStationRepository>();
        stationRepo.Setup(r => r.GetByIdAsync(startId)).ReturnsAsync(MakeStation(startId, lineId: 1));
        stationRepo.Setup(r => r.GetByIdAsync(endId)).ReturnsAsync((Station?)null);

        var sut = BuildSut(stationRepo: stationRepo);

        var ex = await Assert.ThrowsAsync<StationNotFoundException>(
            () => sut.GetRouteAsync(startId, endId));

        _out.WriteLine($"  Actual   → {ex.GetType().Name}: \"{ex.Message}\"");
    }

    // ═══════════════════════════════════════════════════════════════
    // Validation — IRouteService returns empty path
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetRouteAsync_NoPathReturned_ThrowsInvalidRouteException()
    {
        int startId = 1, endId = 2;

        _out.WriteLine("=== GetRouteAsync_NoPathReturned_ThrowsInvalidRouteException ===");
        _out.WriteLine($"  Input    → startId: {startId}, endId: {endId}");
        _out.WriteLine($"  Input    → IRouteService returns empty list []");
        _out.WriteLine($"  Expected → throws InvalidRouteException");

        var routeSvc = new Mock<IRouteService>();
        var stationRepo = new Mock<IStationRepository>();

        stationRepo.Setup(r => r.GetByIdAsync(startId)).ReturnsAsync(MakeStation(startId, lineId: 1));
        stationRepo.Setup(r => r.GetByIdAsync(endId)).ReturnsAsync(MakeStation(endId, lineId: 2));
        routeSvc.Setup(r => r.GetShortestPathAsync(startId, endId)).ReturnsAsync(new List<int>());

        var sut = BuildSut(routeSvc, stationRepo);

        var ex = await Assert.ThrowsAsync<InvalidRouteException>(
            () => sut.GetRouteAsync(startId, endId));

        _out.WriteLine($"  Actual   → {ex.GetType().Name}: \"{ex.Message}\"");
    }
}