using OpenCvSharp;
using AutoPlot.Models;
using System.Windows.Media;

namespace AutoPlot.Utils
{
    public static class PlotColors
    {
        public static readonly Color[] SeriesColors =
        {
            Color.FromRgb(0x5B, 0x8F, 0xF9),
            Color.FromRgb(0x5A, 0xD8, 0xA6),
            Color.FromRgb(0xF6, 0xBD, 0x16),
            Color.FromRgb(0x92, 0x70, 0xCA),
            Color.FromRgb(0xFF, 0x99, 0xC3),
            Color.FromRgb(0x6D, 0xC8, 0xEC),
            Color.FromRgb(0x7B, 0xC9, 0x6F),
            Color.FromRgb(0xE8, 0x64, 0x52)
        };

        public static Color GraphBackground { get; private set; } = Color.FromRgb(0xFA, 0xFA, 0xFA);
        public static Color GridLine { get; private set; } = Color.FromRgb(0xE5, 0xE7, 0xEB);
        public static Color AxisLine { get; private set; } = Color.FromRgb(0x37, 0x41, 0x51);
        public static Color LabelText { get; private set; } = Color.FromRgb(0x4B, 0x55, 0x63);
        public static Color NoiseMask { get; private set; } = Color.FromRgb(0xE8, 0x64, 0x52);
        public static Color RoiHighlight { get; private set; } = Color.FromRgb(0x5A, 0xD8, 0xA6);

        public static void ApplyTheme(AppTheme theme)
        {
            switch (theme)
            {
                case AppTheme.Dark:
                    GraphBackground = Color.FromRgb(0x1B, 0x20, 0x2A);
                    GridLine = Color.FromRgb(0x38, 0x42, 0x52);
                    AxisLine = Color.FromRgb(0xD8, 0xE2, 0xEE);
                    LabelText = Color.FromRgb(0xA8, 0xB3, 0xC2);
                    NoiseMask = Colors.White;
                    RoiHighlight = Color.FromRgb(0x5A, 0xD8, 0xA6);
                    break;
                case AppTheme.ChocoMint:
                    GraphBackground = Color.FromRgb(0xFF, 0xFF, 0xFC);
                    GridLine = Color.FromRgb(0xD6, 0xEE, 0xE5);
                    AxisLine = Color.FromRgb(0x4E, 0x36, 0x2E);
                    LabelText = Color.FromRgb(0x68, 0x5B, 0x55);
                    NoiseMask = Color.FromRgb(0xD9, 0x68, 0x62);
                    RoiHighlight = Color.FromRgb(0x55, 0xC7, 0xA7);
                    break;
                default:
                    GraphBackground = Color.FromRgb(0xFA, 0xFA, 0xFA);
                    GridLine = Color.FromRgb(0xE5, 0xE7, 0xEB);
                    AxisLine = Color.FromRgb(0x37, 0x41, 0x51);
                    LabelText = Color.FromRgb(0x4B, 0x55, 0x63);
                    NoiseMask = Color.FromRgb(0xE8, 0x64, 0x52);
                    RoiHighlight = Color.FromRgb(0x5A, 0xD8, 0xA6);
                    break;
            }
        }

        public static Brush GetSeriesBrush(int index)
        {
            var brush = new SolidColorBrush(GetSeriesColor(index));
            brush.Freeze();
            return brush;
        }

        public static Scalar GetSeriesScalar(int index)
        {
            return ToBgrScalar(GetSeriesColor(index));
        }

        public static Scalar GetSeriesScalarBgra(int index, byte alpha = 255)
        {
            return ToBgraScalar(GetSeriesColor(index), alpha);
        }

        public static Scalar GraphBackgroundScalar => ToBgrScalar(GraphBackground);
        public static Scalar GridLineScalar => ToBgrScalar(GridLine);
        public static Scalar AxisLineScalar => ToBgrScalar(AxisLine);
        public static Scalar LabelTextScalar => ToBgrScalar(LabelText);
        public static Scalar NoiseMaskScalar => ToBgrScalar(NoiseMask);
        public static Scalar RoiHighlightScalar => ToBgrScalar(RoiHighlight);

        private static Color GetSeriesColor(int index)
        {
            return SeriesColors[index % SeriesColors.Length];
        }

        private static Scalar ToBgrScalar(Color color)
        {
            return new Scalar(color.B, color.G, color.R);
        }

        private static Scalar ToBgraScalar(Color color, byte alpha)
        {
            return new Scalar(color.B, color.G, color.R, alpha);
        }
    }
}
