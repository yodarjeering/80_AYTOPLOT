using AutoPlot.Models;
using AutoPlot.Utils;
using System.Windows;
using System.Windows.Media;

namespace AutoPlot.Services
{
    public static class AppThemeManager
    {
        public static void Apply(AppTheme theme)
        {
            var palette = CreatePalette(theme);
            var resources = Application.Current.Resources;

            resources["AppBackgroundBrush"] = CreateBrush(palette.Background);
            resources["AppSurfaceBrush"] = CreateBrush(palette.Surface);
            resources["AppSurfaceAltBrush"] = CreateBrush(palette.SurfaceAlt);
            resources["AppBorderBrush"] = CreateBrush(palette.Border);
            resources["AppTextBrush"] = CreateBrush(palette.Text);
            resources["AppMutedTextBrush"] = CreateBrush(palette.MutedText);
            resources["AppPrimaryBrush"] = CreateBrush(palette.Primary);
            resources["AppSecondaryBrush"] = CreateBrush(palette.Secondary);
            resources["AppButtonForegroundBrush"] = CreateBrush(palette.ButtonText);
            resources["AppInputBackgroundBrush"] = CreateBrush(palette.InputBackground);

            PlotColors.ApplyTheme(theme);
        }

        private static ThemePalette CreatePalette(AppTheme theme)
        {
            return theme switch
            {
                AppTheme.Dark => new ThemePalette(
                    Color.FromRgb(0x0F, 0x12, 0x18),
                    Color.FromRgb(0x14, 0x18, 0x20),
                    Color.FromRgb(0x0C, 0x0F, 0x15),
                    Color.FromRgb(0x08, 0x0A, 0x0F),
                    Color.FromRgb(0xF1, 0xF5, 0xF9),
                    Color.FromRgb(0xA8, 0xB3, 0xC2),
                    Color.FromRgb(0x6D, 0xC8, 0xEC),
                    Color.FromRgb(0x5A, 0xD8, 0xA6),
                    Color.FromRgb(0x0F, 0x14, 0x1A),
                    Color.FromRgb(0x10, 0x14, 0x1C)),
                AppTheme.ChocoMint => new ThemePalette(
                    // Mint base with dark chocolate controls, inspired by the
                    // reference image. Slightly different mint values preserve
                    // panel and input boundaries without introducing gray.
                    Color.FromRgb(0x94, 0xE5, 0xD1),
                    Color.FromRgb(0xB8, 0xF0, 0xE2),
                    Color.FromRgb(0x82, 0xD8, 0xC3),
                    Color.FromRgb(0x58, 0x25, 0x0B),
                    Color.FromRgb(0x49, 0x1D, 0x08),
                    Color.FromRgb(0x75, 0x42, 0x27),
                    Color.FromRgb(0x58, 0x25, 0x0B),
                    Color.FromRgb(0x2D, 0x91, 0x7C),
                    Color.FromRgb(0xD9, 0xFA, 0xF1),
                    Color.FromRgb(0xD8, 0xF8, 0xEF)),
                _ => new ThemePalette(
                    Color.FromRgb(0xFA, 0xFA, 0xFA),
                    Colors.White,
                    Color.FromRgb(0xF5, 0xF5, 0xF5),
                    Color.FromRgb(0x80, 0x80, 0x80),
                    Color.FromRgb(0x21, 0x21, 0x21),
                    Color.FromRgb(0x66, 0x66, 0x66),
                    Color.FromRgb(0x67, 0x3A, 0xB7),
                    Color.FromRgb(0xCD, 0xDC, 0x39),
                    Colors.White,
                    Colors.White)
            };
        }

        private static SolidColorBrush CreateBrush(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        private sealed record ThemePalette(
            Color Background,
            Color Surface,
            Color SurfaceAlt,
            Color Border,
            Color Text,
            Color MutedText,
            Color Primary,
            Color Secondary,
            Color ButtonText,
            Color InputBackground);
    }
}
