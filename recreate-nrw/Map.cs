using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using recreate_nrw.Render;
using recreate_nrw.Render.UI;
using recreate_nrw.Util;

namespace recreate_nrw;

public class Map
{
    private static readonly Shader Shader = new("map");
    private static readonly ShadedModel ShadedModel;
    
    private readonly Framebuffer _framebuffer = new(200, 150, true, true, false);
    private bool _followPlayer = true;
    private Vector2 _position = Vector2.Zero;
    private float _size;

    static Map()
    {
        var model = Model.FromArray(new[] {
            -1f, -1f,
            1f, -1f,
            1f, 1f,
            -1f, 1f,
        }, new[] {
            0u, 1u, 2u,
            0u, 2u, 3u
        });
        model.AddVertexAttribute(new VertexAttribute("aPos", VertexAttribType.Float, 2));
        
        Shader.AddUniform<float>("frameHeight");
        Shader.AddUniform<float>("aspectRatio");
        Shader.AddUniform<Vector2>("position");
        Shader.AddUniform<float>("size");
        Shader.AddUniform<Vector2>("playerPosition");
        Shader.AddUniform<float>("playerDirection");
        ShadedModel = new ShadedModel(model, Shader, BufferUsageAccessFrequency.Static, BufferUsageAccessNature.Draw);
    }
    
    public Map()
    {
        Zoom = 0f;
    }

    public void Update(Camera camera)
    {
        if (_followPlayer) _position = camera.Position.Xz;
        
        Shader.SetUniform("playerPosition", camera.Position.Xz);
        Shader.SetUniform("playerDirection", camera.Yaw);
    }

    private float Zoom
    {
        get => (float)-Math.Log2(_size / Coordinate.TerrainTileSize);
        set => _size = Coordinate.TerrainTileSize * (float)Math.Pow(2, -value);
    }

    // private Vector2 Project(Vector2 worldPosition)
    // {
    //     var offset = worldPosition - _position;
    //     return offset / new Vector2(_size / _framebuffer.Height * _framebuffer.Width, _size);
    // }

    private void Redraw()
    {
        _framebuffer.Use(() =>
        {
            Shader.SetUniform("frameHeight", (float)_framebuffer.Height);
            Shader.SetUniform("aspectRatio", (float)_framebuffer.Width / _framebuffer.Height);
            Shader.SetUniform("position", _position);
            Shader.SetUniform("size", _size);
            ShadedModel.Draw();
        });
    }

    public void Window()
    {
        Redraw();
        ImGui.Begin("Map");

        var contentRegionAvail = ImGui.GetContentRegionAvail();
        var newWidth = Math.Max((int)contentRegionAvail.X, 20);
        if (newWidth != _framebuffer.Width)
            _framebuffer.Resize(newWidth, _framebuffer.Height);
        
        ImGui.Image((IntPtr)_framebuffer.GetHandle(), contentRegionAvail with { Y = _framebuffer.Height });
        if (ImGui.Button("Locate"))
            _followPlayer = true;
        ImGui.SameLine();
        if (ImGuiExtension.Vector2("Position", ref _position))
            _followPlayer = false;
        
        var cachedZoom = Zoom;
        if (ImGui.DragFloat("Zoom", ref cachedZoom, 0.1f))
            Zoom = cachedZoom;
        
        ImGui.End();
    }
}