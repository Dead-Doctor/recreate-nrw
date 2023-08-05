using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using recreate_nrw.Render;
using recreate_nrw.Util;
using Buffer = System.Buffer;

namespace recreate_nrw.Terrain;

public class Terrain : IDisposable
{
    private const int TileSize = Coordinate.TerrainTileSize;

    //TODO: load tiles
    private readonly TerrainData _data = new();

    private int _n = 32;
    private int _renderDistance;
    private int _LODs;
    private int _chunks;

    private readonly Shader _shader;
    private Model? _model;
    private ShadedModel? _shadedModel;

    //TODO: information graph

    public int N
    {
        get => _n;
        set
        {
            _n = Math.Max(value, 8);
            RenderDistance = _renderDistance;
            GenerateModel();
            _shader.SetUniform("n", N);
        }
    }

    public int RenderDistance
    {
        get => _renderDistance;
        set
        {
            _renderDistance = Math.Max(value, 2 * N);
            _LODs = (int) Math.Ceiling(Math.Log2((float) _renderDistance / N));
            _chunks = 4 * (1 + 3 * _LODs);
        }
    }

    public readonly Tile Tile00;

    public Terrain()
    {
        RenderDistance = 512;

        Tile00 = _data.GetTile(new Vector2i(0, 0));

        _shader = new Shader("Shaders/terrain.vert", "Shaders/terrain.frag");
        _shader.AddUniform<Vector2>("modelPos");
        _shader.AddUniform<Matrix4>("viewMat");
        _shader.AddUniform<Matrix4>("projectionMat");
        _shader.AddUniform("n", N);
        _shader.AddUniform("lightDir", new Vector3(1.0f, -1.0f, -1.0f));

        var data = new byte[TileSize * TileSize * 4];
        Buffer.BlockCopy(Tile00.Data, 0, data, 0, data.Length);
        
        _shader.AddTexture("tile00",
            Texture.Load("tile:tile00",
                _ => new TextureData(data, TileSize, TileSize, PixelFormat.RedInteger, PixelType.Int,
                    SizedInternalFormat.R32i, TextureWrapMode.Repeat, true, false)));

        GenerateModel();
    }

    private void GenerateModel()
    {
        var vertexCount = N + 1;

        var vertices = new float[vertexCount * vertexCount * 2];
        var indices = new uint[N * N * 2 * 3];

        for (var z = 0; z < vertexCount; z++)
        {
            for (var x = 0; x < vertexCount; x++)
            {
                vertices[(z * vertexCount + x) * 2 + 0] = x;
                vertices[(z * vertexCount + x) * 2 + 1] = z;
            }
        }

        for (var z = 0; z < N; z++)
        {
            for (var x = 0; x < N; x++)
            {
                var topLeft = (uint) (z * vertexCount + x);
                var topRight = (uint) (z * vertexCount + x + 1);
                var bottomLeft = (uint) ((z + 1) * vertexCount + x);
                var bottomRight = (uint) ((z + 1) * vertexCount + x + 1);

                {
                    var i = ((z * N + x) * 2 + 0) * 3;
                    indices[i + 0] = topLeft;
                    indices[i + 1] = topRight;
                    indices[i + 2] = bottomRight;
                }
                {
                    var i = ((z * N + x) * 2 + 1) * 3;
                    indices[i + 0] = topLeft;
                    indices[i + 1] = bottomRight;
                    indices[i + 2] = bottomLeft;
                }
            }
        }

        _model = Model.FromArray(vertices, indices);
        _model.AddVertexAttribute(new VertexAttribute("aPosition", VertexAttribType.Float, 2));
        _shadedModel?.Dispose();
        _shadedModel = new ShadedModel(_model, _shader);
    }

    private float _left = -1.0f;
    private float _right = +1.0f;
    private float _top = -1.0f;
    private float _bottom = +1.0f;

    private Vector2 _tile0 = new(0.0f, 0.0f);
    private Vector2 _tile1 = new(-1.0f, 0.0f);
    private Vector2 _tile2 = new(0.0f, -1.0f);
    private Vector2 _tile3 = new(-1.0f, -1.0f);

    public void Draw(Camera camera)
    {
        var xTileSpace = camera.Position.X / TileSize;
        var zTileSpace = camera.Position.Z / TileSize;

        if (xTileSpace < _left + 0.375) Update(xTileSpace, zTileSpace);
        else if (xTileSpace > _right - 0.375) Update(xTileSpace, zTileSpace);

        if (zTileSpace < _top + 0.375) Update(xTileSpace, zTileSpace);
        else if (zTileSpace > _bottom - 0.375) Update(xTileSpace, zTileSpace);

        //TODO: snap correctly
        var offset = new Vector3(0.0f, 0.0f, 0.0f);
        var eye = new Vector3(-offset.X, camera.Position.Y, -offset.Z);
        var viewMat = Matrix4.LookAt(eye, eye + camera.Front, camera.Up);
        var modelPos = new Vector2(camera.Position.X, camera.Position.Z) + offset.Xz;
        _shader.SetUniform("modelPos", modelPos);
        _shader.SetUniform("viewMat", viewMat);
        _shader.SetUniform("projectionMat", camera.ProjectionMat);

        _shadedModel!.DrawInstanced(_chunks);
    }

    private void Update(float x, float z)
    {
        var tileXLower = (float) Math.Floor(x - 0.5f);
        var tileZLower = (float) Math.Floor(z - 0.5f);
        var tileXUpper = tileXLower + 1.0f;
        var tileZUpper = tileZLower + 1.0f;

        _left = tileXLower;
        _right = tileXUpper + 1.0f;
        _top = tileZLower;
        _bottom = tileZUpper + 1.0f;

        Load(new Vector2(tileXLower, tileZLower));
        Load(new Vector2(tileXUpper, tileZLower));
        Load(new Vector2(tileXUpper, tileZUpper));
        Load(new Vector2(tileXLower, tileZUpper));
    }

    private void Load(Vector2 tile)
    {
        var index = tile.Y % 2.0 * 2.0 + tile.X % 2.0;
        switch (index)
        {
            case 0:
                _tile0 = tile;
                break;
            case 1:
                _tile1 = tile;
                break;
            case 2:
                _tile2 = tile;
                break;
            case 3:
                _tile3 = tile;
                break;
        }
    }

    public void Window()
    {
        ImGui.Begin("Terrain");

        ImGui.PushItemWidth(100.0f);
        var cachedRenderDistance = RenderDistance;
        if (ImGui.DragInt("Render Distance", ref cachedRenderDistance, 1.0f, 2 * N, 2048))
            RenderDistance = cachedRenderDistance;
        var cachedN = N;
        if (ImGui.InputInt("N", ref cachedN, 8, 32, ImGuiInputTextFlags.EnterReturnsTrue))
            N = cachedN;
        ImGui.PopItemWidth();

        var actualRenderDistance = N * (int) Math.Pow(2.0, _LODs);
        ImGui.Text($"Actual: {actualRenderDistance}m ({_LODs} LODs)");
        ImGui.Value("Highest Quality", 2 * N, "%.0fm");
        ImGui.Text($"Chunks: {_chunks} (Instanced)");
        var triangleCount = _chunks * N * N * 2;
        var naiveTriangleCount = actualRenderDistance * actualRenderDistance * 4 * 2;
        ImGui.Text(
            $"Total Triangles: {triangleCount / 1000}K/{naiveTriangleCount / 1000}K ({(int) ((float) triangleCount / naiveTriangleCount * 100.0f)}%%)");

        ImGui.Text($"X: {_left} - {_right}");
        ImGui.Text($"Z: {_top} - {_bottom}");

        ImGui.Text($"LoadData: {Profiler.FormatDuration(_data.total / _data.count)} ({_data.count}x)");
        
        _data.Profiler?.ImGuiTree();
        
        ImGui.End();
    }

    private bool _disposedValue;

    public void Dispose()
    {
        if (_disposedValue) return;
        GC.SuppressFinalize(this);

        _shadedModel?.Dispose();
        _shader.Dispose();
        _disposedValue = true;
    }

    // Finalizer may not be called at all
    ~Terrain()
    {
        if (_disposedValue) return;
        Console.WriteLine("GPU Resource leak! Did you forget to call Dispose() on Terrain?");
    }
}