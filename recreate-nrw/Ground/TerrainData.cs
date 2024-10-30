using System.Collections.Concurrent;
using System.IO.Compression;
using System.Text;
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
// Data points lie on the vertices (integer coordinates). Example: BaseTileSize = 1024, Lod = 1 (=> 2048)
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
    private const int BaseTileSize = Coordinate.TerrainTileSize;
    private const int TileArea = BaseTileSize * BaseTileSize;

    public static readonly List<Vector2i> AvailableData = new();
    public static Texture AvailableDataTilesTexture { get; private set; }

    // tile: X: x, Y: z, Z: lod
    private static readonly List<Vector3i> SavedTiles = new();
    private static readonly ConcurrentDictionary<Vector3i, float[]> TileCache = new();
    private static readonly AsyncResourceLoader<Vector3i, float[]> TileLoader = new();
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

        AvailableDataTilesTexture = StaticTexture.CreateFrom(new TextureInfo1D(
            new TextureData1D(data, PixelFormat.Rg, PixelType.Float), SizedInternalFormat.Rg32f, AvailableData.Count));
    }

    //TODO: might crash when called twice for same tile in quick succession for the first time
    public static async Task<float[]> GetTile(Vector3i pos)
    {
        if (TileCache.TryGetValue(pos, out var value))
            return value;

        return await TileLoader.LoadResourceAsync(pos, () =>
        {
            var tile = SavedTiles.Contains(pos) ? LoadTile(pos) : CreateTile(pos);
            if (!TileCache.TryAdd(pos, tile)) Console.WriteLine("[WARNING] Reloaded already cached tile.");
            return tile;
        });
    }

    private static float[] CreateTile(Vector3i pos)
    {
        var createTileTask = Profiler.Create($"CreateTile: ({pos.X}, {pos.Y})");


        var tile = new float[TileArea];
        if (pos.Z == 0)
        {
            // y is inverted for data tiles. Exclusive: yEnd, xEnd
            var (xStart, yEnd) = Coordinate.TerrainTileIndex(pos.Xy).TerrainData();
            var (xEnd, yStart) = Coordinate.TerrainTileIndex(pos.Xy + Vector2i.One).TerrainData();

            var dataTileRows = BaseTileSize / DataSize + 1;
            var dataTileColumns = BaseTileSize / DataSize + 1;
            if ((yEnd - 1).Modulo(DataSize) <= BaseTileSize.Modulo(DataSize)) dataTileRows++;
            if ((xEnd - 1).Modulo(DataSize) <= BaseTileSize.Modulo(DataSize)) dataTileColumns++;

            var topLeft = (new Vector2(xStart, yStart) / DataSize).FloorToInt();
            var dataTiles = new Task<float[]>[dataTileRows * dataTileColumns];
            for (var y = 0; y < dataTileRows; y++)
            {
                for (var x = 0; x < dataTileColumns; x++)
                {
                    dataTiles[y * dataTileColumns + x] = GetData(topLeft + new Vector2i(x, y), createTileTask);
                }
            }

            for (var yOffset = 0; yOffset < BaseTileSize; yOffset++)
            {
                var y = yStart + yOffset;
                var dataRowStartIndex = y.Modulo(DataSize) * DataSize;
                var tileRowStartIndex = (BaseTileSize - yOffset - 1) * BaseTileSize;

                var xStartStrip = xStart;
                while (xStartStrip < xEnd)
                {
                    var xEndStrip = Math.Min(xStartStrip.FloorStep(DataSize) + DataSize, xEnd); // Exclusive

                    var relativeDataTile = (new Vector2(xStartStrip, y) / DataSize).FloorToInt() - topLeft;
                    var dataTileIndex = relativeDataTile.Y * dataTileColumns + relativeDataTile.X;

                    var dataIndex = dataRowStartIndex + xStartStrip.Modulo(DataSize);
                    var tileIndex = tileRowStartIndex + (xStartStrip - xStart);
                    Array.Copy(dataTiles[dataTileIndex].Result, dataIndex, tile, tileIndex, xEndStrip - xStartStrip);

                    xStartStrip = xEndStrip;
                }
            }
        }
        else
        {
            var stepSize = 1 << pos.Z;
            var stepsPerTile = BaseTileSize / stepSize;
            
            var tiles = new Task<float[]>[stepSize * stepSize];
            for (var y = 0; y < stepSize; y++)
            {
                for (var x = 0; x < stepSize; x++)
                {
                    tiles[y * stepSize + x] = GetTile(new Vector3i(pos.X + x, pos.Y + y, 0));
                }
            }
            for (var yTile = 0; yTile < stepSize; yTile++)
            {
                for (var xTile = 0; xTile < stepSize; xTile++)
                {
                    for (var yOffset = 0; yOffset < stepsPerTile; yOffset++)
                    {
                        var y = yTile * stepsPerTile + yOffset;
                        var yWithin = yOffset * stepSize;
                        for (var xOffset = 0; xOffset < stepsPerTile; xOffset++)
                        {
                            var x = xTile * stepsPerTile + xOffset;
                            var xWithin = xOffset * stepSize;
                            tile[y * BaseTileSize + x] = tiles[yTile * stepSize + xTile].Result[yWithin * BaseTileSize + xWithin];
                        }
                    }
                }
            }
        }

        createTileTask.Stop();
        SaveTile(pos, tile);
        return tile;
    }

    private static float[] LoadTile(Vector3i pos)
    {
        throw new NotImplementedException("Not implemented yet!");
    }

    private static void SaveTile(Vector3i pos, float[] tile)
    {
        //TODO: Not implemented yet!
        // _savedTiles.Add(pos);
        
        /*// Export as grayscale heightmap
        var path = $"Debug/tile_{pos.Z}_{pos.X}_{pos.Y}.pgm";
        using var stream = File.OpenWrite(path);
        stream.Write(Encoding.ASCII.GetBytes($"P5 {BaseTileSize} {BaseTileSize} {byte.MaxValue}\n"));
        foreach (var height in tile)
        {
            stream.WriteByte((byte)((height - 30f) / (50f - 30f) * 255));
        }*/
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

        var loadingDataTask = task.Start($"LoadData: ({tile.X}, {tile.Y})");

        const int blocks = 250;
        const int linesPerBlock = DataArea / blocks;
        const int lineLength = 27 + 2; // \r\n => +2 chars
        const int bufferSize = lineLength * linesPerBlock;
        const int heightIndex = 21;
        const int heightEndIndex = 27; // Exclusive

        //TODO: use some sort of partitioning or parallel processing
        var buffer = new char[bufferSize];
        for (var i = 0; i < blocks; i++)
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

        loadingDataTask.Stop();
        return data;
    }

    public static float? GetHeightAt(Vector2i pos)
    {
        var coordinate = Coordinate.TerrainTile(pos);
        var terrainTileIndex = coordinate.TerrainTileIndex();
        var task = GetTile(new Vector3i(terrainTileIndex.X, terrainTileIndex.Y, 0));
        if (!task.IsCompletedSuccessfully) return null;
        var tile = task.Result;
        var offset = coordinate.TerrainTile().Modulo(BaseTileSize);
        return tile[offset.Y * BaseTileSize + offset.X];
    }
}