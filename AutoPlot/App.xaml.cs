using System.Configuration;
using System.Data;
using System.Windows;
using AutoPlot.Services;

namespace AutoPlot
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var settings = AppSettingsService.Load();
            AppThemeManager.Apply(settings.Theme);
        }
    }

}
