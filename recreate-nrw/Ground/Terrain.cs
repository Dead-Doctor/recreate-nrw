using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using recreate_nrw.Render;
using recreate_nrw.Util;
using Buffer = System.Buffer;

namespace recreate_nrw.Ground;

public class Terrain
{
    private const int BaseTileSize = Coordinate.TerrainTileSize;
    
    private int _n = 32;
    private int _renderDistance;
    // ReSharper disable once InconsistentNaming
    private int _LODs;
    private int _biggestSquares;
    private int _chunks;

    public const int TextureLODs = 2;
    private const int TexturesPerLOD = 4;
    private const float SwitchRegionSize = 0.375f;
    
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

    public readonly Vector2i[] Center = new Vector2i[TextureLODs];
    private readonly LoadedTile[][] _loadedTiles = new LoadedTile[TextureLODs][];

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
        _shader.AddUniform<int>("debug");

        for (var lod = 0; lod < TextureLODs; lod++)
        {
            _loadedTiles[lod] = new LoadedTile[TexturesPerLOD];
            for (var i = 0; i < TexturesPerLOD; i++)
            {
                _loadedTiles[lod][i] = new LoadedTile(lod);
            }
            SwitchTiles(lod, Vector2.Zero);
        }
        AddDependentShader(_shader);
        
        GenerateModel();
    }

    public void AddDependentShader(Shader shader)
    {
        Console.WriteLine($"Marked shader '{shader}' as dependent.");
        _dependentShaders.Add(shader);
        shader.AddUniform("textureBaseSize", BaseTileSize);
        for (var i = 0; i < TextureLODs; i++)
        {
            for (var j = 0; j < TexturesPerLOD; j++)
            {
                var loadedTile = _loadedTiles[i][j];
                var tilePos = loadedTile.Pos!.Value;
                var texture = loadedTile.Texture;
                shader.AddUniform($"tiles[{i}][{j}].pos", tilePos.ToVector2());
                shader.AddTexture($"tiles[{i}][{j}].data", texture);
            }
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
    
    public void Draw(Camera camera, Sky sky, bool debug = false)
    {
        var offset = new Vector2(_biggestSquares / 2.0f) - camera.Position.Xz.Modulo(_biggestSquares);
        var eye = new Vector3(-offset.X, camera.Position.Y, -offset.Y);
        var viewMat = Matrix4.LookAt(eye, eye + camera.Front, camera.Up);
        var modelPos = camera.Position.Xz + offset;
        _shader.SetUniform("modelPos", modelPos);
        _shader.SetUniform("viewMat", viewMat);
        _shader.SetUniform("projectionMat", camera.ProjectionMat);
        _shader.SetUniform("sunDir", sky.SunDirection);
        _shader.SetUniform("debug", debug ? 1 : 0);
        _shadedModel!.DrawInstanced(_chunks);
    }

    public void Update(Camera camera)
    {
        for (var lod = 0; lod < TextureLODs; lod++)
        {
            var stepSize = 1 << lod;
            var tileSize = BaseTileSize * stepSize;
            var tileSpace = camera.Position.Xz / tileSize;
            
            var bounds = Center[lod].ToVector2().GrowToBox(1 - SwitchRegionSize);
            if (!bounds.ContainsInclusive(tileSpace)) SwitchTiles(lod, tileSpace);
        }

        for (var lod = 0; lod < TextureLODs; lod++)
        {
            for (var i = 0; i < TexturesPerLOD; i++)
            {
                var loadedTile = _loadedTiles[lod][i];
                if (!loadedTile.TryUpload()) continue;
                foreach (var shader in _dependentShaders)
                {
                    var tilePos = loadedTile.Pos!.Value;
                    shader.SetUniform($"tiles[{lod}][{i}].pos", tilePos.ToVector2());
                }
            }
        }
    }

    private void SwitchTiles(int lod, Vector2 tileSpace)
    {
        var tileCenter = (tileSpace + new Vector2(0.5f)).FloorToInt();

        Center[lod] = tileCenter;

        Load(lod, new Vector2i(tileCenter.X - 1, tileCenter.Y - 1));
        Load(lod, new Vector2i(tileCenter.X - 1, tileCenter.Y));
        Load(lod, new Vector2i(tileCenter.X, tileCenter.Y - 1));
        Load(lod, new Vector2i(tileCenter.X, tileCenter.Y));
    }

    private void Load(int lod, Vector2i tileSpacePos)
    {
        var i = tileSpacePos.Y.Modulo(2) * 2 + tileSpacePos.X.Modulo(2);
        var tile = _loadedTiles[lod][i];
        var stepSize = 1 << lod;
        var tilePos = tileSpacePos * stepSize;
        if (tile.Pos == tilePos) return;
        tile.MoveTile(tilePos);
    }

    public void Window()
    {
        ImGui.Begin("Terrain");
        
        const int bytesPerTexture = BaseTileSize * BaseTileSize * sizeof(float);
        ImGui.Text($"Texture Size: {bytesPerTexture.FormatSize()}");
        ImGui.Text($"Texture LODs: {TextureLODs}");
        ImGui.Text($"Total texture size: {(TextureLODs * TexturesPerLOD * bytesPerTexture).FormatSize()}");
        const int biggestLodSize = BaseTileSize << (TextureLODs - 1);
        ImGui.Text($"Texture coverage distance (worse/best): ({(biggestLodSize * SwitchRegionSize).FormatDistance()}/{biggestLodSize.FormatDistance()})");
        
        ImGui.Separator();

        ImGui.PushItemWidth(100.0f);
        var cachedRenderDistance = RenderDistance;
        if (ImGui.DragInt("Render Distance", ref cachedRenderDistance, 1.0f, 2 * N, 2048))
            RenderDistance = cachedRenderDistance;
        var cachedN = N;
        if (ImGui.InputInt("N", ref cachedN, 8, 32, ImGuiInputTextFlags.EnterReturnsTrue))
            N = cachedN;
        ImGui.PopItemWidth();
        
        var actualRenderDistance = N * (1 << _LODs);
        ImGui.Text($"Actual: {actualRenderDistance.FormatDistance()} ({_LODs} LODs)");
        ImGui.Text($"Highest Quality: {(2 * N).FormatDistance()}");
        ImGui.Text($"Step Size: {_biggestSquares.FormatDistance()}");
        ImGui.Text($"Chunks: {_chunks} (Instanced)");
        var triangleCount = _chunks * N * N * 2;
        var naiveTriangleCount = actualRenderDistance * actualRenderDistance * 4 * 2;
        ImGui.Text(
            $"Total Triangles: {triangleCount.FormatCount()}/{naiveTriangleCount.FormatCount()} ({(int) ((float) triangleCount / naiveTriangleCount * 100.0f)}%%)");
        
        ImGui.End();
    }

    private class LoadedTile
    {
        private readonly int _lod;
        public Vector2i? Pos;
        public readonly StaticTexture Texture = new(new TextureInfo2D(null, SizedInternalFormat.R32f,
            new Vector2i(BaseTileSize), TextureWrapMode.ClampToEdge, false, false));

        private float[]? _buffer;

        public LoadedTile(int lod)
        {
            _lod = lod;
        }

        public async void MoveTile(Vector2i pos)
        {
            Pos = pos;
            _buffer = null;
            _buffer = await TerrainData.GetTile(new Vector3i(pos.X, pos.Y, _lod));
        }

        public bool TryUpload()
        {
            if (_buffer is null) return false;
            Texture.UploadImageData(new TextureInfo2D(
                new TextureData(_buffer!, PixelFormat.Red, PixelType.Float),
                SizedInternalFormat.R32f, new Vector2i(BaseTileSize),
                TextureWrapMode.ClampToEdge, false, false)
            );
            _buffer = null;
            return true;
        }
    }
}