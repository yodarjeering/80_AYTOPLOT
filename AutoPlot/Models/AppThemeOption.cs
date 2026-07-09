namespace AutoPlot.Models
{
    public class AppThemeOption
    {
        public AppThemeOption(AppTheme value, string displayName)
        {
            Value = value;
            DisplayName = displayName;
        }

        public AppTheme Value { get; }

        public string DisplayName { get; }
    }
}
