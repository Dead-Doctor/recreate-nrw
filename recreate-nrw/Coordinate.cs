using JetBrains.Annotations;
using OpenTK.Mathematics;

namespace recreate_nrw;


//TODO: do not convert to float when only converting between tile spaces
public readonly struct Coordinate
{
    public const int TerrainTileSize = 2048;
    public const int TerrainDataSize = 1000;

    /// <summary>
    /// World coordinates of terrain data origin;
    /// </summary>
    private static readonly Vector2i TerrainDataOrigin = new(-346000, 5675000);
    private static readonly Vector2i TerrainDataFlip = new(1, -1);

    private readonly Vector3 _world;
    private readonly Vector3i _worldInt;

    [PublicAPI]
    public static Coordinate World(Vector3 pos) => new(pos);

    [PublicAPI]
    public static Coordinate Epsg25832(Vector2 pos, float height = 0.0f) => new(WithHeight(TerrainDataOrigin + pos* TerrainDataFlip, height));
    
    [PublicAPI]
    public static Coordinate TerrainTile(Vector2i pos) => new(WithHeight(pos, 0));
    
    [PublicAPI]
    public static Coordinate TerrainTileIndex(Vector2i tile) => TerrainTile(tile * TerrainTileSize);
    
    [PublicAPI]
    public static Coordinate TerrainData(Vector2i pos) => new(WithHeight(TerrainDataOrigin + pos*TerrainDataFlip, 0));
    
    [PublicAPI]
    public static Coordinate TerrainDataIndex(Vector2i data) => TerrainData(data * TerrainDataSize);

    private Coordinate(Vector3 world)
    {
        _world = world;
        _worldInt = FloorToInt(world);
    }
    
    
    private Coordinate(Vector3i world)
    {
        _world = world.ToVector3();
        _worldInt = world;
    }
    
    [PublicAPI]
    public Vector3 World() => _world;
    
    [PublicAPI]
    public Vector3 World(float height) => WithHeight(WithoutHeight(_world), height);
    
    [PublicAPI]
    public Vector2 Epsg25832() => (WithoutHeight(_world) - TerrainDataOrigin) * TerrainDataFlip;
    
    [PublicAPI]
    public Vector2i TerrainTile() => WithoutHeight(_worldInt);

    [PublicAPI]
    public Vector2i TerrainTileIndex() => FloorToInt(TerrainTile().ToVector2() / TerrainTileSize);

    [PublicAPI]
    public Vector2i TerrainData() => (WithoutHeight(_worldInt) - TerrainDataOrigin) * TerrainDataFlip;

    [PublicAPI]
    public Vector2i TerrainDataIndex() => FloorToInt(TerrainData().ToVector2() / TerrainDataSize);

    [PublicAPI]
    public static Vector2i FloorToInt(Vector2 vec) => new((int) Math.Floor(vec.X), (int) Math.Floor(vec.Y));
    
    [PublicAPI]
    public static Vector3i FloorToInt(Vector3 vec) => new((int) Math.Floor(vec.X), (int) Math.Floor(vec.Y), (int) Math.Floor(vec.Z));
    
    private static Vector2i WithoutHeight(Vector3i pos) => new(pos.X, pos.Z);
    private static Vector2 WithoutHeight(Vector3 pos) => new(pos.X, pos.Z);
    private static Vector3i WithHeight(Vector2i pos, int height) => new (pos.X, height, pos.Y);
    private static Vector3 WithHeight(Vector2 pos, float height) => new (pos.X, height, pos.Y);
}