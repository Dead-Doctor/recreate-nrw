using System.IO.Compression;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using recreate_nrw.Render;
using recreate_nrw.Util;

namespace recreate_nrw.Ground;

//      Data            |         Tiles
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
//
//
// Data points lie on the vertices (integer coordinates)
//                    |                    |
//                    |                  2047    2049
//             -2  -1 | 0   1   2 .. 2046  | 2048  
//                    |                    |        
//              :   : | :   :   :    :   : | :   :  
//      -2   .. x - x | x - x - x .. x - x | x - x ..
//              |   | | |   |   |    |   | | |   |  
//      -1   .. x - x | x - x - x .. x - x | x - x ..
//  ------------------+--------------------+-----------
//       0   .. x - x | x - x - x .. x - x | x - x ..
//              |   | | |   |   |    |   | | |   |  
//       1   .. x - x | x - x - x .. x - x | x - x ..
//              |   | | |   |   |    |   | | |   |  
//       2   .. x - x | x - x - x .. x - x | x - x ..
//              :   : | :   :   :    :   : | :   :  
//                    |                    |

//TODO: do as much asynchronous as possible
public static class TerrainData
{
    private const int DataSize = Coordinate.TerrainDataSize;
    private const int DataArea = DataSize * DataSize;
    private const int TileSize = Coordinate.TerrainTileSize;
    private const int TileArea = TileSize * TileSize;

    public static readonly List<Vector2i> AvailableData = new();
    public static Texture AvailableDataTilesTexture { get; private set; }

    private static readonly List<Vector2i> SavedTiles = new();
    private static readonly Dictionary<Vector2i, float[]> TileCache = new();
    private static readonly AsyncResourceLoader<Vector2i, float[]> TileLoader = new();
    private static readonly AsyncResourceLoaderCached<Vector2i, float[]> DataLoaderCached = new();

    static TerrainData()
    {
        // file: dgm1_32_{tile.X}_{tile.Y}_1_nw.xyz.gz
        const string directory = "Data/Raw/";
        const string prefix = directory + "dgm1";
        foreach (var file in Directory.EnumerateFiles(directory))
        {
            var parts = file.Split("_");
            if (parts.Length != 6 || parts[0] != prefix) continue;
            var position = new Vector2i(int.Parse(parts[2]), int.Parse(parts[3]));
            AvailableData.Add(position);
            Console.WriteLine($"[TerrainData]: Found data tile X: {position.X}, Y: {position.Y}");
        }

        var data = new float[AvailableData.Count * 2];
        for (var i = 0; i < AvailableData.Count; i++)
        {
            data[i * 2 + 0] = AvailableData[i].X;
            data[i * 2 + 1] = AvailableData[i].Y;
        }

        AvailableDataTilesTexture = new StaticTexture(new TextureInfo1D(
            new TextureData(data, PixelFormat.Rg, PixelType.Float), SizedInternalFormat.Rg32f, AvailableData.Count));
    }

    //TODO: might crash when called twice for same tile in quick succession for the first time
    public static async Task<float[]> GetTile(Vector2i pos)
    {
        lock (TileCache)
            if (TileCache.TryGetValue(pos, out var value))
                return value;

        return await TileLoader.LoadResourceAsync(pos, () =>
        {
            var tile = SavedTiles.Contains(pos) ? LoadTile(pos) : CreateTile(pos);
            lock (TileCache) TileCache.Add(pos, tile);
            return tile;
        });
    }

    private static float[] CreateTile(Vector2i pos)
    {
        var task = Profiler.Create($"CreateTile: ({pos.X}, {pos.Y})");

        // y is inverted for data tiles. Exclusive: yStart, xEnd
        var (xStart, yStart) = Coordinate.TerrainTileIndex(pos).TerrainData();
        var (xEnd, yEnd) = Coordinate.TerrainTileIndex(pos + Vector2i.One).TerrainData();

        var dataTileRows = TileSize / DataSize + 1;
        if (OffsetInDataTile(yStart - 1) <= OffsetInDataTile(TileSize)) dataTileRows++;
        var dataTileColumns = TileSize / DataSize + 1;
        if (OffsetInDataTile(xEnd - 1) <= OffsetInDataTile(TileSize)) dataTileColumns++;

        var topLeft = new Vector2i(xStart, yEnd) / DataSize;
        var dataTiles = new Task<float[]>[dataTileRows * dataTileColumns];
        for (var y = 0; y < dataTileRows; y++)
        {
            for (var x = 0; x < dataTileColumns; x++)
            {
                dataTiles[y * dataTileColumns + x] = GetData(topLeft + new Vector2i(x, y), task);
            }
        }

        var tile = new float[TileArea];

        for (var yOffset = 0; yOffset < TileSize; yOffset++)
        {
            var y = yEnd + yOffset;

            var xStartStrip = xStart;
            while (xStartStrip < xEnd)
            {
                var stepToNextDataTile = DataSize - OffsetInDataTile(xStartStrip);
                var xEndStrip = Math.Min(xStartStrip + stepToNextDataTile, xEnd); // Exclusive

                var relativeDataTile = new Vector2i(xStartStrip, y) / DataSize - topLeft;
                var dataTileIndex = relativeDataTile.Y * dataTileColumns + relativeDataTile.X;

                var dataIndex = OffsetInDataTile(y) * DataSize + OffsetInDataTile(xStartStrip);
                var tileIndex = (TileSize - yOffset - 1) * TileSize + (xStartStrip - xStart);
                Array.Copy(dataTiles[dataTileIndex].Result, dataIndex, tile, tileIndex, xEndStrip - xStartStrip);

                xStartStrip = xEndStrip;
            }
        }

        task.Stop( /*CreateTile*/);
        SaveTile(pos, tile);
        return tile;
    }

    private static int OffsetInDataTile(int pos)
    {
        return pos.Modulo(DataSize);
    }

    private static float[] LoadTile(Vector2i pos)
    {
        throw new NotImplementedException("Not implemented yet!");
    }

    private static void SaveTile(Vector2i pos, float[] tile)
    {
        //TODO: Not implemented yet!
        // _savedTiles.Add(pos);
    }

    //TODO: delete after all tiles have been created using this datatile
    private static async Task<float[]> GetData(Vector2i tile, Profiler task) =>
        await DataLoaderCached.Get(tile, i => LoadData(i, task));

    /// <summary>
    /// Time (avg for 9x, changes additional to previous):
    /// Line by line: 242ms
    /// Use 'ReadBlock': 214ms
    /// 100 Lines at a time: 196ms
    /// Parse Int manually: 139ms
    /// </summary>
    private static float[] LoadData(Vector2i tile, Profiler task)
    {
        var data = new float[DataArea];
        if (!AvailableData.Contains(tile))
        {
            //TODO: Temp
            Console.WriteLine($"[WARNING]: Data tile was not found. {tile}");
            return data;
        }

        var path = $"Data/Raw/dgm1_32_{tile.X}_{tile.Y}_1_nw.xyz.gz";

        using var stream = File.OpenRead(path);
        using var decompressed = new GZipStream(stream, CompressionMode.Decompress);
        using var reader = new StreamReader(decompressed);

        var subTask = task.Start($"LoadData: ({tile.X}, {tile.Y})");

        const int linesPerBlock = 1000 / 4;
        const int lineLength = 27 + 2; // \r\n => +2 chars
        const int heightIndex = 21;
        const int heightEndIndex = 27; // Exclusive

        //TODO: use some sort of partitioning or parallel processing
        var buffer = new char[lineLength * linesPerBlock];
        for (var i = 0; i < DataArea / linesPerBlock; i++)
        {
            reader.ReadBlock(buffer, 0, buffer.Length);

            for (var j = 0; j < linesPerBlock; j++)
            {
                var height = 0;
                for (var k = heightIndex; k < heightEndIndex; k++)
                {
                    if (buffer[j * lineLength + k] == ' ') break;
                    if (buffer[j * lineLength + k] == '.') continue;
                    height *= 10;
                    height += buffer[j * lineLength + k] - '0';
                }

                var lineI = i * linesPerBlock + j;
                var y = (999 - lineI / 1000) * 1000;
                data[y + lineI % 1000] = height / 100f;
            }
        }

        subTask.Stop( /*LoadData*/);
        return data;
    }

    public static float? GetHeightAt(Vector2i pos)
    {
        var coordinate = Coordinate.TerrainTile(pos);
        var task = GetTile(coordinate.TerrainTileIndex());
        if (!task.IsCompletedSuccessfully) return null;
        var tile = task.Result;
        var offset = coordinate.TerrainTile().Modulo(TileSize);
        return tile[offset.Y * TileSize + offset.X];
    }
}