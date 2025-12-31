using System.Windows;

namespace AutoPlot.Views
{
    public partial class AxisCalibrationDialog : Window
    {
        public AxisCalibrationDialog()
        {
            InitializeComponent();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
