using CommunityToolkit.Mvvm.ComponentModel;

namespace AutoPlot.ViewModels
{
    public partial class SeriesCountDialogViewModel : ObservableObject
    {
        [ObservableProperty]
        private int _seriesCount = 1;
    }
}
