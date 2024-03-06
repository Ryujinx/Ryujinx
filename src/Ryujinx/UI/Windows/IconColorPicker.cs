using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;

namespace Ryujinx.Ava.UI.Windows
{
    static class IconColorPicker
    {
        private const int ColorsPerLine = 64;
        private const int TotalColors = ColorsPerLine * ColorsPerLine;

        private const int UvQuantBits = 3;
        private const int UvQuantShift = BitsPerComponent - UvQuantBits;

        private const int SatQuantBits = 5;
        private const int SatQuantShift = BitsPerComponent - SatQuantBits;

        private const int BitsPerComponent = 8;

        private const int CutOffLuminosity = 64;

        private readonly struct PaletteColor
        {
            public int Qck { get; }
            public byte R { get; }
            public byte G { get; }
            public byte B { get; }

            public PaletteColor(int qck, byte r, byte g, byte b)
            {
                Qck = qck;
                R = r;
                G = g;
                B = b;
            }
        }

        public static Color GetFilteredColor(Image<Bgra32> image)
        {
            var color = GetColor(image).ToPixel<Bgra32>();

            // We don't want colors that are too dark.
            // If the color is too dark, make it brighter by reducing the range
            // and adding a constant color.
            int luminosity = GetColorApproximateLuminosity(color.R, color.G, color.B);
            if (luminosity < CutOffLuminosity)
            {
                color = Color.FromRgb(
                    (byte)Math.Min(CutOffLuminosity + color.R, byte.MaxValue),
                    (byte)Math.Min(CutOffLuminosity + color.G, byte.MaxValue),
                    (byte)Math.Min(CutOffLuminosity + color.B, byte.MaxValue));
            }

            return color;
        }

        public static Color GetColor(Image<Bgra32> image)
        {
            var colors = new PaletteColor[TotalColors];

            var dominantColorBin = new Dictionary<int, int>();

            var buffer = GetBuffer(image);

            int w = image.Width;

            int w8 = w << 8;
            int h8 = image.Height << 8;

#pragma warning disable IDE0059 // Unnecessary assignment
            int xStep = w8 / ColorsPerLine;
            int yStep = h8 / ColorsPerLine;
#pragma warning restore IDE0059

            int i = 0;
            int maxHitCount = 0;

            for (int y = 0; y < image.Height; y++)
            {
                int yOffset = y * image.Width;

                for (int x = 0; x < image.Width && i < TotalColors; x++)
                {
                    int offset = x + yOffset;

                    byte cb = buffer[offset].B;
                    byte cg = buffer[offset].G;
                    byte cr = buffer[offset].R;

                    var qck = GetQuantizedColorKey(cr, cg, cb);

                    if (dominantColorBin.TryGetValue(qck, out int hitCount))
                    {
                        dominantColorBin[qck] = hitCount + 1;

                        if (maxHitCount < hitCount)
                        {
                            maxHitCount = hitCount;
                        }
                    }
                    else
                    {
                        dominantColorBin.Add(qck, 1);
                    }

                    colors[i++] = new PaletteColor(qck, cr, cg, cb);
                }
            }

            int highScore = -1;
            PaletteColor bestCandidate = default;

            for (i = 0; i < TotalColors; i++)
            {
                var score = GetColorScore(dominantColorBin, maxHitCount, colors[i]);

                if (highScore < score)
                {
                    highScore = score;
                    bestCandidate = colors[i];
                }
            }

            return Color.FromRgb(bestCandidate.R, bestCandidate.G, bestCandidate.B);
        }

        public static Bgra32[] GetBuffer(Image<Bgra32> image)
        {
            return image.TryGetSinglePixelSpan(out var data) ? data.ToArray() : Array.Empty<Bgra32>();
        }

        private static int GetColorScore(Dictionary<int, int> dominantColorBin, int maxHitCount, PaletteColor color)
        {
            var hitCount = dominantColorBin[color.Qck];
            var balancedHitCount = BalanceHitCount(hitCount, maxHitCount);
            var quantSat = (GetColorSaturation(color) >> SatQuantShift) << SatQuantShift;
            var value = GetColorValue(color);

            // If the color is rarely used on the image,
            // then chances are that theres a better candidate, even if the saturation value
            // is high. By multiplying the saturation value with a weight, we can lower
            // it if the color is almost never used (hit count is low).
            var satWeighted = quantSat;
            var satWeight = balancedHitCount << 5;
            if (satWeight < 0x100)
            {
                satWeighted = (satWeighted * satWeight) >> 8;
            }

            // Compute score from saturation and dominance of the color.
            // We prefer more vivid colors over dominant ones, so give more weight to the saturation.
            var score = ((satWeighted << 1) + balancedHitCount) * value;

            return score;
        }

        private static int BalanceHitCount(int hitCount, int maxHitCount)
        {
            return (hitCount << 8) / maxHitCount;
        }

        private static int GetColorApproximateLuminosity(byte r, byte g, byte b)
        {
            return (r + g + b) / 3;
        }

        private static int GetColorSaturation(PaletteColor color)
        {
            int cMax = Math.Max(Math.Max(color.R, color.G), color.B);

            if (cMax == 0)
            {
                return 0;
            }

            int cMin = Math.Min(Math.Min(color.R, color.G), color.B);
            int delta = cMax - cMin;
            return (delta << 8) / cMax;
        }

        private static int GetColorValue(PaletteColor color)
        {
            return Math.Max(Math.Max(color.R, color.G), color.B);
        }

        private static int GetQuantizedColorKey(byte r, byte g, byte b)
        {
            int u = ((-38 * r - 74 * g + 112 * b + 128) >> 8) + 128;
            int v = ((112 * r - 94 * g - 18 * b + 128) >> 8) + 128;
            return (v >> UvQuantShift) | ((u >> UvQuantShift) << UvQuantBits);
        }
    }
}
