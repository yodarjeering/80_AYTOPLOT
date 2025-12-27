using OpenCvSharp;
using System.Windows.Media.Imaging;

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
    
    }
    
    
}
