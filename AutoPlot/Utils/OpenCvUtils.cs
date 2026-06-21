using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System.Windows.Media.Imaging;
using AutoPlot.Models;

namespace AutoPlot.Utils
{
    public static class OpenCvUtils
    {
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
            var canvas = new Mat(height, width, MatType.CV_8UC3, Scalar.White);

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

                Cv2.Line(canvas, pt1, pt2, new Scalar(0, 0, 255), 2);
            }

            // 枠線（グラフ領域）
            Cv2.Rectangle(
                canvas,
                new Point(0, 0),
                new Point(width - 1, height - 1),
                new Scalar(0, 0, 0),
                2
            );

            int gridCount = 5;
            var gridColor = new Scalar(0, 0, 0); // 黒

            for (int i = 1; i < gridCount; i++)
            {
                int x = i * width / gridCount;
                int y = i * height / gridCount;

                // 縦グリッド
                Cv2.Line(canvas,
                    new Point(x, 0),
                    new Point(x, height - 1),
                    gridColor,
                    1
                );

                // 横グリッド
                Cv2.Line(canvas,
                    new Point(0, y),
                    new Point(width - 1, y),
                    gridColor,
                    1
                );
            }

            void DrawLabel(string text, int x, int y)
            {
                Cv2.PutText(
                    canvas,
                    text,
                    new Point(x, y),
                    HersheyFonts.HersheyDuplex,
                    0.45,
                    new Scalar(60, 60, 60),
                    1,
                    LineTypes.AntiAlias
                    );

            }

            // X軸ラベル
            double xMid = isXLog
                ? Math.Sqrt(xMin * xMax)
                : (xMin + xMax) / 2;

            DrawLabel(xMin.ToString("G4"), 5, height - 5);
            DrawLabel(xMid.ToString("G4"), width / 2 - 20, height - 5);
            DrawLabel(xMax.ToString("G4"), width - 60, height - 5);

            // Y軸ラベル
            double yMid = isYLog
                ? Math.Sqrt(yMin * yMax)
                : (yMin + yMax) / 2;

            DrawLabel(yMin.ToString("G4"), 5, height - 20);
            DrawLabel(yMid.ToString("G4"), 5, height / 2);
            DrawLabel(yMax.ToString("G4"), 5, 15);



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

            Scalar[] colors =
            {
                new Scalar(0, 0, 255),
                new Scalar(255, 0, 0),
                new Scalar(0, 160, 0),
                new Scalar(0, 165, 255),
                new Scalar(128, 0, 128),
                new Scalar(42, 42, 165),
                new Scalar(147, 20, 255),
                new Scalar(128, 128, 0)
            };

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

                    Cv2.Line(canvas, p1.Value, p2.Value, colors[seriesIndex % colors.Length], 2);
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
            var canvas = new Mat(height, width, MatType.CV_8UC3, Scalar.White);

            Cv2.Rectangle(
                canvas,
                new Point(0, 0),
                new Point(width - 1, height - 1),
                new Scalar(0, 0, 0),
                2
            );

            int gridCount = 5;
            var gridColor = new Scalar(0, 0, 0);

            for (int i = 1; i < gridCount; i++)
            {
                int x = i * width / gridCount;
                int y = i * height / gridCount;

                Cv2.Line(canvas, new Point(x, 0), new Point(x, height - 1), gridColor, 1);
                Cv2.Line(canvas, new Point(0, y), new Point(width - 1, y), gridColor, 1);
            }

            void DrawLabel(string text, int x, int y)
            {
                Cv2.PutText(
                    canvas,
                    text,
                    new Point(x, y),
                    HersheyFonts.HersheyDuplex,
                    0.45,
                    new Scalar(60, 60, 60),
                    1,
                    LineTypes.AntiAlias
                );
            }

            double xMid = isXLog
                ? Math.Sqrt(xMin * xMax)
                : (xMin + xMax) / 2;

            DrawLabel(xMin.ToString("G4"), 5, height - 5);
            DrawLabel(xMid.ToString("G4"), width / 2 - 20, height - 5);
            DrawLabel(xMax.ToString("G4"), width - 60, height - 5);

            double yMid = isYLog
                ? Math.Sqrt(yMin * yMax)
                : (yMin + yMax) / 2;

            DrawLabel(yMin.ToString("G4"), 5, height - 20);
            DrawLabel(yMid.ToString("G4"), 5, height / 2);
            DrawLabel(yMax.ToString("G4"), 5, 15);

            return canvas;
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
