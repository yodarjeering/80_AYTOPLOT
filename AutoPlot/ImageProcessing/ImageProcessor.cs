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
        public CurveData Process(Mat inputImage,
                                 double xMinInput, double xMaxInput,
                                 double yMinInput, double yMaxInput,
                                 string xScale, string yScale)
        {
            // 画像読み込み
            Mat imgRgb = new();
            Cv2.CvtColor(inputImage, imgRgb, ColorConversionCodes.BGR2RGB);

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

            // Mat → LineSegmentPoint
            List<LineSegmentPoint> horizontalSegments =
                ExtractLineSegments(horizontalLines, isHorizontal: true);

            List<LineSegmentPoint> verticalSegments =
                ExtractLineSegments(verticalLines, isHorizontal: false);


            Rect roi = CalculatePlotRoi(verticalSegments, horizontalSegments);

            Mat grid = new();
            Cv2.BitwiseOr(horizontalLines, verticalLines, grid);

            Mat bwNoGrid = new();
            Cv2.Subtract(bw, grid, bwNoGrid);

            // 元画像からROIだけ切り出す
            Mat plotArea = new Mat(bwNoGrid, roi);

            // 連結成分解析
            Mat labels = new();
            Mat stats = new();
            Mat centroids = new();

            int numLabels = Cv2.ConnectedComponentsWithStats(
                plotArea,
                labels,
                stats,
                centroids,
                PixelConnectivity.Connectivity8
                );

            Mat clean = Mat.Zeros(plotArea.Size(), MatType.CV_8UC1);
            int minArea = 10;

            for (int i = 1; i < numLabels; i++)
            {
                int area = stats.Get<int>(i, (int)ConnectedComponentsTypes.Area);
                if (area >= minArea)
                {
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

            return new CurveData
            {
                Points = points,
                PlotRoi = roi   // ★ ここが肝
            };
        }

        Rect CalculatePlotRoi(
            List<LineSegmentPoint> verticalLines,
            List<LineSegmentPoint> horizontalLines)
        {
            if (verticalLines.Count < 2 || horizontalLines.Count < 2)
                throw new InvalidOperationException("ROI算出に十分な線が検出されていない");

            int xMin = verticalLines.Min(l => Math.Min(l.P1.X, l.P2.X));
            int xMax = verticalLines.Max(l => Math.Max(l.P1.X, l.P2.X));


            int yMin = horizontalLines.Min(l => Math.Min(l.P1.Y, l.P2.Y));
            int yMax = horizontalLines.Max(l => Math.Max(l.P1.Y, l.P2.Y));


            return new Rect(
                xMin,
                yMin,
                xMax - xMin,
                yMax - yMin
            );
        }

        List<LineSegmentPoint> ExtractLineSegments(
            Mat lineImage,
            bool isHorizontal)
        {
            Cv2.FindContours(
                lineImage,
                out Point[][] contours,
                out _,
                RetrievalModes.External,
                ContourApproximationModes.ApproxSimple
            );

            var segments = new List<LineSegmentPoint>();

            foreach (var c in contours)
            {
                Rect r = Cv2.BoundingRect(c);

                // 方向判定
                if (isHorizontal && r.Width <= r.Height * 5)
                    continue;
                if (!isHorizontal && r.Height <= r.Width * 5)
                    continue;

                LineSegmentPoint seg;

                if (isHorizontal)
                {
                    int y = r.Top + r.Height / 2;
                    seg = new LineSegmentPoint(
                        new Point(r.Left,  y),
                        new Point(r.Right, y)
                    );
                }
                else
                {
                    int x = r.Left + r.Width / 2;
                    seg = new LineSegmentPoint(
                        new Point(x, r.Top),
                        new Point(x, r.Bottom)
                    );
                }

                segments.Add(seg);
            }

            return segments;
        }

    }
}
