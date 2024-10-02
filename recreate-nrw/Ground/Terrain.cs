using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using recreate_nrw.Render;
using recreate_nrw.Util;
using Buffer = System.Buffer;

namespace recreate_nrw.Ground;

//TODO: fix schloss burg: 7.1496781, 51.1375123      370552.5815306349, 5666744.753800459      370.000, 5666.000

public class Terrain
{
    private const int TileSize = Coordinate.TerrainTileSize;

    //TODO: load tiles
    private readonly TerrainData _data = new();

    private int _n = 32;
    private int _renderDistance;
    // ReSharper disable once InconsistentNaming
    private int _LODs;
    private int _biggestSquares;
    private int _chunks;

    private readonly Shader _shader;
    private Model? _model;
    private ShadedModel? _shadedModel;

    //TODO: information graph

    private int N
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

    private int RenderDistance
    {
        get => _renderDistance;
        set
        {
            _renderDistance = Math.Max(value, 2 * N);
            _LODs = (int) Math.Ceiling(Math.Log2((float) _renderDistance / N));
            _biggestSquares = 1 << (_LODs - 1);
            if (_biggestSquares >= N) Console.WriteLine("[WARNING]: Step size is greater or equal to the distance of highest LOD.");
            _chunks = 4 * (1 + 3 * _LODs);
        }
    }
    
    private int _left;
    private int _right;
    private int _top;
    private int _bottom;
    private readonly LoadedTile?[] _loadedTiles = new LoadedTile?[4];

    private readonly List<Shader> _dependentShaders = new();

    public Terrain()
    {
        RenderDistance = 512;

        _shader = new Shader("terrain");
        _shader.AddUniform<Vector2>("modelPos");
        _shader.AddUniform<Matrix4>("viewMat");
        _shader.AddUniform<Matrix4>("projectionMat");
        _shader.AddUniform("n", N);
        _shader.AddUniform<Vector3>("sunDir");
        
        SwitchTiles(0.0f, 0.0f);
        AddDependentShader(_shader);
        
        GenerateModel();
    }

    public void AddDependentShader(Shader shader)
    {
        Console.WriteLine($"Marked shader '{shader}' as dependent.");
        _dependentShaders.Add(shader);
        for (var i = 0; i < _loadedTiles.Length; i++)
        {
            var loadedTile = _loadedTiles[i]!;
            var tilePos = loadedTile.Pos;
            var texture = loadedTile.Texture;
            shader.AddUniform($"tiles[{i}].pos", tilePos.ToVector2());
            shader.AddTexture($"tiles[{i}].data", texture);
        }
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
                    indices[i + 1] = bottomRight;
                    indices[i + 2] = topRight;
                }
                {
                    var i = ((z * N + x) * 2 + 1) * 3;
                    indices[i + 0] = topLeft;
                    indices[i + 1] = bottomLeft;
                    indices[i + 2] = bottomRight;
                }
            }
        }

        _model = Model.FromArray(vertices, indices);
        _model.AddVertexAttribute(new VertexAttribute("aPosition", VertexAttribType.Float, 2));
        if (_shadedModel != null) Resources.Dispose(_shadedModel);
        _shadedModel = new ShadedModel(_model, _shader, BufferUsageAccessFrequency.Static, BufferUsageAccessNature.Draw);
    }
    
    public void Draw(Camera camera, Sky sky)
    {
        var offset = new Vector2(_biggestSquares / 2.0f) - camera.Position.Xz.Modulo(_biggestSquares);
        var eye = new Vector3(-offset.X, camera.Position.Y, -offset.Y);
        var viewMat = Matrix4.LookAt(eye, eye + camera.Front, camera.Up);
        var modelPos = camera.Position.Xz + offset;
        _shader.SetUniform("modelPos", modelPos);
        _shader.SetUniform("viewMat", viewMat);
        _shader.SetUniform("projectionMat", camera.ProjectionMat);
        _shader.SetUniform("sunDir", sky.SunDirection);
        _shadedModel!.DrawInstanced(_chunks);
    }

    public void Update(Camera camera)
    {
        var xTileSpace = camera.Position.X / TileSize;
        var zTileSpace = camera.Position.Z / TileSize;

        if (xTileSpace < _left + 0.375f) SwitchTiles(xTileSpace, zTileSpace);
        else if (xTileSpace > _right - 0.375f) SwitchTiles(xTileSpace, zTileSpace);

        if (zTileSpace < _top + 0.375f) SwitchTiles(xTileSpace, zTileSpace);
        else if (zTileSpace > _bottom - 0.375f) SwitchTiles(xTileSpace, zTileSpace);
    }

    private void SwitchTiles(float x, float z)
    {
        var tileXLower = (int) Math.Floor(x - 0.5f);
        var tileZLower = (int) Math.Floor(z - 0.5f);
        var tileXUpper = tileXLower + 1;
        var tileZUpper = tileZLower + 1;

        _left = tileXLower;
        _right = tileXUpper + 1;
        _top = tileZLower;
        _bottom = tileZUpper + 1;

        Load(new Vector2i(tileXLower, tileZLower));
        Load(new Vector2i(tileXUpper, tileZLower));
        Load(new Vector2i(tileXUpper, tileZUpper));
        Load(new Vector2i(tileXLower, tileZUpper));
    }

    private void Load(Vector2i tilePos)
    {
        var i = tilePos.Y.Modulo(2) * 2 + tilePos.X.Modulo(2);
        var previous = _loadedTiles[i];
        if (previous != null)
        {
            if (previous.Pos == tilePos) return;
            previous.Unload();
        }
        var next = new LoadedTile(_data, tilePos);
        
        _loadedTiles[i] = next;

        foreach (var shader in _dependentShaders)
        {
            shader.SetUniform($"tiles[{i}].pos", tilePos.ToVector2());
            shader.SetTexture($"tiles[{i}].data", next.Texture);
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
        
        var actualRenderDistance = N * (1 << _LODs);
        ImGui.Text($"Actual: {actualRenderDistance}m ({_LODs} LODs)");
        ImGui.Value("Highest Quality", 2 * N, "%.0fm");
        ImGui.Value("Step Size", _biggestSquares, "%.0fm");
        ImGui.Text($"Chunks: {_chunks} (Instanced)");
        var triangleCount = _chunks * N * N * 2;
        var naiveTriangleCount = actualRenderDistance * actualRenderDistance * 4 * 2;
        ImGui.Text(
            $"Total Triangles: {triangleCount / 1000}K/{naiveTriangleCount / 1000}K ({(int) ((float) triangleCount / naiveTriangleCount * 100.0f)}%%)");
        
        //TODO: Create Window() method in TerrainData.cs
        _data.Profiler?.ImGuiTree();
            
        ImGui.End();
    }

    private record LoadedTile
    {
        public readonly Vector2i Pos;
        public readonly StaticTexture Texture;

        public LoadedTile(TerrainData data, Vector2i pos)
        {
            Pos = pos;

            var buffer = new byte[TileSize * TileSize * 4];
            Buffer.BlockCopy(data.GetTile(pos), 0, buffer, 0, buffer.Length);

            Texture = new StaticTexture(new TextureDataBuffer(buffer, TileSize, TileSize, PixelFormat.Red, PixelType.Float,
                SizedInternalFormat.R32f, TextureWrapMode.ClampToEdge, false, false));
            
            // Export as grayscale heightmap
            /*var path = $"Debug/tile_{Pos.X}_{Pos.Y}.pgm";
            using var stream = File.OpenWrite(path);
            stream.Write(Encoding.ASCII.GetBytes($"P5 {TileSize} {TileSize} {byte.MaxValue}\n"));
            foreach (var height in data.GetTile(pos))
            {
                stream.WriteByte((byte)((height / 100.0 - 30.0)*8.0));
            }*/
        }
    
        public void Unload()
        {
            Resources.Dispose(Texture);
        }
    }
}