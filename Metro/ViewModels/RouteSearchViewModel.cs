using Metro.Core.DTOs;
using System.ComponentModel.DataAnnotations;

namespace Metro.ViewModels
{
    public class RouteSearchViewModel
    {
        // ── Form Input ────────────────────────────────────────────────────────
        [Required(ErrorMessage = "Please select a departure station.")]
        public int? FromStationId { get; set; }

        [Required(ErrorMessage = "Please select a destination station.")]
        public int? ToStationId { get; set; }

        // ── Dropdown Data ─────────────────────────────────────────────────────
        /// <summary>
        /// All available stations, populated on every GET and after a failed POST.
        /// </summary>
        public List<StationOptionViewModel> Stations { get; set; } = new();

        // ── Result ────────────────────────────────────────────────────────────
        /// <summary>
        /// Populated with a <see cref="RouteResultDto"/> on a successful POST.
        /// Kept as <c>object</c> per spec so the View can cast as needed.
        /// </summary>
        public object? Result { get; set; }

        // ── Feedback ──────────────────────────────────────────────────────────
        /// <summary>
        /// Validation or service error message shown to the user.
        /// </summary>
        public string? ErrorMessage { get; set; }
    }
}
