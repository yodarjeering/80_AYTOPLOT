namespace AutoPlot.Models
{
    public enum DisplayState
    {
        None,            // 初期状態（画像未ロードなど）
        Original,        // 原図表示
        AxisCalibrated,   // 軸キャリブレーション表示
        NoiseRemoval   // ノイズ除去モード
    }
}
