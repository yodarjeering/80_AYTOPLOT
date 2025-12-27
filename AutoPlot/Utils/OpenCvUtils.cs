using OpenCvSharp;

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
    }
}
