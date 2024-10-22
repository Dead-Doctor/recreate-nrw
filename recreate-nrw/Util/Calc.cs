using JetBrains.Annotations;
using OpenTK.Mathematics;

namespace recreate_nrw.Util;

public static class Calc
{
    [PublicAPI]
    public static System.Numerics.Vector2 ToSystem(this Vector2 v) => new(v.X, v.Y);
    
    [PublicAPI]
    public static System.Numerics.Vector2 ToSystem(this Vector2i v) => new(v.X, v.Y);
    
    [PublicAPI]
    public static System.Numerics.Vector3 ToSystem(this Vector3 v) => new(v.X, v.Y, v.Z);
    
    [PublicAPI]
    public static System.Numerics.Vector3 ToSystem(this Vector3i v) => new(v.X, v.Y, v.Z);
    
    [PublicAPI]
    public static Vector2 ToVector2(this System.Numerics.Vector2 v) => new(v.X, v.Y);
    
    // ReSharper disable once InconsistentNaming
    [PublicAPI]
    public static Vector2i ToVector2i(this System.Numerics.Vector2 v) => new((int)v.X, (int)v.Y);
    
    [PublicAPI]
    public static Vector3 ToVector3(this System.Numerics.Vector3 v) => new(v.X, v.Y, v.Z);
    
    // ReSharper disable once InconsistentNaming
    [PublicAPI]
    public static Vector3i ToVector3i(this System.Numerics.Vector3 v) => new((int)v.X, (int)v.Y, (int)v.Z);
    
    [PublicAPI]
    public static Vector2i FloorToInt(this Vector2 vec) => new((int) Math.Floor(vec.X), (int) Math.Floor(vec.Y));

    [PublicAPI]
    public static Vector3i FloorToInt(this Vector3 vec) =>
        new((int) Math.Floor(vec.X), (int) Math.Floor(vec.Y), (int) Math.Floor(vec.Z));

    [PublicAPI]
    public static int Modulo(this int value, int step) => (value % step + step) % step;

    [PublicAPI]
    public static float Modulo(this float value, float step) => (value % step + step) % step;

    [PublicAPI]
    public static Vector2i Modulo(this Vector2i value, int step) =>
        new(value.X.Modulo(step), value.Y.Modulo(step));

    [PublicAPI]
    public static Vector2 Modulo(this Vector2 value, float step) =>
        new(value.X.Modulo(step), value.Y.Modulo(step));

    [PublicAPI]
    public static Vector3i Modulo(this Vector3i value, int step) =>
        new(value.X.Modulo(step), value.Y.Modulo(step), value.Z.Modulo(step));

    [PublicAPI]
    public static Vector3 Modulo(this Vector3 value, float step) =>
        new(value.X.Modulo(step), value.Y.Modulo(step), value.Z.Modulo(step));
    
    [PublicAPI]
    public static string FormatDuration(this TimeSpan duration) =>
        duration.TotalSeconds < 1.0 ? $"{duration.TotalMilliseconds:N0}ms"
        : duration.TotalMinutes < 1.0 ? $"{duration.TotalSeconds:N1}s"
        : duration.TotalHours < 1.0 ? $"{duration.TotalMinutes:N1}min"
        : duration.TotalDays < 1.0 ? $"{duration.TotalHours:N1}min"
        : $"{duration.TotalDays:N1}min";
}