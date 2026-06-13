using System.Windows;
using AutoPlot.ViewModels;

namespace AutoPlot.Views
{
    public partial class SeriesCountDialog : Window
    {
        public SeriesCountDialog()
        {
            InitializeComponent();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not SeriesCountDialogViewModel vm)
                return;

            if (vm.SeriesCount < 1)
            {
                MessageBox.Show("系列数は1以上で入力してください。");
                SeriesCountTextBox.Focus();
                SeriesCountTextBox.SelectAll();
                return;
            }

            DialogResult = true;
        }
    }
}
