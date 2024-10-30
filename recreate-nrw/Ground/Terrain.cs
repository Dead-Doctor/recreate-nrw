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

    public const int TextureLODs = 3;
    public const int TextureLODSize = 2;
    private const int TexturesPerLOD = TextureLODSize * TextureLODSize;
    private const int TextureCount = TextureLODs * TexturesPerLOD;
    private const float DelayedSwitchBufferRegion = 0.25f;

    private readonly Shader _shader;
    private Model? _model;
    private ShadedModel? _shadedModel;

    public readonly Vector2[] Center = new Vector2[TextureLODs];
    private readonly LoadedTile[] _loadedTiles = new LoadedTile[TextureCount];

    private readonly StaticTexture _tilesTexture = StaticTexture.CreateFrom(new TextureInfo2DArray(
        SizedInternalFormat.R32f, new Vector2i(BaseTileSize), TextureCount, TextureWrapMode.ClampToEdge,
        false, false));

    private readonly StaticTexture _tilesPosTexture =
        StaticTexture.CreateFrom(new TextureInfo1D(SizedInternalFormat.Rg32f, TextureCount));

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
            _LODs = (int)Math.Ceiling(Math.Log2((float)_renderDistance / N));
            _biggestSquares = 1 << (_LODs - 1);
            if (_biggestSquares >= N)
                Console.WriteLine("[WARNING]: Step size is greater or equal to the distance of highest LOD.");
            _chunks = 4 * (1 + 3 * _LODs);
        }
    }

    public Terrain()
    {
        RenderDistance = 2048;

        _shader = new Shader("terrain");
        _shader.AddUniform<Vector2>("modelPos");
        _shader.AddUniform<Matrix4>("viewMat");
        _shader.AddUniform<Matrix4>("projectionMat");
        _shader.AddUniform("n", N);
        _shader.AddUniform<Vector3>("sunDir");
        _shader.AddUniform<int>("debug");

        for (var lod = 0; lod < TextureLODs; lod++)
        {
            for (var i = 0; i < TexturesPerLOD; i++)
            {
                _loadedTiles[lod * TexturesPerLOD + i] = new LoadedTile();
            }
            SwitchTiles(lod, Vector2.Zero);
        }

        AddDependentShader(_shader);

        GenerateModel();
    }

    public void AddDependentShader(Shader shader)
    {
        Console.WriteLine($"Marked shader '{shader}' as dependent on terrain data.");
        shader.AddUniform("textureBaseSize", BaseTileSize);
        shader.AddUniform("texturesPerLod", TexturesPerLOD);
        shader.AddTexture("tileData", _tilesTexture);
        shader.AddTexture("tilePos", _tilesPosTexture);
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
                var topLeft = (uint)(z * vertexCount + x);
                var topRight = (uint)(z * vertexCount + x + 1);
                var bottomLeft = (uint)((z + 1) * vertexCount + x);
                var bottomRight = (uint)((z + 1) * vertexCount + x + 1);

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
        _shadedModel =
            new ShadedModel(_model, _shader, BufferUsageAccessFrequency.Static, BufferUsageAccessNature.Draw);
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

            var bounds = Center[lod].GrowToBox(0.5f + DelayedSwitchBufferRegion);
            if (!bounds.ContainsInclusive(tileSpace)) SwitchTiles(lod, tileSpace);
        }
        
        for (var i = 0; i < TextureCount; i++)
            _loadedTiles[i].TryUpload(i, _tilesTexture, _tilesPosTexture);
    }

    private void SwitchTiles(int lod, Vector2 tileSpace)
    {
        const float radius = TextureLODSize / 2f;
        const float centerOffset = radius % 1f;
        var tileCenter = (tileSpace + new Vector2(0.5f - centerOffset)).FloorToInt();

        Center[lod] = tileCenter + new Vector2(centerOffset);

        var topLeftOffset = (int)Math.Ceiling(radius);
        for (var yOffset = 0; yOffset < TextureLODSize; yOffset++)
        {
            var y = tileCenter.Y - topLeftOffset + yOffset;
            for (var xOffset = 0; xOffset < TextureLODSize; xOffset++)
            {
                var x = tileCenter.X - topLeftOffset + xOffset;
                Load(lod, new Vector2i(x, y));
            }
        }
    }

    private void Load(int lod, Vector2i tileSpacePos)
    {
        var i = tileSpacePos.Y.Modulo(TextureLODSize) * TextureLODSize + tileSpacePos.X.Modulo(TextureLODSize);
        var tile = _loadedTiles[lod * TexturesPerLOD + i];
        var stepSize = 1 << lod;
        tile.MoveTile(new Vector3i(tileSpacePos * stepSize, lod));
    }

    public void Window()
    {
        ImGui.Begin("Terrain");

        const int bytesPerTexture = BaseTileSize * BaseTileSize * sizeof(float);
        const int totalTextureSize = TextureCount * bytesPerTexture;
        ImGui.Text($"Texture Size: {bytesPerTexture.FormatSize()}");
        ImGui.Text($"Texture LODs: {TextureLODs}");
        ImGui.Text($"Total texture size: {totalTextureSize.FormatSize()}");
        const int biggestLodSize = BaseTileSize << (TextureLODs - 1);
        const float bestDistance = TextureLODSize / 2f;
        const float worstDistance = (TextureLODSize - 1) / 2f - DelayedSwitchBufferRegion;
        ImGui.Text(
            $"Texture coverage distance (worse/best): ({(worstDistance * biggestLodSize).FormatDistance()}/{(bestDistance * biggestLodSize).FormatDistance()})");

        ImGui.Separator();

        ImGui.PushItemWidth(100.0f);
        var cachedRenderDistance = RenderDistance;
        if (ImGui.InputInt("Render Distance", ref cachedRenderDistance, 256, 1024,
                ImGuiInputTextFlags.EnterReturnsTrue))
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
            $"Total Triangles: {triangleCount.FormatCount()}/{naiveTriangleCount.FormatCount()} ({(int)((float)triangleCount / naiveTriangleCount * 100.0f)}%%)");

        ImGui.End();
    }

    private class LoadedTile
    {
        private Vector3i? _pos;
        private float[]? _buffer;

        public async void MoveTile(Vector3i pos)
        {
            if (_pos == pos) return;
            _pos = pos;
            _buffer = null;
            _buffer = await TerrainData.GetTile(pos);
        }

        public void TryUpload(int index, StaticTexture tilesTexture, StaticTexture tilesPosTexture)
        {
            if (_buffer is null) return;
            
            tilesTexture.UploadImageData(new TextureData2DArray(_buffer!, PixelFormat.Red, PixelType.Float, null, index,
                null, 1));
            tilesPosTexture.UploadImageData(new TextureData1D(new float[] { _pos!.Value.X, _pos!.Value.Y }, PixelFormat.Rg,
                PixelType.Float, index, 1));

            _buffer = null;
        }
    }
}