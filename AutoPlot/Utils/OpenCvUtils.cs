using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System.Windows.Media.Imaging;
using AutoPlot.Models;

namespace AutoPlot.Utils
{
    public static class OpenCvUtils
    {
        private const int GraphGridDivisions = 10;

        public static Mat ReadImage(string path)
        {
            var img = Cv2.ImRead(path);
            if (img.Empty())
                throw new System.IO.FileNotFoundException($"画像が開けへん: {path}");
            return img;
        }
         
        public static BitmapImage LoadBitmap(string path)
        {
            BitmapImage bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(path, UriKind.Absolute);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }

        public static Mat BitmapImageToMat(BitmapSource bitmap)
        {
            return BitmapSourceConverter.ToMat(bitmap);
        }

        public static Mat RenderGraphFromCurveData(
            CurveData data,
            int width,
            int height,
            double xMin,
            double xMax,
            double yMin,
            double yMax,
            bool isXLog,
            bool isYLog
        )
        {
            var canvas = new Mat(height, width, MatType.CV_8UC3, PlotColors.GraphBackgroundScalar);
            DrawGraphFrame(canvas, width, height, xMin, xMax, yMin, yMax, isXLog, isYLog);

            if (data?.Points == null || data.Points.Count < 2)
                return canvas;

            double ToPixelX(double x)
            {
                if (isXLog && x <= 0) return double.NaN;

                double xx = isXLog ? Math.Log10(x) : x;
                double min = isXLog ? Math.Log10(xMin) : xMin;
                double max = isXLog ? Math.Log10(xMax) : xMax;

                if (Math.Abs(max - min) < 1e-12) return double.NaN;

                return (xx - min) / (max - min) * (width - 1);
            }

            double ToPixelY(double y)
            {
                if (isYLog && y <= 0) return double.NaN;

                double yy = isYLog ? Math.Log10(y) : y;
                double min = isYLog ? Math.Log10(yMin) : yMin;
                double max = isYLog ? Math.Log10(yMax) : yMax;

                if (Math.Abs(max - min) < 1e-12) return double.NaN;

                return height - 1 - (yy - min) / (max - min) * (height - 1);
            }

            int Clamp(int v, int min, int max)
                => Math.Max(min, Math.Min(max, v));

            for (int i = 1; i < data.Points.Count; i++)
            {
                var p1 = data.Points[i - 1];
                var p2 = data.Points[i];

                double x1 = ToPixelX(p1.X);
                double y1 = ToPixelY(p1.Y);
                double x2 = ToPixelX(p2.X);
                double y2 = ToPixelY(p2.Y);

                if (double.IsNaN(x1) || double.IsNaN(y1) ||
                    double.IsNaN(x2) || double.IsNaN(y2))
                    continue;

                var pt1 = new Point(
                    Clamp((int)x1, 0, width - 1),
                    Clamp((int)y1, 0, height - 1)
                );

                var pt2 = new Point(
                    Clamp((int)x2, 0, width - 1),
                    Clamp((int)y2, 0, height - 1)
                );

                Cv2.Line(canvas, pt1, pt2, PlotColors.GetSeriesScalar(0), 2, LineTypes.AntiAlias);
            }

            return canvas;
        }

        public static Mat RenderGraphFromSeries(
            List<List<ImagePoint>> seriesList,
            int width,
            int height,
            double xMin,
            double xMax,
            double yMin,
            double yMax,
            bool isXLog,
            bool isYLog
        )
        {
            var canvas = CreateGraphCanvas(width, height, xMin, xMax, yMin, yMax, isXLog, isYLog);

            if (seriesList == null || seriesList.Count == 0)
                return canvas;

            for (int seriesIndex = 0; seriesIndex < seriesList.Count; seriesIndex++)
            {
                var series = seriesList[seriesIndex];
                if (series == null || series.Count < 2)
                    continue;

                for (int i = 1; i < series.Count; i++)
                {
                    var p1 = ToGraphPixel(series[i - 1], width, height, xMin, xMax, yMin, yMax, isXLog, isYLog);
                    var p2 = ToGraphPixel(series[i], width, height, xMin, xMax, yMin, yMax, isXLog, isYLog);

                    if (p1 == null || p2 == null)
                        continue;

                    Cv2.Line(canvas, p1.Value, p2.Value, PlotColors.GetSeriesScalar(seriesIndex), 2, LineTypes.AntiAlias);
                }
            }

            return canvas;
        }

        private static Mat CreateGraphCanvas(
            int width,
            int height,
            double xMin,
            double xMax,
            double yMin,
            double yMax,
            bool isXLog,
            bool isYLog)
        {
            var canvas = new Mat(height, width, MatType.CV_8UC3, PlotColors.GraphBackgroundScalar);
            DrawGraphFrame(canvas, width, height, xMin, xMax, yMin, yMax, isXLog, isYLog);

            return canvas;
        }

        private static void DrawGraphFrame(
            Mat canvas,
            int width,
            int height,
            double xMin,
            double xMax,
            double yMin,
            double yMax,
            bool isXLog,
            bool isYLog)
        {
            var xTicks = BuildAxisTicks(xMin, xMax, isXLog);
            var yTicks = BuildAxisTicks(yMin, yMax, isYLog);

            foreach (double xValue in xTicks.Skip(1).SkipLast(1))
            {
                int x = AxisValueToPixel(xValue, xMin, xMax, width, isXLog);
                Cv2.Line(canvas, new Point(x, 0), new Point(x, height - 1), PlotColors.GridLineScalar, 1, LineTypes.AntiAlias);
            }

            foreach (double yValue in yTicks.Skip(1).SkipLast(1))
            {
                int y = AxisValueToPixel(yValue, yMin, yMax, height, isYLog, invert: true);
                Cv2.Line(canvas, new Point(0, y), new Point(width - 1, y), PlotColors.GridLineScalar, 1, LineTypes.AntiAlias);
            }

            Cv2.Rectangle(
                canvas,
                new Point(0, 0),
                new Point(width - 1, height - 1),
                PlotColors.AxisLineScalar,
                2
            );

            foreach (double xValue in xTicks)
            {
                int x = AxisValueToPixel(xValue, xMin, xMax, width, isXLog);
                DrawGraphLabel(canvas, FormatTickLabel(xValue), Clamp(x - 18, 4, width - 58), height - 5);
            }

            foreach (double yValue in yTicks)
            {
                int y = AxisValueToPixel(yValue, yMin, yMax, height, isYLog, invert: true);
                DrawGraphLabel(canvas, FormatTickLabel(yValue), 5, Clamp(y + 4, 14, height - 18));
            }
        }

        private static void DrawGraphLabel(Mat canvas, string text, int x, int y)
        {
            Cv2.PutText(
                canvas,
                text,
                new Point(x, y),
                HersheyFonts.HersheyDuplex,
                0.34,
                PlotColors.LabelTextScalar,
                1,
                LineTypes.AntiAlias
            );
        }

        private static List<double> BuildAxisTicks(double min, double max, bool isLog)
        {
            if (!isLog)
            {
                return Enumerable
                    .Range(0, GraphGridDivisions + 1)
                    .Select(i => min + (max - min) * i / GraphGridDivisions)
                    .ToList();
            }

            if (min <= 0 || max <= 0)
                return new List<double> { min, max };

            int minPower = (int)Math.Ceiling(Math.Log10(min));
            int maxPower = (int)Math.Floor(Math.Log10(max));
            var ticks = new List<double> { min };

            for (int power = minPower; power <= maxPower; power++)
            {
                double tick = Math.Pow(10, power);
                if (tick > min && tick < max)
                    ticks.Add(tick);
            }

            ticks.Add(max);
            return ticks
                .Distinct()
                .OrderBy(v => v)
                .ToList();
        }

        private static int AxisValueToPixel(
            double value,
            double min,
            double max,
            int pixelLength,
            bool isLog,
            bool invert = false)
        {
            double v = value;
            double axisMin = min;
            double axisMax = max;

            if (isLog)
            {
                if (value <= 0 || min <= 0 || max <= 0)
                    return 0;

                v = Math.Log10(value);
                axisMin = Math.Log10(min);
                axisMax = Math.Log10(max);
            }

            if (Math.Abs(axisMax - axisMin) < 1e-12)
                return 0;

            double t = (v - axisMin) / (axisMax - axisMin);
            if (invert)
                t = 1.0 - t;

            return Clamp((int)Math.Round(t * (pixelLength - 1)), 0, pixelLength - 1);
        }

        private static string FormatTickLabel(double value)
        {
            return double.IsNaN(value) || double.IsInfinity(value)
                ? ""
                : value.ToString("G3");
        }

        private static Point? ToGraphPixel(
            ImagePoint point,
            int width,
            int height,
            double xMin,
            double xMax,
            double yMin,
            double yMax,
            bool isXLog,
            bool isYLog)
        {
            double x = ToPixelX(point.X, width, xMin, xMax, isXLog);
            double y = ToPixelY(point.Y, height, yMin, yMax, isYLog);

            if (double.IsNaN(x) || double.IsNaN(y))
                return null;

            return new Point(
                Clamp((int)x, 0, width - 1),
                Clamp((int)y, 0, height - 1)
            );
        }

        private static double ToPixelX(double x, int width, double xMin, double xMax, bool isXLog)
        {
            if (isXLog && x <= 0) return double.NaN;

            double xx = isXLog ? Math.Log10(x) : x;
            double min = isXLog ? Math.Log10(xMin) : xMin;
            double max = isXLog ? Math.Log10(xMax) : xMax;

            if (Math.Abs(max - min) < 1e-12) return double.NaN;

            return (xx - min) / (max - min) * (width - 1);
        }

        private static double ToPixelY(double y, int height, double yMin, double yMax, bool isYLog)
        {
            if (isYLog && y <= 0) return double.NaN;

            double yy = isYLog ? Math.Log10(y) : y;
            double min = isYLog ? Math.Log10(yMin) : yMin;
            double max = isYLog ? Math.Log10(yMax) : yMax;

            if (Math.Abs(max - min) < 1e-12) return double.NaN;

            return height - 1 - (yy - min) / (max - min) * (height - 1);
        }

        private static int Clamp(int v, int min, int max)
            => Math.Max(min, Math.Min(max, v));

    }
    
    
}
