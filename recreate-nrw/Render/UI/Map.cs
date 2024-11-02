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

    private static readonly Texture AvailableDataTilesTexture = StaticTexture.CreateFrom(
        new TextureInfo1D(SizedInternalFormat.Rg32f, TerrainData.AvailableData.Count),
        new TextureData1D(TerrainData.AvailableData.Select(v => v.ToVector2()).ToArray(), PixelFormat.Rg,
            PixelType.Float)
    );
    private static readonly StaticTexture TerrainTextureCenters = StaticTexture.CreateFrom(
        new TextureInfo1D(SizedInternalFormat.Rg32f, Terrain.TextureLODs)
    );

    private readonly Framebuffer _framebuffer = new(new Vector2i(200, 150), true, true, false);
    private readonly Camera _camera;
    private readonly Terrain _terrain;
    public bool FollowPlayer = true;
    private Vector2 _position = Vector2.Zero;
    private float _size;
    private Vector2 _dragDelta = Vector2.Zero;
    private Vector2 _clickPosition;

    static Map()
    {
        var model = Model.FromArray(new[]
        {
            -1f, -1f,
            1f, -1f,
            1f, 1f,
            -1f, 1f,
        }, new[]
        {
            0u, 1u, 2u,
            0u, 2u, 3u
        });
        model.AddVertexAttribute(new VertexAttribute("aPos", VertexAttribType.Float, 2));

        // var data = new float[AvailableData.Count * 2];
        // for (var i = 0; i < AvailableData.Count; i++)
        // {
        //     data[i * 2 + 0] = AvailableData[i].X;
        //     data[i * 2 + 1] = AvailableData[i].Y;
        // }

        Shader.AddUniform<float>("frameHeight");
        Shader.AddUniform<float>("aspectRatio");
        Shader.AddUniform<Vector2>("position");
        Shader.AddUniform<float>("size");
        Shader.AddUniform<Vector2>("playerPosition");
        Shader.AddUniform<float>("playerDirection");
        Shader.AddTexture("dataTilesTexture", AvailableDataTilesTexture);
        Shader.AddUniform<float>("baseTerrainTileSize", Coordinate.TerrainTileSize);
        Shader.AddTexture("terrainTextureCenters", TerrainTextureCenters);
        Shader.AddUniform("terrainTilesPerLod", Terrain.TextureLODSize);
        ShadedModel = new ShadedModel(model, Shader, BufferUsageAccessFrequency.Static, BufferUsageAccessNature.Draw);
    }

    public Map(Camera camera, Terrain terrain)
    {
        _camera = camera;
        _terrain = terrain;
        Zoom = TargetZoom;
    }

    private Vector2 Position => _position + _dragDelta;
    public bool Hovered { get; private set; }

    private const float ZoomMin = -4f;
    private const float ZoomMax = 2f;
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

    private void Redraw(Vector2 maxSize, Vector2 size, float deltaTime)
    {
        _framebuffer.Size = new Vector2i(
            (int)Math.Max(size.X == 0.0 ? maxSize.X : size.X, 10f),
            (int)Math.Max(size.Y == 0.0 ? maxSize.Y : size.Y, 10f)
        );

        var difference = TargetZoom - Zoom;
        if (Math.Abs(difference) > ZoomEpsilon)
        {
            Zoom += difference * deltaTime * 3f;
        }

        if (FollowPlayer) _position = _camera.Position.Xz;

        _framebuffer.Use(() =>
        {
            Shader.SetUniform("playerPosition", _camera.Position.Xz);
            Shader.SetUniform("playerDirection", _camera.Yaw);
            Shader.SetUniform("frameHeight", (float)_framebuffer.Size.Y);
            Shader.SetUniform("aspectRatio", (float)_framebuffer.Size.X / _framebuffer.Size.Y);
            Shader.SetUniform("position", Position);
            var centersData = new float[Terrain.TextureLODs * 2];
            for (var i = 0; i < Terrain.TextureLODs; i++)
            {
                centersData[i * 2 + 0] = _terrain.Center[i].X;
                centersData[i * 2 + 1] = _terrain.Center[i].Y;
            }

            TerrainTextureCenters.UploadImageData(new TextureData1D(centersData, PixelFormat.Rg, PixelType.Float));
            Shader.SetUniform("size", _size);
            ShadedModel.Draw();
        });
    }

    public void Window(Vector2 size)
    {
        var io = ImGui.GetIO();

        var maxSize = ImGui.GetContentRegionAvail().ToVector2() - ImGui.GetStyle().FramePadding.ToVector2() * 2;
        Redraw(maxSize, size, io.DeltaTime);

        var mousePos = (io.MousePos - ImGui.GetCursorScreenPos() - ImGui.GetStyle().FramePadding).ToVector2();
        var relativeMousePos = (mousePos * 2f - _framebuffer.Size.ToVector2()) / _framebuffer.Size.Y;
        var worldPosition = _position + relativeMousePos * _size;

        ImGui.ImageButton("mapImage", (IntPtr)_framebuffer.GetHandle(), _framebuffer.Size.ToSystem());
        Hovered = ImGui.IsItemHovered();
        if (Hovered)
        {
            var scroll = io.MouseWheel;
            if (scroll != 0f) TargetZoom += scroll;

            if (ImGui.IsMouseReleased(ImGuiMouseButton.Right))
            {
                _clickPosition = worldPosition;
                ImGui.OpenPopup("##clickMap");
            }
        }

        if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
        {
            _dragDelta = -ImGui.GetMouseDragDelta(ImGuiMouseButton.Left).ToVector2() * 2f / _framebuffer.Size.Y * _size;
            if (_dragDelta != Vector2.Zero) FollowPlayer = false;
        }

        if (ImGui.IsItemDeactivated())
        {
            _position += _dragDelta;
            _dragDelta = Vector2.Zero;
        }

        if (ImGui.BeginPopup("##clickMap"))
        {
            ImGui.Text($"X: {_clickPosition.X:0.0}, Y: {_clickPosition.Y:0.0}");
            if (ImGui.Button("Teleport"))
            {
                ImGui.CloseCurrentPopup();
                _camera.Position = new Vector3(_clickPosition.X, _camera.Position.Y, _clickPosition.Y);
            }

            ImGui.EndPopup();
        }
    }
}