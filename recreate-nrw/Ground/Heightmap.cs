#if INCLUDE_TERRAIN_MODEL
using System.IO.Compression;
using OpenTK.Mathematics;
using recreate_nrw.Util;

namespace recreate_nrw.Ground;

/// <summary>
/// Represents a source for height data.
/// </summary>
public class Heightmap
{
    private const int Size = 1000;
    
    /// <summary>
    /// The tile which has world coordinates (0,0).
    /// </summary>
    private readonly Vector2i _origin;
    
    /// <summary>
    /// Two-dimensional height array for every tile
    /// Vector2i: World tile coordinates
    /// </summary>
    private readonly Dictionary<Vector2i, float[,]> _values;

    /// <summary>
    /// Create a heightmap.
    /// </summary>
    /// <param name="origin">The tile which lays at the origin of the world coordinates.</param>
    public Heightmap(Vector2i origin)
    {
        _origin = origin;
        _values = new Dictionary<Vector2i, float[,]>();
    }
    
    //      Tiles           |         World
    //  0,1       1,1       |          0,-1
    //   +---------+        |           |
    //   |         |        |           |
    //   |  (x,y)  |        |   -1,0 ---+--- 1,0
    //   |         |        |           |
    //   +---------+        |    (x,z)  |
    //  0,0       1,0       |          0,1
    //                      |
    // from world:          |  from tile:
    // x= origin.x + pos.x  |  x=  pos.x - origin.x
    // y= origin.y - pos.z  |  z= -pos.y - origin.y


    /// <summary>
    /// Load the tile which contains this position.
    /// </summary>
    /// <param name="worldTile">The world tile to load.</param>
    /// <exception cref="Exception">Throws an exception if the file containing this data is not found.</exception>
    public void LoadTile(Vector2i worldTile)
    {
        var tile = new Vector2i(_origin.X + worldTile.X, _origin.Y - worldTile.Y);
        
        ushort minHeight = 0;
        string? path = null;

        var fileNames = Directory.EnumerateFiles("Resources", $"data_{tile.X}_{tile.Y}_*.gz");
        foreach (var fileName in fileNames)
        {
            var parts = fileName.Split("_");
            if (parts[1] != tile.X.ToString() || parts[2] != tile.Y.ToString()) continue;
            minHeight = ushort.Parse(parts[3][..^3]);
            path = fileName;
            break;
        }

        if (path == null) throw new Exception($"Data file not found: {tile.X} {tile.Y}");

        using var stream = File.OpenRead(path);
        using var decompressed = new GZipStream(stream, CompressionMode.Decompress);
        using var binaryStream = new BinaryReader(decompressed);

        _values[worldTile] = new float[Size, Size];
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                var height = (minHeight + binaryStream.ReadUInt16()) / 100.0f;
                var world = worldTile * 1000 + new Vector2(x, Size - 1 - y);
                _values[worldTile][(int)world.X, (int)world.Y] = height;
            }
        }
    }

    /// <summary>
    /// Returns the world position at the surface of the heightmap.
    /// </summary>
    /// <param name="pos">The position in world space.</param>
    /// <exception cref="Exception">Throws an exception if this tile is not loaded.</exception>
    public Vector3 this[Vector2i pos]
    {
        get
        {
            var worldTile = new Vector2i((int) MathF.Floor(pos.X / (float) Size),
                (int) MathF.Floor(pos.Y / (float) Size));
            if (!_values.ContainsKey(worldTile)) throw new Exception($"The tile {worldTile} containing the position {pos} is not loaded!");
            var subTilePos = GetSubTilePos(pos);
            return new Vector3(pos.X, _values[worldTile][subTilePos.X, subTilePos.Y], pos.Y);
        }
    }

    /// <summary>
    /// Returns the sub tile position avoiding negative numbers.
    /// </summary>
    /// <param name="pos">The position in world space.</param>
    /// <returns>The sub tile position.</returns>
    private static Vector2i GetSubTilePos(Vector2i pos) => pos.Modulo(Size);
}
#endif