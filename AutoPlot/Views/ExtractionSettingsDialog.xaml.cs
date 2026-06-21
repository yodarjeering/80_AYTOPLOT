using AutoPlot.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AutoPlot.Views
{
    public partial class ExtractionSettingsDialog : Window
    {
        public ExtractionSettingsDialog()
        {
            InitializeComponent();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (HasValidationError(this))
            {
                MessageBox.Show(
                    "Please enter valid integer values.",
                    "Invalid Extraction Settings",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (DataContext is ExtractionSettingsDialogViewModel vm &&
                !vm.TryValidate(out string errorMessage))
            {
                MessageBox.Show(
                    errorMessage,
                    "Invalid Extraction Settings",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
        }

        private static bool HasValidationError(DependencyObject parent)
        {
            if (Validation.GetHasError(parent))
                return true;

            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                if (HasValidationError(VisualTreeHelper.GetChild(parent, i)))
                    return true;
            }

            return false;
        }
    }
}
