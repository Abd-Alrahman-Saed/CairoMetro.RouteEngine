using Metro.Core.Interfaces;
using Metro.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace Metro.Controllers
{
    public class HomeController : Controller
    {
        private readonly IStationRepository _stationRepository;
        private readonly IRouteService _routeService;
        private readonly IPricingRuleRepository _pricingRuleRepository;

        public HomeController(
            IStationRepository stationRepository,
            IRouteService routeService,
            IPricingRuleRepository pricingRuleRepository)
        {
            _stationRepository = stationRepository;
            _routeService = routeService;
            _pricingRuleRepository = pricingRuleRepository;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            ViewBag.Stations = await _stationRepository.GetAllAsync();
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Index(int fromStationId, int toStationId)
        {
            var stations = await _stationRepository.GetAllAsync();
            ViewBag.Stations = stations;

            if (fromStationId == toStationId)
            {
                ViewBag.Error = "Please choose two different stations.";
                return View();
            }

            var pathIds = await _routeService.GetShortestPathAsync(fromStationId, toStationId);

            var routeStations = pathIds
                .Select(id => stations.FirstOrDefault(s => s.Id == id))
                .Where(s => s != null)
                .ToList();

            var stationCount = routeStations.Count;

            var pricingRules = await _pricingRuleRepository.GetAllAsync();
            var price = pricingRules
                .FirstOrDefault(rule => rule.IsMatch(stationCount))
                ?.Price ?? 0;

            ViewBag.RouteStations = routeStations;
            ViewBag.StationCount = stationCount;
            ViewBag.Price = price;
            ViewBag.FromStationId = fromStationId;
            ViewBag.ToStationId = toStationId;

            return View();
        }

        //public async Task<IActionResult> Index(int fromStationId, int toStationId)
        //{
        //    var stations = await _stationRepository.GetAllAsync();

        //    var model = new RouteViewModel
        //    {
        //        Stations = stations,
        //        FromStationId = fromStationId,
        //        ToStationId = toStationId
        //    };

        //    if (fromStationId == toStationId)
        //    {
        //        model.ErrorMessage =
        //            "Please choose two different stations.";

        //        return View(model);
        //    }

        //    var pathIds =
        //        await _routeService.GetShortestPathAsync(
        //            fromStationId,
        //            toStationId);

        //    var routeStations = pathIds
        //        .Select(id => stations.FirstOrDefault(s => s.Id == id))
        //        .Where(s => s != null)
        //        .ToList();

        //    var stationCount = routeStations.Count;

        //    var pricingRules =
        //        await _pricingRuleRepository.GetAllAsync();

        //    var price = pricingRules
        //        .FirstOrDefault(rule => rule.IsMatch(stationCount))
        //        ?.Price ?? 0;

        //    model.RouteStations = routeStations!;
        //    model.StationCount = stationCount;
        //    model.Price = price;

        //    return View(model);
        //}

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}