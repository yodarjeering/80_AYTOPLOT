using AutoPlot.Models;
using OpenCvSharp;
using WpfPoint = System.Windows.Point;

namespace AutoPlot.ImageProcessing
{
    /// <summary>
    /// Detects plot series from geometry first. Colour is used only to build a
    /// more complete ink mask, so monochrome and same-colour plots also work.
    /// </summary>
    public static class AutoSeriesDetector
    {
        private const int MaximumSeriesCount = 16;
        private const int MaximumGapColumns = 6;

        public static List<List<WpfPoint>> Detect(Mat plotArea, ExtractionSettings settings)
        {
            if (plotArea == null || plotArea.Empty() || plotArea.Width < 3 || plotArea.Height < 3)
                return new List<List<WpfPoint>>();

            using var bgr = ToBgr(plotArea);
            using var gray = new Mat();
            using var hsv = new Mat();
            Cv2.CvtColor(bgr, gray, ColorConversionCodes.BGR2GRAY);
            Cv2.CvtColor(bgr, hsv, ColorConversionCodes.BGR2HSV);

            using var darkInk = new Mat();
            using var colouredInk = new Mat();
            using var ink = new Mat();
            Cv2.Threshold(gray, darkInk, settings.CurveThreshold, 255, ThresholdTypes.BinaryInv);
            Cv2.InRange(hsv, new Scalar(0, 45, 0), new Scalar(179, 255, 249), colouredInk);
            Cv2.BitwiseOr(darkInk, colouredInk, ink);

            RemoveLongGridLines(ink);
            RemovePlotFrame(ink);

            // The manual trace band is intentionally wide, but using that width
            // for automatic tracking can pull a temporarily hidden series onto a
            // neighbouring curve. Automatic tracking uses a tighter local step.
            int maxJump = Math.Clamp(settings.TraceSearchBandWidth / 3, 3, 8);
            List<List<WpfPoint>> horizontalCandidates = DetectInScanDirection(
                ink, maxJump, settings.MinCurveLength, transposeResult: false);

            // A column-wise tracker cannot represent a near-vertical curve well:
            // one x coordinate contains many y values. Run exactly the same
            // detector on the transposed mask, then map its points back.
            using var transposedInk = new Mat();
            Cv2.Transpose(ink, transposedInk);
            List<List<WpfPoint>> verticalCandidates = DetectInScanDirection(
                transposedInk, maxJump, settings.MinCurveLength, transposeResult: true);

            return MergeDirectionalCandidates(
                horizontalCandidates, verticalCandidates, ink, MaximumSeriesCount);
        }

        private static List<List<WpfPoint>> DetectInScanDirection(
            Mat ink,
            int maxJump,
            int configuredMinimumLength,
            bool transposeResult)
        {
            var tracks = TrackCandidates(ink, maxJump);
            MergeCompatibleFragments(tracks, ink.Width, maxJump);
            List<List<WpfPoint>> detected = SelectUsefulTracks(
                tracks, ink.Width, configuredMinimumLength);

            if (!transposeResult)
                return detected;

            return detected
                .Select(series => series
                    .Select(point => new WpfPoint(point.Y, point.X))
                    .ToList())
                .ToList();
        }

        private static Mat ToBgr(Mat source)
        {
            var result = new Mat();
            if (source.Channels() == 1)
                Cv2.CvtColor(source, result, ColorConversionCodes.GRAY2BGR);
            else if (source.Channels() == 4)
                Cv2.CvtColor(source, result, ColorConversionCodes.BGRA2BGR);
            else
                source.CopyTo(result);
            return result;
        }

        private static void RemoveLongGridLines(Mat ink)
        {
            // Only remove near full-span lines. A shorter kernel would also erase
            // legitimate flat portions of characteristic curves.
            int horizontalLength = Math.Min(ink.Width, Math.Max(25, (int)(ink.Width * 0.65)));
            int verticalLength = Math.Min(ink.Height, Math.Max(25, (int)(ink.Height * 0.65)));

            using var horizontalKernel = Cv2.GetStructuringElement(
                MorphShapes.Rect, new OpenCvSharp.Size(horizontalLength, 1));
            using var verticalKernel = Cv2.GetStructuringElement(
                MorphShapes.Rect, new OpenCvSharp.Size(1, verticalLength));
            using var horizontal = new Mat();
            using var vertical = new Mat();
            using var grid = new Mat();

            Cv2.MorphologyEx(ink, horizontal, MorphTypes.Open, horizontalKernel);
            Cv2.MorphologyEx(ink, vertical, MorphTypes.Open, verticalKernel);
            Cv2.BitwiseOr(horizontal, vertical, grid);
            Cv2.Subtract(ink, grid, ink);
        }

        private static void RemovePlotFrame(Mat ink)
        {
            int border = Math.Min(3, Math.Min(ink.Width, ink.Height) / 3);
            if (border <= 0)
                return;

            using var top = ink.RowRange(0, border);
            using var bottom = ink.RowRange(ink.Height - border, ink.Height);
            using var left = ink.ColRange(0, border);
            using var right = ink.ColRange(ink.Width - border, ink.Width);
            top.SetTo(Scalar.Black);
            bottom.SetTo(Scalar.Black);
            left.SetTo(Scalar.Black);
            right.SetTo(Scalar.Black);
        }

        private static List<Track> TrackCandidates(Mat ink, int maxJump)
        {
            var tracks = new List<Track>();

            for (int x = 0; x < ink.Width; x++)
            {
                List<double> candidates = FindColumnCentres(ink, x);
                var claimed = new bool[candidates.Count];

                foreach (Track track in tracks.Where(t => t.MissedColumns <= MaximumGapColumns))
                {
                    double predictedY = track.LastY + track.Slope * (track.MissedColumns + 1);
                    int bestIndex = -1;
                    double bestDistance = double.MaxValue;

                    for (int i = 0; i < candidates.Count; i++)
                    {
                        double distance = Math.Abs(candidates[i] - predictedY);
                        double allowedDistance = maxJump + track.MissedColumns * 1.5;
                        if (distance < bestDistance && distance <= allowedDistance)
                        {
                            bestDistance = distance;
                            bestIndex = i;
                        }
                    }

                    if (bestIndex >= 0)
                    {
                        track.Add(x, candidates[bestIndex]);
                        claimed[bestIndex] = true;
                    }
                    else
                    {
                        track.MissedColumns++;
                    }
                }

                for (int i = 0; i < candidates.Count; i++)
                {
                    if (!claimed[i])
                        tracks.Add(new Track(x, candidates[i]));
                }
            }

            return tracks;
        }

        private static void MergeCompatibleFragments(List<Track> tracks, int imageWidth, int maxJump)
        {
            int maximumMergeGap = Math.Max(12, (int)(imageWidth * 0.12));
            var ordered = tracks
                .Where(track => track.HorizontalSpan >= 5)
                .OrderBy(track => track.FirstX)
                .ToList();

            foreach (Track first in ordered)
            {
                if (first.IsMerged)
                    continue;

                Track? best = null;
                double bestScore = double.MaxValue;

                foreach (Track second in ordered)
                {
                    if (ReferenceEquals(first, second) || second.IsMerged || second.FirstX <= first.LastX)
                        continue;

                    int gap = second.FirstX - first.LastX - 1;
                    if (gap > maximumMergeGap)
                        break;

                    double predictedY = first.LastY + first.EndSlope * (gap + 1);
                    double yError = Math.Abs(second.FirstY - predictedY);
                    double slopeError = Math.Abs(second.StartSlope - first.EndSlope);
                    double allowedYError = Math.Max(5, Math.Min(maxJump * 1.5, gap * 0.12 + 3));

                    if (yError > allowedYError || slopeError > 0.55)
                        continue;

                    double score = yError + slopeError * 8 + gap * 0.02;
                    if (score < bestScore)
                    {
                        best = second;
                        bestScore = score;
                    }
                }

                if (best != null)
                {
                    first.Append(best);
                    best.IsMerged = true;
                }
            }

            tracks.RemoveAll(track => track.IsMerged);
        }

        private static List<double> FindColumnCentres(Mat ink, int x)
        {
            var centres = new List<double>();
            int y = 0;

            while (y < ink.Height)
            {
                while (y < ink.Height && ink.At<byte>(y, x) == 0)
                    y++;
                if (y >= ink.Height)
                    break;

                int start = y;
                while (y < ink.Height && ink.At<byte>(y, x) != 0)
                    y++;
                centres.Add((start + y - 1) / 2.0);
            }

            return centres;
        }

        private static List<List<WpfPoint>> SelectUsefulTracks(
            List<Track> tracks,
            int imageWidth,
            int configuredMinimumLength)
        {
            int minimumSpan = Math.Max(configuredMinimumLength, (int)Math.Ceiling(imageWidth * 0.20));
            var accepted = new List<List<WpfPoint>>();

            foreach (Track track in tracks
                .Where(t => t.HorizontalSpan >= minimumSpan)
                .Where(t => t.Points.Count >= t.HorizontalSpan * 0.45)
                .OrderByDescending(t => t.HorizontalSpan)
                .ThenByDescending(t => t.Points.Count))
            {
                List<WpfPoint> smoothed = Smooth(track.Points);
                if (accepted.Any(existing => IsDuplicate(existing, smoothed)))
                    continue;

                accepted.Add(smoothed);
                if (accepted.Count >= MaximumSeriesCount)
                    break;
            }

            return accepted.OrderBy(series => series.Average(p => p.Y)).ToList();
        }

        private static List<WpfPoint> Smooth(List<WpfPoint> points)
        {
            if (points.Count < 3)
                return points.ToList();

            var result = new List<WpfPoint>(points.Count) { points[0] };
            for (int i = 1; i < points.Count - 1; i++)
            {
                double y = (points[i - 1].Y + 2 * points[i].Y + points[i + 1].Y) / 4.0;
                result.Add(new WpfPoint(points[i].X, y));
            }
            result.Add(points[^1]);
            return result;
        }

        private static bool IsDuplicate(List<WpfPoint> first, List<WpfPoint> second)
        {
            if (first.Count == 0 || second.Count == 0)
                return false;

            const double maximumDistance = 3.0;
            double firstCoverage = CalculateNearPointCoverage(first, second, maximumDistance);
            double secondCoverage = CalculateNearPointCoverage(second, first, maximumDistance);
            return Math.Min(firstCoverage, secondCoverage) >= 0.55;
        }

        private static double CalculateNearPointCoverage(
            List<WpfPoint> source,
            List<WpfPoint> target,
            double maximumDistance)
        {
            double maximumDistanceSquared = maximumDistance * maximumDistance;
            int step = Math.Max(1, source.Count / 250);
            int sampled = 0;
            int near = 0;

            for (int i = 0; i < source.Count; i += step)
            {
                WpfPoint point = source[i];
                sampled++;
                if (target.Any(other =>
                    (other.X - point.X) * (other.X - point.X) +
                    (other.Y - point.Y) * (other.Y - point.Y) <= maximumDistanceSquared))
                {
                    near++;
                }
            }

            return sampled == 0 ? 0 : (double)near / sampled;
        }

        private static List<List<WpfPoint>> MergeDirectionalCandidates(
            List<List<WpfPoint>> horizontalCandidates,
            List<List<WpfPoint>> verticalCandidates,
            Mat ink,
            int maximumCount)
        {
            var ranked = horizontalCandidates
                .Concat(verticalCandidates)
                .Select(points => new
                {
                    Points = points,
                    Score = ScoreCandidate(points, ink)
                })
                .OrderByDescending(candidate => candidate.Score)
                .ToList();

            var accepted = new List<List<WpfPoint>>();
            foreach (var candidate in ranked)
            {
                if (accepted.Any(existing => IsDuplicate(existing, candidate.Points)))
                    continue;

                accepted.Add(candidate.Points);
                if (accepted.Count >= maximumCount)
                    break;
            }

            // Keep the established top-to-bottom presentation order. Points in a
            // vertical candidate intentionally remain ordered along Y; sorting
            // them by X here would destroy its path through near-vertical parts.
            return accepted.OrderBy(series => series.Average(point => point.Y)).ToList();
        }

        private static double ScoreCandidate(List<WpfPoint> points, Mat ink)
        {
            if (points.Count == 0)
                return 0;

            double arcLength = 0;
            int supported = 0;
            for (int i = 0; i < points.Count; i++)
            {
                WpfPoint point = points[i];
                if (i > 0)
                {
                    WpfPoint previous = points[i - 1];
                    arcLength += Math.Sqrt(
                        (point.X - previous.X) * (point.X - previous.X) +
                        (point.Y - previous.Y) * (point.Y - previous.Y));
                }

                int x = Math.Clamp((int)Math.Round(point.X), 0, ink.Width - 1);
                int y = Math.Clamp((int)Math.Round(point.Y), 0, ink.Height - 1);
                if (ink.At<byte>(y, x) != 0)
                    supported++;
            }

            double supportRatio = (double)supported / points.Count;
            return arcLength * (0.5 + supportRatio) + points.Count * 0.25;
        }

        private sealed class Track
        {
            public List<WpfPoint> Points { get; } = new();
            public double LastY { get; private set; }
            public double Slope { get; private set; }
            public int MissedColumns { get; set; }
            public bool IsMerged { get; set; }
            public int FirstX => Points.Count == 0 ? 0 : (int)Points[0].X;
            public int LastX => Points.Count == 0 ? 0 : (int)Points[^1].X;
            public double FirstY => Points.Count == 0 ? 0 : Points[0].Y;
            public double EndSlope => CalculateSlope(fromStart: false);
            public double StartSlope => CalculateSlope(fromStart: true);
            public int HorizontalSpan => Points.Count == 0
                ? 0
                : (int)(Points[^1].X - Points[0].X + 1);

            public Track(int x, double y)
            {
                Points.Add(new WpfPoint(x, y));
                LastY = y;
            }

            public void Add(int x, double y)
            {
                WpfPoint previous = Points[^1];
                double dx = Math.Max(1, x - previous.X);
                double measuredSlope = (y - previous.Y) / dx;
                Slope = Points.Count == 1
                    ? measuredSlope
                    : Slope * 0.7 + measuredSlope * 0.3;
                Points.Add(new WpfPoint(x, y));
                LastY = y;
                MissedColumns = 0;
            }

            public void Append(Track continuation)
            {
                Points.AddRange(continuation.Points);
                LastY = Points[^1].Y;
                Slope = EndSlope;
            }

            private double CalculateSlope(bool fromStart)
            {
                if (Points.Count < 2)
                    return 0;

                int sampleCount = Math.Min(8, Points.Count);
                WpfPoint first = fromStart ? Points[0] : Points[^sampleCount];
                WpfPoint last = fromStart ? Points[sampleCount - 1] : Points[^1];
                double dx = last.X - first.X;
                return Math.Abs(dx) < 1e-9 ? 0 : (last.Y - first.Y) / dx;
            }
        }
    }
}
