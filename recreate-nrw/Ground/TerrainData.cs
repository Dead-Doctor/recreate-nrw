using System.Collections;
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

    private readonly Dictionary<Vector2i, int[]> _data = new();
    // private readonly Hashtable _data = new();
    private readonly Dictionary<Vector2i, float[]> _tiles = new();
    private readonly List<Vector2i> _savedTiles = new();

    public float[] GetTile(Vector2i pos)
    {
        // Check loaded
        if (_tiles.TryGetValue(pos, out var tile))
        {
            return tile;
        }

        // Check hard drive
        if (_savedTiles.Contains(pos))
        {
            //TODO: tile = ReadTile(pos);
            tile = null!;
        }
        else
        {
            // Generate from heightmap
            tile = CreateTile(pos);

            //TODO: do asynchronous
            //TODO: SaveTile(generatedTile);
        }

        _tiles.Add(pos, tile);
        return tile;
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
        var dataTiles = new Task<int[]>[dataTileRows * dataTileColumns];
        for (var y = 0; y < dataTileRows; y++)
        {
            for (var x = 0; x < dataTileColumns; x++)
            {
                dataTiles[y * dataTileColumns + x] = GetData(topLeft + new Vector2i(x, y));
            }
        }
        // for (var y = 0; y < dataTileRows; y++)
        // {
        //     for (var x = 0; x < dataTileColumns; x++)
        //     {
        //         dataTiles[y * dataTileColumns + x].Result;
        //     }
        // }
        // // Task.WaitAll(dataTiles);
        
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

        Profiler.Stop(/*CreateTile*/);
        return tile;
    }

    private static int OffsetInDataTile(int pos)
    {
        return pos.Modulo(DataSize);
    }

    private readonly Hashtable _activeLoadingTasks = Hashtable.Synchronized(new Hashtable()); 
    private async Task<int[]> GetData(Vector2i tile)
    {
        lock (_data)
        {
            if (_data.TryGetValue(tile, out var data)) return data;
        }

        Task<int[]>? loadingTask;
        if (_activeLoadingTasks.Contains(tile))
        {
            loadingTask = (Task<int[]>)_activeLoadingTasks[tile]!;
        }
        else
        {
            loadingTask = Task.Factory.StartNew(() =>
            {
                var data = LoadData(tile);
                _activeLoadingTasks.Remove(tile);
                return data;
            }, TaskCreationOptions.LongRunning);
            _activeLoadingTasks.Add(tile, loadingTask);
        }
        var loadedData = await loadingTask;
        lock (_data)
        {
            _data.Add(tile, loadedData);
        }
        return loadedData;
    }

    /// <summary>
    /// Time (avg for 9x, changes additional to previous):
    /// Line by line: 242ms
    /// Use 'ReadBlock': 214ms
    /// 100 Lines at a time: 196ms
    /// Parse Int manually: 139ms
    /// </summary>
    private static int[] LoadData(Vector2i tile)
    {
        var data = new int[DataArea];
        try
        {
            var path = $"Data/Raw/dgm1_32_{tile.X}_{tile.Y}_1_nw.xyz.gz";

            using var stream = File.OpenRead(path);
            using var decompressed = new GZipStream(stream, CompressionMode.Decompress);
            using var reader = new StreamReader(decompressed);

            Profiler.Start($"LoadData: ({tile.X}, {tile.Y})");

            var line = reader.ReadLine()!;

            var lastSpace = line[..^1].LastIndexOf(' ');
            var blocks = line.Split(' ')[2].Split('.');
            var firstBlockIndex = lastSpace + 1;
            var firstBlockLength = blocks[0].Length;
            var secondBlockIndex = firstBlockIndex + firstBlockLength + 1;
            var secondBlockLength = blocks[1].Length;

            const int linesPerBlock = 1000 / 4;
            // \r\n => +2 chars
            var lineLength = line.Length + 2;
            var buffer = new char[lineLength * linesPerBlock];
            Console.WriteLine($"Buffer has a size of: {buffer.Length / 1024}KB");

            for (var j = 0; j < lineLength; j++)
            {
                if (j == lineLength - 2) buffer[j] = '\r';
                else if (j == lineLength - 1) buffer[j] = '\n';
                else buffer[j] = line[j];
            }

            reader.ReadBlock(buffer, lineLength, buffer.Length - lineLength);

            //TODO: use some sort of partitioning or parallel processing
            for (var i = 0; i < DataArea / linesPerBlock; i++)
            {
                if (i != 0) reader.ReadBlock(buffer, 0, buffer.Length);
                
                for (var j = 0; j < linesPerBlock; j++)
                {
                    var height = 0;
                    for (var k = 0; k < firstBlockLength; k++)
                    {
                        height *= 10;
                        height += buffer[j * lineLength + firstBlockIndex + k] - '0';
                    }
                    for (var k = 0; k < secondBlockLength; k++)
                    {
                        height *= 10;
                        height += buffer[j * lineLength + secondBlockIndex + k] - '0';
                    }
                    var lineI = i * linesPerBlock + j;
                    var y = (999 - lineI / 1000) * 1000;
                    data[y + lineI % 1000] = height;
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