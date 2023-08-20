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

    public static bool ImGuiVector2(string label, Vector2 value, out Vector2 newValue)
    {
        var vec = new System.Numerics.Vector2(value.X, value.Y);
        var result = ImGui.DragFloat2(label, ref vec);
        newValue = new Vector2(vec.X, vec.Y);
        return result;
    }

    public static bool ImGuiVector3(string label, Vector3 value, out Vector3 newValue)
    {
        var pos = new System.Numerics.Vector3(value.X, value.Y, value.Z);
        var result = ImGui.DragFloat3(label, ref pos);
        newValue = new Vector3(pos.X, pos.Y, pos.Z);
        return result;
    }
}