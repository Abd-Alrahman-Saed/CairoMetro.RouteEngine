namespace Metro.ViewModels
{
    public class StationOptionViewModel
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string LineName { get; set; } = string.Empty;

        /// <summary>
        /// Formatted display text shown in dropdown: "Name (LineName)"
        /// </summary>
        public string DisplayName => $"{Name} ({LineName})";
    }
}
