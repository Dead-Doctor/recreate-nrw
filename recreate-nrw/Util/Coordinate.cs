using JetBrains.Annotations;
using OpenTK.Mathematics;

namespace recreate_nrw.Util;

public readonly struct Coordinate
{
    public const int TerrainTileSize = 512;
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
    public static Coordinate Epsg25832(Vector2 pos, float height = 0.0f) =>
        new(WithHeight(TerrainDataOrigin + pos * TerrainDataFlip, height));

    // Copied from: https://gist.github.com/triman/17eaac7ccb1ba89abcf694a80f2aa160

    /// <summary>
    /// Converts latitude/longitude coordinates to position
    /// </summary>
    /// <param name="pos">X: latitude, Y: longitude</param>
    /// <param name="height">Optional height value in meters</param>
    /// <returns></returns>
    [PublicAPI]
    public static Coordinate Wgs84(Vector2 pos, float height = 0.0f)
    {
        const double r = 6378137.0; //radius of earth: ellip.radius;
        const double eccSq = 0.00669438; //eccentricitySquared: ellip.eccsq = f*(2-f); flattening = 1 / 298.257223563;
        const double k0 = 0.9996;
        const double eccPrimeSquared = eccSq / (1.0 - eccSq);
        const int zoneNumber = 32; // epsg25832 => utm32

        // in middle of
        // zone
        const int longOrigin = (zoneNumber - 1) * 6 - 180 + 3; //+3 puts origin
        var longOriginRad = MathHelper.DegreesToRadians(longOrigin);

        var latRad = MathHelper.DegreesToRadians(pos.X);
        var longRad = MathHelper.DegreesToRadians(pos.Y);

        var n = r / Math.Sqrt(1 - eccSq * Math.Sin(latRad) * Math.Sin(latRad));
        var t = Math.Tan(latRad) * Math.Tan(latRad);
        var c = eccPrimeSquared * Math.Cos(latRad) * Math.Cos(latRad);
        var a = Math.Cos(latRad) * (longRad - longOriginRad);

        var m = r * ((1 - eccSq / 4 - 3 * eccSq * eccSq / 64 - 5 * eccSq * eccSq * eccSq / 256) *
            latRad -
            (3 * eccSq / 8 + 3 * eccSq * eccSq / 32 +
             45 * eccSq * eccSq * eccSq / 1024) * Math.Sin(2 * latRad) +
            (15 * eccSq * eccSq / 256 + 45 * eccSq * eccSq * eccSq / 1024) *
            Math.Sin(4 * latRad) - (35 * eccSq * eccSq * eccSq / 3072) * Math.Sin(6 * latRad));

        var utmEasting = k0 * n * (a + (1 - t + c)
            * a * a * a / 6.0 + (5 - 18 * t + t * t + 72 * c - 58 * eccPrimeSquared)
            * a * a * a * a * a / 120.0) + 500000.0;

        var utmNorthing = k0 * (m + n * Math.Tan(latRad) * (a * a / 2 + (5 - t + 9 * c + 4 * c * c)
            * a * a * a * a / 24.0 + (61 - 58 * t + t * t + 600 * c - 330 * eccPrimeSquared)
            * a * a * a * a * a * a / 720.0));

        //10000000 meter offset for southern hemisphere
        if (pos.X < 0.0) utmNorthing += 10000000.0;

        return Epsg25832(new Vector2((float)utmEasting, (float)utmNorthing), height);
    }

    [PublicAPI]
    public static Coordinate TerrainTile(Vector2i pos) => new(WithHeight(pos, 0));

    [PublicAPI]
    public static Coordinate TerrainTileIndex(Vector2i tile) => TerrainTile(tile * TerrainTileSize);

    [PublicAPI]
    public static Coordinate TerrainData(Vector2i pos) => new(WithHeight(TerrainDataOrigin + pos * TerrainDataFlip, 0));

    [PublicAPI]
    public static Coordinate TerrainDataIndex(Vector2i data) => TerrainData(data * TerrainDataSize);

    private Coordinate(Vector3 world)
    {
        _world = world;
        _worldInt = world.FloorToInt();
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
    public Vector2 Wgs84()
    {
        const double r = 6378137.0; //radius of earth: ellip.radius;
        const double eccSq = 0.00669438; //eccentricitySquared: ellip.eccsq = f*(2-f); flattening = 1 / 298.257223563;
        const double k0 = 0.9996;
        const double eccPrimeSquared = eccSq / (1.0 - eccSq);
        const int zoneNumber = 32; // epsg25832 => utm32
        const int longOrigin = (zoneNumber - 1) * 6 - 180 + 3; // +3 puts origin in middle of zone
        var e1 = (1 - Math.Sqrt(1 - eccSq)) / (1 + Math.Sqrt(1 - eccSq));

        var utm = Epsg25832();
        var x = utm.X - 500000.0; // remove 500,000 meter offset for longitude
        double y = utm.Y;

        var mu = y / k0 / (r * (1 - eccSq / 4 - 3 * eccSq * eccSq / 64 - 5 * eccSq * eccSq * eccSq / 256));

        var phi1Rad = mu
                      + Math.Sin(2 * mu) * (3 * e1 / 2 - 27 * e1 * e1 * e1 / 32)
                      + Math.Sin(4 * mu) * (21 * e1 * e1 / 16 - 55 * e1 * e1 * e1 * e1 / 32)
                      + Math.Sin(6 * mu) * (151 * e1 * e1 * e1 / 96);

        var n1 = r / Math.Sqrt(1 - eccSq * Math.Sin(phi1Rad) * Math.Sin(phi1Rad));
        var t1 = Math.Tan(phi1Rad) * Math.Tan(phi1Rad);
        var c1 = eccPrimeSquared * Math.Cos(phi1Rad) * Math.Cos(phi1Rad);
        var r1 = r * (1 - eccSq) / Math.Pow(1 - eccSq * Math.Sin(phi1Rad) * Math.Sin(phi1Rad), 1.5);
        var a = x / (n1 * k0);

        var lat = phi1Rad - (n1 * Math.Tan(phi1Rad) / r1)
            * (a * a / 2 - (5 + 3 * t1 + 10 * c1 - 4 * c1 * c1 - 9 * eccPrimeSquared)
                * a * a * a * a / 24 + (61 + 90 * t1 + 298 * c1 + 45 * t1 * t1 - 252 * eccPrimeSquared - 3 * c1 * c1)
                * a * a * a * a * a * a / 720);

        var lon = (a - (1 + 2 * t1 + c1)
            * a * a * a / 6 + (5 - 2 * c1 + 28 * t1 - 3 * c1 * c1 + 8 * eccPrimeSquared + 24 * t1 * t1)
            * a * a * a * a * a / 120) / Math.Cos(phi1Rad);

        return new Vector2((float)MathHelper.RadiansToDegrees(lat), (float)(longOrigin + MathHelper.RadiansToDegrees(lon)));
    }

    [PublicAPI]
    public Vector2i TerrainTile() => WithoutHeight(_worldInt);

    [PublicAPI]
    public Vector2i TerrainTileIndex() => (TerrainTile().ToVector2() / TerrainTileSize).FloorToInt();

    [PublicAPI]
    public Vector2i TerrainData() => (WithoutHeight(_worldInt) - TerrainDataOrigin) * TerrainDataFlip;

    [PublicAPI]
    public Vector2i TerrainDataIndex() => (TerrainData().ToVector2() / TerrainDataSize).FloorToInt();

    private static Vector2i WithoutHeight(Vector3i pos) => new(pos.X, pos.Z);
    private static Vector2 WithoutHeight(Vector3 pos) => new(pos.X, pos.Z);
    private static Vector3i WithHeight(Vector2i pos, int height) => new(pos.X, height, pos.Y);
    private static Vector3 WithHeight(Vector2 pos, float height) => new(pos.X, height, pos.Y);
}