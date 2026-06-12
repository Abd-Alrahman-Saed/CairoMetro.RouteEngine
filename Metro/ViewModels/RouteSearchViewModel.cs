using Metro.Core.DTOs;
using System.ComponentModel.DataAnnotations;

namespace Metro.ViewModels
{
    public class RouteSearchViewModel
    {
        [Required(ErrorMessage = "Please select a departure station.")]
        public int FromStationId { get; set; }

        [Required(ErrorMessage = "Please select a destination station.")]
        public int ToStationId { get; set; }

        public List<StationOptionViewModel> Stations { get; set; } = new();

        // ── Result ────────────────────────────────────────────────────────────
        /// <summary>
        /// Populated with a <see cref="RouteResultDto"/> on a successful POST.
        /// Kept as <c>object</c> per spec so the View can cast as needed.
        /// </summary>
        public object? Result { get; set; }

        public string? ErrorMessage { get; set; }
    }
}
