using System.Diagnostics;
using System.Globalization;
using ImGuiNET;
using OpenTK.Mathematics;

namespace recreate_nrw.Util;

public static class ImGuiExtension
{
    public static void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    public static bool Vector2(string label, ref Vector2 vector)
        => Vector2(label, vector, out vector);

    public static bool Vector2(string label, Vector2 value, out Vector2 newValue)
    {
        var vec = value.ToSystem();
        var result = ImGui.DragFloat2(label, ref vec);
        newValue = new Vector2(vec.X, vec.Y);
        return result;
    }

    public static bool Vector3(string label, Vector3 value, out Vector3 newValue)
    {
        var pos = value.ToSystem();
        var result = ImGui.DragFloat3(label, ref pos);
        newValue = new Vector3(pos.X, pos.Y, pos.Z);
        return result;
    }

    public static bool ColorEdit4(string label, ref Color4 value)
    {
        var color = new System.Numerics.Vector4(value.R, value.G, value.B, value.A);
        var result = ImGui.ColorEdit4(label, ref color);
        value = new Color4(color.X, color.Y, color.Z, color.W);
        return result;
    }

    public static System.Numerics.Vector4 Hsv(float hue, float saturation, float value)
    {
        ImGui.ColorConvertHSVtoRGB(hue, saturation, value, out var r, out var g, out var b);
        return new System.Numerics.Vector4(r, g, b, 1);
    }
    
    public static string FormatDuration(this TimeSpan duration) =>
        duration.TotalSeconds < 1.0 ? FormatValue(duration.TotalMilliseconds, "{0:N0}ms")
        : duration.TotalSeconds < 10.0 ? FormatValue(duration.TotalSeconds, "{0:N1}s")
        : duration.TotalMinutes < 1.0 ? FormatValue(duration.TotalSeconds, "{0:N0}s")
        : duration.TotalMinutes < 10.0 ? FormatValue(duration.TotalMinutes, "{0:N1}s")
        : duration.TotalHours < 1.0 ? FormatValue(duration.TotalMinutes, "{0:N0}min")
        : duration.TotalDays < 1.0 ? FormatValue(duration.TotalHours, "{0:N1}min")
        : FormatValue(duration.TotalDays, "{0:N1}min");

    private static string FormatValue(double value, string format) =>
        string.Format(CultureInfo.InvariantCulture, format, value);
    
    public static string FormatCount(this int count) =>
        count < 1_000 ? FormatValue(count, "{0:N0}")
        : count < 1_000_000 ? FormatValue(count / 1_000.0, "{0:N1}K")
        : count < 1_000_000_000 ? FormatValue(count / 1_000_000.0, "{0:N1}M")
        : FormatValue(count / 1_000_000_000.0, "{0:N1}G");
    
    public static string FormatDistance(this int distance) =>
        distance < 1_000 ? FormatValue(distance, "{0:N0}m")
        : distance < 10_000 ? FormatValue(distance/1_000.0, "{0:N1}km")
        : FormatValue(distance/1_000.0, "{0:N0}km");
    
    public static string FormatDistance(this float distance) =>
        distance < 10 ? FormatValue(distance, "{0:N1}m")
        : distance < 1_000 ? FormatValue(distance, "{0:N0}m")
        : distance < 10_000 ? FormatValue(distance/1_000.0, "{0:N1}km")
        : FormatValue(distance/1_000.0, "{0:N0}km");
    
    public static string FormatSize(this int bytes) =>
        bytes < 1_024 ? FormatValue(bytes, "{0:N0}B")
        : bytes < 10 * 1_024 ? FormatValue(bytes/1_024.0, "{0:N1}KB")
        : bytes < 1_024 * 1_024 ? FormatValue(bytes/1_024.0, "{0:N0}KB")
        : bytes < 10 * 1_024 * 1_024 ? FormatValue(bytes/(1_024.0*1_024.0), "{0:N1}MB")
        : bytes < 1_024 * 1_024 * 1_024 ? FormatValue(bytes/(1_024.0*1_024.0*1_024.0), "{0:N0}MB")
        : FormatValue(bytes/(1_024.0*1_024.0*1_024.0), "{0:N1}GB");
}