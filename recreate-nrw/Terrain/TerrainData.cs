using System.IO.Compression;
using OpenTK.Mathematics;
using recreate_nrw.Util;

namespace recreate_nrw.Terrain;

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

//TODO: do as much asynchronous as possible
public class TerrainData
{
    private const int DataSize = Coordinate.TerrainDataSize;
    private const int DataArea = DataSize * DataSize;
    private const int TileSize = Coordinate.TerrainTileSize;
    private const int TileArea = TileSize * TileSize;

    private Dictionary<Vector2i, int[]> _data = new();
    private Dictionary<Vector2i, Tile> _tiles = new();
    private List<Vector2i> _savedTiles = new();
    
    public Profiler? Profiler;

    public Tile GetTile(Vector2i pos)
    {
        Profiler = new Profiler("GetTile");

        // Check loaded
        if (_tiles.TryGetValue(pos, out var tile)) return tile;

        // Check hard drive
        if (_savedTiles.Contains(pos))
        {
            //TODO: tile = ReadTile(pos);
        }
        else
        {
            // Generate from heightmap
            tile = CreateTile(pos);

            //TODO: do asynchronous
            //TODO: SaveTile(generatedTile);
        }

        _tiles.Add(pos, tile);

        Profiler.StopProfiler();
        return tile;
    }

    private Tile CreateTile(Vector2i pos)
    {
        Profiler?.Start("CreateTile");
        //TODO: check if still works if any variable negative

        var (xStart, startY) = Coordinate.TerrainTileIndex(pos).TerrainData();
        var endPosData = Coordinate.TerrainTileIndex(pos + Vector2i.One).TerrainData();

        var xEnd = endPosData.X;

        var tile = new int[TileArea];

        for (var zOffset = 0; zOffset < TileSize; zOffset += 1)
        {
            var yOffset = -zOffset;
            var y = startY + yOffset;

            var xStartStrip = xStart;
            while (xStartStrip < xEnd)
            {
                var fractionalPartOfDataTile = (xStart % DataSize + 1000) % 1000;
                var stepToNextDataTile = 1000 - fractionalPartOfDataTile;
                // Exclusive
                var xEndStrip = Math.Min(xStartStrip + stepToNextDataTile, xEnd);

                var data = GetData(Coordinate.FloorToInt(new Vector2(xStartStrip, y) / DataSize));

                var dataIndex = OffsetInDataTile(y) * DataSize + OffsetInDataTile(xStartStrip);
                var tileIndex = zOffset * TileSize + (xStartStrip - xStart);
                Array.Copy(data, dataIndex, tile, tileIndex, xEndStrip - xStartStrip);

                xStartStrip = xEndStrip;
            }
        }

        Profiler?.Stop( /*CreateTile*/);
        return new Tile(pos, tile);
    }

    private static int OffsetInDataTile(int pos)
    {
        return (pos % DataSize + DataSize) % DataSize;
    }

    private int[] GetData(Vector2i tile) => _data.TryGetValue(tile, out var data) ? data : LoadData(tile);
    
    /// <summary>
    /// Time (avg for 9x, changes additional to previous):
    /// Line by line: 242ms
    /// Use 'ReadBlock': 214ms
    /// 100 Lines at a time: 196ms
    /// Parse Int manually: 139ms
    /// </summary>
    private int[] LoadData(Vector2i tile)
    {
        Profiler?.Start($"LoadData: ({tile.X}, {tile.Y})");
        var data = new int[DataArea];
        try
        {
            var path = $"Data/Raw/dgm1_32_{tile.X}_{tile.Y}_1_nw.xyz.gz";

            using var stream = File.OpenRead(path);
            using var decompressed = new GZipStream(stream, CompressionMode.Decompress);
            using var reader = new StreamReader(decompressed);


            var line = reader.ReadLine()!;

            var lastSpace = line[..^1].LastIndexOf(' ');
            var blocks = line.Split(' ')[2].Split('.');
            var firstBlockIndex = lastSpace + 1;
            var firstBlockLength = blocks[0].Length;
            var secondBlockIndex = firstBlockIndex + firstBlockLength + 1;
            var secondBlockLength = blocks[1].Length;

            const int linesPerBlock = 100;
            // \r\n => +2 chars
            var lineLength = line.Length + 2;
            var buffer = new char[lineLength * linesPerBlock];

            for (var j = 0; j < lineLength; j++)
            {
                if (j == lineLength - 2) buffer[j] = '\r';
                else if (j == lineLength - 1) buffer[j] = '\n';
                else buffer[j] = line[j];
            }

            reader.ReadBlock(buffer, lineLength, buffer.Length - lineLength);

            //TODO: use some sort of partitioning or parallel processing
            Profiler?.Start("Read lines");
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

            Profiler?.Stop( /*Read lines*/);

            _data.Add(tile, data);
        }
        catch (FileNotFoundException)
        {
            //TODO: Temp
            Console.WriteLine($"[WARNING]: Data tile was not found. {tile}");
        }

        Profiler?.Stop( /*LoadData*/);
        return data;
    }
}

//GL_R32I
public readonly struct Tile
{
    public readonly Vector2i Pos;
    public readonly int[] Data;

    public Tile(Vector2i pos, int[] data)
    {
        Pos = pos;
        Data = data;
    }
}