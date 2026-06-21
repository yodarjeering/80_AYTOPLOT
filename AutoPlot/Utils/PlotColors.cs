using OpenCvSharp;
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

        public static readonly Color GraphBackground = Color.FromRgb(0xFA, 0xFA, 0xFA);
        public static readonly Color GridLine = Color.FromRgb(0xE5, 0xE7, 0xEB);
        public static readonly Color AxisLine = Color.FromRgb(0x37, 0x41, 0x51);
        public static readonly Color LabelText = Color.FromRgb(0x4B, 0x55, 0x63);
        public static readonly Color NoiseMask = Color.FromRgb(0xE8, 0x64, 0x52);
        public static readonly Color RoiHighlight = Color.FromRgb(0x5A, 0xD8, 0xA6);

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
