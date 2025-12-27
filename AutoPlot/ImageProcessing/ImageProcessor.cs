using OpenCvSharp;
using AutoPlot.Models;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.Statistics;
using AutoPlot.ImageProcessing.Helpers;
using AutoPlot.Utils;        // OpenCvUtils クラス
using System;

namespace AutoPlot.ImageProcessing
{
    public class ImageProcessor
    {
        public CurveData Process(string imagePath,
                                 double xMinInput, double xMaxInput,
                                 double yMinInput, double yMaxInput,
                                 string xScale, string yScale)
        {
            // 画像読み込み
            Mat imgBgr = OpenCvUtils.ReadImage(imagePath);
            Mat imgRgb = new();
            Cv2.CvtColor(imgBgr, imgRgb, ColorConversionCodes.BGR2RGB);

            Mat gray = new();
            Cv2.CvtColor(imgRgb, gray, ColorConversionCodes.RGB2GRAY);

            Mat bw = new();
            Cv2.Threshold(gray, bw, 0, 255, ThresholdTypes.BinaryInv | ThresholdTypes.Otsu);

            // 横線・縦線除去
            Mat hKernel = Cv2.GetStructuringElement(MorphShapes.Rect, new(40, 1));
            Mat horizontalLines = new();
            Cv2.MorphologyEx(bw, horizontalLines, MorphTypes.Open, hKernel);

            Mat vKernel = Cv2.GetStructuringElement(MorphShapes.Rect, new(1, 40));
            Mat verticalLines = new();
            Cv2.MorphologyEx(bw, verticalLines, MorphTypes.Open, vKernel);

            Mat grid = new();
            Cv2.BitwiseOr(horizontalLines, verticalLines, grid);

            Mat bwNoGrid = new();
            Cv2.Subtract(bw, grid, bwNoGrid);

            // 連結成分解析
            Mat labels = new();
            Mat stats = new();
            Mat centroids = new();
            // int numLabels = Cv2.ConnectedComponentsWithStats(bwNoGrid,
            //                        labels, stats, centroids, Connectivity.EightConnected);
            int numLabels = Cv2.ConnectedComponentsWithStats(
                bwNoGrid,
                labels,
                stats,
                centroids,
                PixelConnectivity.Connectivity8
                );

            Mat clean = Mat.Zeros(bwNoGrid.Size(), MatType.CV_8UC1);
            int minArea = 10;

            for (int i = 1; i < numLabels; i++)
            {
                // int area = stats.Get<int>(i, (int)ConnectedComponentsTypes.CC_STAT_AREA);
                int area = stats.Get<int>(i, (int)ConnectedComponentsTypes.Area);
                if (area >= minArea)
                {
                    // Cv2.Compare(labels, i, clean, CmpType.Equal);
                    Cv2.Compare(labels, i, clean,CmpType.EQ);
                }
            }

            Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new(3, 3));
            Mat clean2 = new();
            Cv2.MorphologyEx(clean, clean2, MorphTypes.Close, kernel);

            // 白ピクセル座標取得
            var points = new List<ImagePoint>();
            for (int y = 0; y < clean2.Rows; y++)
            {
                for (int x = 0; x < clean2.Cols; x++)
                {
                    if (clean2.At<byte>(y, x) > 0)
                        points.Add(new ImagePoint { X = x, Y = y });
                }
            }

            // Xごとに中央値 y を求める
            var grouped = points.GroupBy(p => (int)p.X)
                                .Select(g =>
                                {
                                    var ys = g.Select(p => p.Y).ToList();
                                    return new ImagePoint
                                    {
                                        X = g.Key,
                                        Y = Statistics.Median(ys)
                                    };
                                })
                                .OrderBy(p => p.X)
                                .ToList();

            var data = new CurveData { Points = grouped };

            // px → real
            double[] xPx = grouped.Select(p => p.X).ToArray();
            double[] yPx = grouped.Select(p => p.Y).ToArray();

            int xPxMin = 0;
            int xPxMax = clean2.Cols;
            int yPxMin = 0;
            int yPxMax = clean2.Rows;

            double[] xReal = PixelConverter.PxToReal(xPx, xPxMin, xPxMax,
                                                     xMinInput, xMaxInput, xScale);
            double[] yReal = PixelConverter.PxToReal(yPx, yPxMin, yPxMax,
                                                     yMinInput, yMaxInput, yScale, true);

            for (int i = 0; i < data.Points.Count; i++)
            {
                data.Points[i] = new ImagePoint
                {
                    X = xReal[i],
                    Y = yReal[i]
                };
            }

            return data;
        }
    }
}
