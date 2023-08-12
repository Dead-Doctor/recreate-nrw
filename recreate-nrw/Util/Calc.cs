using JetBrains.Annotations;
using OpenTK.Mathematics;

namespace recreate_nrw.Util;

public static class Calc
{
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
}