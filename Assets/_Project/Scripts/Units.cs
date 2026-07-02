#nullable enable
namespace Mehawar.Greybox
{
    /// <summary>
    /// Project-wide pixel/unit conversion (Pixel Perfect Camera reference: PPU = 16).
    /// Tunables are authored in pixels to match the design docs and converted once here,
    /// so every actor shares the same constant instead of a private copy.
    /// </summary>
    public static class Units
    {
        public const float PixelsPerUnit = 16f;

        /// <summary>Convert a pixel value (or px/s) to Unity units (or u/s).</summary>
        public static float PxToUnits(float pixels) => pixels / PixelsPerUnit;
    }
}
