using System.Collections.Generic;
using OpenCvSharp;

namespace AutoPlot.Models
{
    public class CurveData
    {
        public List<ImagePoint> Points { get; set; } = new();
        public Rect PlotRoi { get; set; }   // ← 追加
    }
}
