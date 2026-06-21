namespace AutoPlot.Models
{
    public class ExtractionSettings
    {
        public int MovingAverageWindow { get; set; } = 5;
        public int CurveThreshold { get; set; } = 180;
        public int NoiseMaskThreshold { get; set; } = 180;
        public int TraceSearchBandWidth { get; set; } = 15;
        public int MinCurveLength { get; set; } = 20;
        public int OutlierRemovalThreshold { get; set; } = 0;

        public ExtractionSettings()
        {
        }

        public ExtractionSettings(ExtractionSettings source)
        {
            MovingAverageWindow = source.MovingAverageWindow;
            CurveThreshold = source.CurveThreshold;
            NoiseMaskThreshold = source.NoiseMaskThreshold;
            TraceSearchBandWidth = source.TraceSearchBandWidth;
            MinCurveLength = source.MinCurveLength;
            OutlierRemovalThreshold = source.OutlierRemovalThreshold;
        }
    }
}
