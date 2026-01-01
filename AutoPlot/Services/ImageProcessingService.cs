using AutoPlot.ImageProcessing;
using AutoPlot.Models;
using OpenCvSharp;
using System.Windows;

namespace AutoPlot.Services
{
    public class ImageProcessingService
    {
        private readonly ImageProcessor _processor = new();

        public CurveData Run(Mat inputImage,
                             double xMin, double xMax,
                             double yMin, double yMax,
                             string xScale, string yScale)
        {
            using var inputImage_clone = inputImage.Clone();
            return _processor.Process(inputImage_clone, xMin, xMax, yMin, yMax, xScale, yScale);
        }

        
        public Mat CreateRoiHighlightImage(Mat src, OpenCvSharp.Rect roi)
        {
            Mat baseImage = new();

            if (src.Channels() == 1)
            {
                Cv2.CvtColor(src, baseImage, ColorConversionCodes.GRAY2BGR);
            }
            else
            {
                baseImage = src.Clone(); // 3ch / 4ch はそのまま使う
            }


            // 全体を暗くする
            Mat dimmed = new();
            baseImage.ConvertTo(dimmed, -1, 0.5, 0);

            // ROI部分だけ元画像をコピー
            src[roi].CopyTo(dimmed[roi]);

            // ROI枠を描画（分かりやすく）
            Cv2.Rectangle(
                dimmed,
                roi,
                new Scalar(0, 255, 0),
                2
            );

            return dimmed;
        }
        /// <summary>
        /// ノイズマスクで指定された領域を白ピクセル化する
        /// </summary>
        public Mat ApplyNoiseMask(Mat plotArea, Mat noiseMask)
        {
            if (plotArea == null)
                throw new ArgumentNullException(nameof(plotArea));

            if (noiseMask == null)
                throw new ArgumentNullException(nameof(noiseMask));

            if (plotArea.Size() != noiseMask.Size())
                throw new ArgumentException("plotArea と noiseMask のサイズが一致していません");

            // 元画像を壊さないように clone
            Mat result = plotArea.Clone();

            // マスク部分を白にする
            result.SetTo(Scalar.White, noiseMask); 

            return result;
        }
    }
}
