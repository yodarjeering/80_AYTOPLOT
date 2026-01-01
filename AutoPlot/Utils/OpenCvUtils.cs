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
            var gridColor = new Scalar(220, 220, 220); // 薄いグレー

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
                    HersheyFonts.HersheySimplex,
                    0.4,
                    new Scalar(0, 0, 0),
                    1
                );
            }

            string FormatValue(double v, bool isLog)
            {
                return isLog
                    ? $"10^{Math.Log10(v):0.#}"
                    : v.ToString("G4");
            }

            // X軸ラベル
            DrawLabel(FormatValue(xMin, isXLog), 5, height - 5);
            DrawLabel(FormatValue((xMin + xMax) / 2, isXLog), width / 2 - 20, height - 5);
            DrawLabel(FormatValue(xMax, isXLog), width - 60, height - 5);

            // Y軸ラベル
            DrawLabel(FormatValue(yMin, isYLog), 5, height - 20);
            DrawLabel(FormatValue((yMin + yMax) / 2, isYLog), 5, height / 2);
            DrawLabel(FormatValue(yMax, isYLog), 5, 15);

            return canvas;
        }

    }
    
    
}
