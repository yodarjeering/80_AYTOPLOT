using System;

namespace AutoPlot.ImageProcessing.Helpers
{
    // pixel座標をグラフ上の座標に変換する関数
    public static class PixelConverter
    {
        public static double[] PxToReal(
            double[] px, double pxMin, double pxMax,
            double vMin, double vMax, string scale = "linear", bool invert = false)
        {
            /*
                px       : pixel座標（配列OK）
                px_min   : pixel最小値
                px_max   : pixel最大値
                v_min    : 実値最小
                v_max    : 実値最大
                scale    : "linear" or "log"
                invert   : Trueなら軸方向を反転（例: y軸）
            */
            int n = px.Length;
            double[] result = new double[n];

            for (int i = 0; i < n; i++)
            {
                double t = (px[i] - pxMin) / (pxMax - pxMin);
                if (invert) t = 1 - t;

                if (scale == "log")
                {
                    if (vMin <= 0 || vMax <= 0)
                        throw new ArgumentException("log scale requires vMin > 0 and vMax > 0");

                    double logV = Math.Log10(vMin) + t * (Math.Log10(vMax) - Math.Log10(vMin));
                    result[i] = Math.Pow(10, logV);
                }
                else if (scale == "linear")
                {
                    result[i] = vMin + t * (vMax - vMin);
                }
                else
                {
                    throw new ArgumentException("scale must be 'linear' or 'log'");
                }
            }

            return result;
        }
    }
}
