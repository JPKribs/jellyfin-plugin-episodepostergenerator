using System;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Models
{
    public struct TextInfo : IEquatable<TextInfo>
    {
        public float Height { get; set; }

        public float Width { get; set; }

        public float CenterX { get; set; }

        public float Y { get; set; }

        // Equals
        // Compares this TextInfo with another for value equality.
        public readonly bool Equals(TextInfo other)
        {
            return Height == other.Height &&
                   Width == other.Width &&
                   CenterX == other.CenterX &&
                   Y == other.Y;
        }

        // Equals
        // Compares this TextInfo with an object for value equality.
        public override readonly bool Equals(object? obj)
        {
            return obj is TextInfo other && Equals(other);
        }

        // GetHashCode
        // Returns a hash code based on the TextInfo properties.
        public override readonly int GetHashCode()
        {
            return HashCode.Combine(Height, Width, CenterX, Y);
        }

        public static bool operator ==(TextInfo left, TextInfo right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(TextInfo left, TextInfo right)
        {
            return !left.Equals(right);
        }
    }
}
