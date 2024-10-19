using System.IO.Compression;
using OpenTK.Mathematics;
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
public class TerrainData
{
    private const int DataSize = Coordinate.TerrainDataSize;
    private const int DataArea = DataSize * DataSize;
    private const int TileSize = Coordinate.TerrainTileSize;
    private const int TileArea = TileSize * TileSize;
    
    private readonly List<Vector2i> _savedTiles = new();
    private readonly Dictionary<Vector2i, float[]> _tileCache = new();
    private readonly AsyncResourceLoader<Vector2i, float[]> _tileLoader = new();
    private readonly AsyncResourceLoaderCached<Vector2i, float[]> _dataLoaderCached = new();

    //TODO: might crash when called twice for same tile in quick succession for the first time
    public async Task<float[]> GetTile(Vector2i pos)
    {
        lock (_tileCache)
            if (_tileCache.TryGetValue(pos, out var value)) return value;
        
        return await _tileLoader.LoadResourceAsync(pos, () =>
        {
            var tile = _savedTiles.Contains(pos) ? LoadTile(pos) : CreateTile(pos);
            lock (_tileCache) _tileCache.Add(pos, tile);
            return tile;
        });
    }

    private float[] CreateTile(Vector2i pos)
    {
        Profiler.Start($"CreateTile: ({pos.X}, {pos.Y})");
        //TODO: check if still works if any variable negative

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
                dataTiles[y * dataTileColumns + x] = GetData(topLeft + new Vector2i(x, y));
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

        Profiler.Stop( /*CreateTile*/);
        SaveTile(pos, tile);
        return tile;
    }

    private static int OffsetInDataTile(int pos)
    {
        return pos.Modulo(DataSize);
    }

    private float[] LoadTile(Vector2i pos)
    {
        throw new NotImplementedException("Not implemented yet!");
    }

    private void SaveTile(Vector2i pos, float[] tile)
    {
        //TODO: Not implemented yet!
        // _savedTiles.Add(pos);
    }
    
    //TODO: delete after all tiles have been created using this datatile
    private async Task<float[]> GetData(Vector2i tile) => await _dataLoaderCached.Get(tile, LoadData);

    /// <summary>
    /// Time (avg for 9x, changes additional to previous):
    /// Line by line: 242ms
    /// Use 'ReadBlock': 214ms
    /// 100 Lines at a time: 196ms
    /// Parse Int manually: 139ms
    /// </summary>
    private static float[] LoadData(Vector2i tile)
    {
        var data = new float[DataArea];
        try
        {
            var path = $"Data/Raw/dgm1_32_{tile.X}_{tile.Y}_1_nw.xyz.gz";

            using var stream = File.OpenRead(path);
            using var decompressed = new GZipStream(stream, CompressionMode.Decompress);
            using var reader = new StreamReader(decompressed);

            Profiler.Start($"LoadData: ({tile.X}, {tile.Y})");
            
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

            Profiler.Stop( /*LoadData*/);
        }
        catch (FileNotFoundException)
        {
            //TODO: Temp
            Console.WriteLine($"[WARNING]: Data tile was not found. {tile}");
        }
        
        return data;
    }
}