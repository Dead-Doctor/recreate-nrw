using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using recreate_nrw.Ground;
using recreate_nrw.Util;

namespace recreate_nrw.Render.UI;

public class Map
{
    private static readonly Shader Shader = new("map");
    private static readonly ShadedModel ShadedModel;
    
    private readonly Framebuffer _framebuffer = new(new Vector2i(200, 150), true, true, false);
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
        Shader.AddUniform("countDataTiles", TerrainData.AvailableData.Count);
        Shader.AddTexture("dataTilesTexture", TerrainData.AvailableDataTilesTexture);
        ShadedModel = new ShadedModel(model, Shader, BufferUsageAccessFrequency.Static, BufferUsageAccessNature.Draw);
    }
    
    public Map()
    {
        Zoom = TargetZoom;
    }

    public void Update(Camera camera)
    {
        if (_followPlayer) _position = camera.Position.Xz;
        
        Shader.SetUniform("playerPosition", camera.Position.Xz);
        Shader.SetUniform("playerDirection", camera.Yaw);
    }

    private const float ZoomMin = -2f;
    private const float ZoomMax = 5f;
    private const float ZoomEpsilon = 0.001f;
    private float _zoom;
    private float _targetZoom;

    private float TargetZoom
    {
        get => _targetZoom;
        set => _targetZoom = Math.Clamp(value, ZoomMin, ZoomMax);
    }
    
    private float Zoom
    {
        get => _zoom;
        set
        {
            _zoom = value;
            _size = Coordinate.TerrainTileSize * (float)Math.Pow(2, -value);
        }
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
            Shader.SetUniform("frameHeight", (float)_framebuffer.Size.Y);
            Shader.SetUniform("aspectRatio", (float)_framebuffer.Size.X / _framebuffer.Size.Y);
            Shader.SetUniform("position", _position);
            Shader.SetUniform("size", _size);
            ShadedModel.Draw();
        });
    }

    public void Window(float delta)
    {
        Redraw();
        ImGui.Begin("Map");

        var nextSize = ImGui.GetContentRegionAvail() - ImGui.GetStyle().FramePadding * 2 - new System.Numerics.Vector2(0, (int)ImGui.GetFrameHeightWithSpacing());
        _framebuffer.Size = new Vector2i((int)Math.Max(nextSize.X, 10.0), (int)Math.Max(nextSize.Y, 10.0));
        
        ImGui.ImageButton((IntPtr)_framebuffer.GetHandle(), _framebuffer.Size.ToSystem());
        if (ImGui.IsItemHovered())
        {
            var scroll = ImGui.GetIO().MouseWheel;
            if (scroll != 0f) TargetZoom += scroll;
        }
        
        var difference = TargetZoom - Zoom;
        if (Math.Abs(difference) > ZoomEpsilon) 
        {
            Zoom += difference * delta * 3f;
        }
        
        if (ImGui.Button("Locate"))
            _followPlayer = true;
        ImGui.SameLine();
        if (ImGuiExtension.Vector2("Position", ref _position))
            _followPlayer = false;
        
        ImGui.End();
    }
}