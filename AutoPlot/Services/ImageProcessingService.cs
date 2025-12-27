using AutoPlot.ImageProcessing;
using AutoPlot.Models;

namespace AutoPlot.Services
{
    public class ImageProcessingService
    {
        private readonly ImageProcessor _processor = new();

        public CurveData Run(string path,
                             double xMin, double xMax,
                             double yMin, double yMax,
                             string xScale, string yScale)
        {
            return _processor.Process(path, xMin, xMax, yMin, yMax, xScale, yScale);
        }
    }
}
