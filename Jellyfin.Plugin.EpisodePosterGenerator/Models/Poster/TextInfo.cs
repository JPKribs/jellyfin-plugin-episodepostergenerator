using System;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Models
{
    /// <summary>
    /// Text rendering information for frame border calculations.
    /// </summary>
    public struct TextInfo : IEquatable<TextInfo>
    {
        /// <summary>
        /// Total height of the text block.
        /// </summary>
        public float Height { get; set; }

        /// <summary>
        /// Width of the widest text line.
        /// </summary>
        public float Width { get; set; }

        /// <summary>
        /// Horizontal center position of the text.
        /// </summary>
        public float CenterX { get; set; }

        /// <summary>
        /// Vertical position of the text.
        /// </summary>
        public float Y { get; set; }

        // MARK: Equals
        public readonly bool Equals(TextInfo other)
        {
            return Height == other.Height &&
                   Width == other.Width &&
                   CenterX == other.CenterX &&
                   Y == other.Y;
        }

        // MARK: Equals
        public override readonly bool Equals(object? obj)
        {
            return obj is TextInfo other && Equals(other);
        }

        // MARK: GetHashCode
        public override readonly int GetHashCode()
        {
            return HashCode.Combine(Height, Width, CenterX, Y);
        }

        // MARK: ==
        public static bool operator ==(TextInfo left, TextInfo right)
        {
            return left.Equals(right);
        }

        // MARK: !=
        public static bool operator !=(TextInfo left, TextInfo right)
        {
            return !left.Equals(right);
        }
    }
}