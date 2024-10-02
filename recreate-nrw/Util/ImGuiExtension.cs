using System.Diagnostics;
using ImGuiNET;
using OpenTK.Mathematics;

namespace recreate_nrw.Util;

public static class ImGuiExtension
{
    public static void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo(url) {UseShellExecute = true});
    }

    public static bool Vector2(string label, ref Vector2 vector)
        => Vector2(label, vector, out vector);
    public static bool Vector2(string label, Vector2 value, out Vector2 newValue)
    {
        var vec = new System.Numerics.Vector2(value.X, value.Y);
        var result = ImGui.DragFloat2(label, ref vec);
        newValue = new Vector2(vec.X, vec.Y);
        return result;
    }

    public static bool Vector3(string label, Vector3 value, out Vector3 newValue)
    {
        var pos = new System.Numerics.Vector3(value.X, value.Y, value.Z);
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
}