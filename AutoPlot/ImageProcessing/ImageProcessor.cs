using OpenCvSharp;
using AutoPlot.Models;
using MathNet.Numerics.Statistics;
using AutoPlot.ImageProcessing.Helpers;
using System.Windows;


namespace AutoPlot.ImageProcessing
{
    public class ImageProcessor
    {

        public OpenCvSharp.Rect DetectPlotRoi(Mat workingImage)
        {
            // グレースケール
            Mat gray = new();
            Cv2.CvtColor(workingImage, gray, ColorConversionCodes.BGR2GRAY);

            // 二値化
            Mat bw = new();
            Cv2.Threshold(gray, bw, 0, 255,
                ThresholdTypes.BinaryInv | ThresholdTypes.Otsu);

            // 横線・縦線抽出
            Mat hKernel = Cv2.GetStructuringElement(
                MorphShapes.Rect, new OpenCvSharp.Size(40, 1));
            Mat horizontalLines = new();
            Cv2.MorphologyEx(bw, horizontalLines,
                MorphTypes.Open, hKernel);

            Mat vKernel = Cv2.GetStructuringElement(
                MorphShapes.Rect, new OpenCvSharp.Size(1, 40));
            Mat verticalLines = new();
            Cv2.MorphologyEx(bw, verticalLines,
                MorphTypes.Open, vKernel);

            // LineSegmentPoint 化
            var horizontalSegments =
                ExtractLineSegments(horizontalLines, isHorizontal: true);

            var verticalSegments =
                ExtractLineSegments(verticalLines, isHorizontal: false);

            // ★ ROI はここで一度だけ決める
            OpenCvSharp.Rect roi =
                CalculatePlotRoi(verticalSegments, horizontalSegments);

            return roi;
        }

        public CurveData ProcessPlotArea(
            Mat plotAreaForGrid,                 // ROI切り出し済み
            Mat plotAreaForAnalysis,
            OpenCvSharp.Rect roi,          // 呼び出し元の確定値
            OpenCvSharp.Size workingImageSize,         // overlay用
            double xMinInput, double xMaxInput,
            double yMinInput, double yMaxInput,
            string xScale, string yScale)
        {
            // plotArea はすでに ROI 内なので、そのまま使う
            Mat gray = new();

            Cv2.CvtColor(plotAreaForGrid, gray, ColorConversionCodes.BGR2GRAY);

            Mat bw = new();
            Cv2.Threshold(gray, bw, 0, 255,
                ThresholdTypes.BinaryInv | ThresholdTypes.Otsu);

            // === グリッド除去（ROI内）===
            Mat hKernel = Cv2.GetStructuringElement(
                MorphShapes.Rect, new OpenCvSharp.Size(40, 1));
            Mat horizontalLines = new();
            Cv2.MorphologyEx(bw, horizontalLines,
                MorphTypes.Open, hKernel);


            Mat vKernel = Cv2.GetStructuringElement(
                MorphShapes.Rect, new OpenCvSharp.Size(1, 40));
            Mat verticalLines = new();
            Cv2.MorphologyEx(bw, verticalLines,
                MorphTypes.Open, vKernel);

            Mat grid = new();
            Cv2.BitwiseOr(horizontalLines, verticalLines, grid);

            Mat bwNoGrid = new();
            Cv2.Subtract(bw, grid, bwNoGrid);

            // ノイズマスク適用
            if(plotAreaForAnalysis!=null){
                Mat grayMask = new();

                Cv2.CvtColor(plotAreaForAnalysis, grayMask, ColorConversionCodes.BGR2GRAY);
                bwNoGrid.SetTo(0, grayMask);
            }

            // === 連結成分解析 ===
            Mat labels = new();
            Mat stats = new();
            Mat centroids = new();

            int numLabels = Cv2.ConnectedComponentsWithStats(
                bwNoGrid, labels, stats, centroids,
                PixelConnectivity.Connectivity8);

            Mat clean = Mat.Zeros(bwNoGrid.Size(), MatType.CV_8UC1);


            int minArea = 10;

            for (int i = 1; i < numLabels; i++)
            {
                int area = stats.Get<int>(i, (int)ConnectedComponentsTypes.Area);
                if (area >= minArea)
                {
                    using var mask = new Mat();
                    Cv2.Compare(labels, i, mask, CmpType.EQ);
                    Cv2.BitwiseOr(clean, mask, clean);
                }
            }

            // ==== TBD debug用 ====
            // Cv2.ImShow("debug clean", clean);
            // Cv2.WaitKey(0);   // キー押すまで止まる
            // Cv2.DestroyAllWindows();
            // =====

            Mat kernel = Cv2.GetStructuringElement(
                MorphShapes.Rect, new OpenCvSharp.Size(3, 3));
            Mat clean2 = new();
            Cv2.MorphologyEx(clean, clean2,
                MorphTypes.Close, kernel);

            // === Xごとに中央値Y抽出 ===
            var pointsPx = new List<ImagePoint>();

            for (int x = 0; x < clean2.Cols; x++)
            {
                var ys = new List<int>();

                for (int y = 0; y < clean2.Rows; y++)
                {
                    if (clean2.At<byte>(y, x) > 0)
                        ys.Add(y);
                }

                if (ys.Count > 0)
                {
                    pointsPx.Add(new ImagePoint
                    {
                        X = x,
                        Y = Statistics.Median(ys.Select(v => (double)v))
                    });
                }
            }

            // === px → real ===
            double[] xPx = pointsPx.Select(p => p.X).ToArray();
            double[] yPx = pointsPx.Select(p => p.Y).ToArray();

            double[] xReal = PixelConverter.PxToReal(
                xPx, 0, clean2.Cols,
                xMinInput, xMaxInput, xScale);

            double[] yReal = PixelConverter.PxToReal(
                yPx, 0, clean2.Rows,
                yMinInput, yMaxInput, yScale, true);

            var realPoints = new List<ImagePoint>();
            for (int i = 0; i < xReal.Length; i++)
            {
                realPoints.Add(new ImagePoint
                {
                    X = xReal[i],
                    Y = yReal[i]
                });
            }

            // === Overlay 作成（原図サイズ）===
            var overlay = CreateGraphOverlay(
                workingImageSize,
                pointsPx.Select(p =>
                    new OpenCvSharp.Point(
                        p.X + roi.X,
                        p.Y + roi.Y)).ToList(),
                new OpenCvSharp.Point(roi.X, roi.Y)
            );

            return new CurveData
            {
                Points = realPoints,
                PlotRoi = roi,
                OverlayGraphMat = overlay
            };
        }


        OpenCvSharp.Rect CalculatePlotRoi(
            List<LineSegmentPoint> verticalLines,
            List<LineSegmentPoint> horizontalLines)
        {
            if (verticalLines.Count < 2 || horizontalLines.Count < 2)
                throw new InvalidOperationException("ROI算出に十分な線が検出されていない");

            int xMin = verticalLines.Min(l => Math.Min(l.P1.X, l.P2.X));
            int xMax = verticalLines.Max(l => Math.Max(l.P1.X, l.P2.X));


            int yMin = horizontalLines.Min(l => Math.Min(l.P1.Y, l.P2.Y));
            int yMax = horizontalLines.Max(l => Math.Max(l.P1.Y, l.P2.Y));


            return new OpenCvSharp.Rect(
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
                out OpenCvSharp.Point[][] contours,
                out _,
                RetrievalModes.External,
                ContourApproximationModes.ApproxSimple
            );

            var segments = new List<LineSegmentPoint>();

            foreach (var c in contours)
            {
                OpenCvSharp.Rect r = Cv2.BoundingRect(c);

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
                        new OpenCvSharp.Point(r.Left,  y),
                        new OpenCvSharp.Point(r.Right, y)
                    );
                }
                else
                {
                    int x = r.Left + r.Width / 2;
                    seg = new LineSegmentPoint(
                        new OpenCvSharp.Point(x, r.Top),
                        new OpenCvSharp.Point(x, r.Bottom)
                    );
                }

                segments.Add(seg);
            }

            return segments;
        }

        private Mat CreateGraphOverlay(
            OpenCvSharp.Size imageSize,
            List<OpenCvSharp.Point> graphPoints,
            OpenCvSharp.Point roiOffset)
        {
            // 透明背景（RGBA）
            Mat overlay = new Mat(
                imageSize,
                MatType.CV_8UC4,
                Scalar.All(0)
            );

            for (int i = 1; i < graphPoints.Count; i++)
            {
                Cv2.Line(
                    overlay,
                    graphPoints[i - 1],
                    graphPoints[i],
                    new Scalar(255, 0, 0, 255), // ←こいつは必要
                    2
                );
            }

            return overlay;
        }

    }
}
