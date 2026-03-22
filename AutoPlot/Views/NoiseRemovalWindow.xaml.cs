using AutoPlot.ViewModels;
using System.Windows;
using System.Windows.Input;

namespace AutoPlot.Views
{
    public partial class NoiseRemovalWindow : Window
    {
        private bool _isDrawing;
        private Point _prevPoint;

        public NoiseRemovalWindow()
        {
            InitializeComponent();
        }

        private void PreviewImageControl_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is not NoiseRemovalViewModel vm)
                return;

            _isDrawing = true;
            _prevPoint = e.GetPosition(PreviewImageControl);

            vm.BeginStroke();

            PreviewImageControl.CaptureMouse();
        }

        private void PreviewImageControl_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDrawing || DataContext is not NoiseRemovalViewModel vm)
                return;

            Point curr = e.GetPosition(PreviewImageControl);

            vm.DrawMaskLine(
                _prevPoint,
                curr,
                PreviewImageControl.ActualWidth,
                PreviewImageControl.ActualHeight);

            _prevPoint = curr;
        }

        private void PreviewImageControl_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isDrawing = false;
            PreviewImageControl.ReleaseMouseCapture();
        }

        private void OnOkClicked(object sender, RoutedEventArgs e)
        {
            if (DataContext is NoiseRemovalViewModel vm)
            {
                vm.Confirm();
            }

            DialogResult = true;
        }

        private void OnCancelClicked(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}