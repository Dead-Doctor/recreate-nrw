using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using recreate_nrw.Render;
using Vector3 = OpenTK.Mathematics.Vector3;

namespace recreate_nrw.Terrain;

public class TerrainModel
{
    private readonly Vector2i _origin;
    private readonly uint _size;
    public readonly Model Model;

    /// <summary>
    /// Generate a model from a heightmap. The data directly correlates to the positions of the vertices.
    /// </summary>
    /// <param name="heightmap">The height data to use.</param>
    /// <param name="origin">Top left position of this model.</param>
    /// <param name="size">The width and height of this model.</param>
    public TerrainModel(Heightmap heightmap, Vector2i origin, uint size)
    {
        _origin = origin;
        _size = size;

        var indices = new uint[(size - 1) * (size - 1) * 2 * 3];
        var normals = new Vector3[size * size];

        for (var worldZ = 0; worldZ < size - 1; worldZ++)
        {
            for (var worldX = 0; worldX < size - 1; worldX++)
            {
                var topLeft = origin + new Vector2i(worldX, worldZ);
                var topRight = origin + new Vector2i(worldX + 1, worldZ);
                var bottomLeft = origin + new Vector2i(worldX, worldZ + 1);
                var bottomRight = origin + new Vector2i(worldX + 1, worldZ + 1);

                {
                    var i = ((worldZ * (size - 1) + worldX) * 2 + 0) * 3;
                    indices[i + 0] = Index(topLeft);
                    indices[i + 1] = Index(topRight);
                    indices[i + 2] = Index(bottomRight);
                    var normal = Vector3.Cross(
                        heightmap[bottomRight] - heightmap[topLeft],
                        heightmap[topRight] - heightmap[topLeft]
                    ).Normalized();
                    normals[indices[i + 0]] += normal;
                    normals[indices[i + 1]] += normal;
                    normals[indices[i + 2]] += normal;
                }
                {
                    var i = ((worldZ * (size - 1) + worldX) * 2 + 1) * 3;
                    indices[i + 0] = Index(topLeft);
                    indices[i + 1] = Index(bottomRight);
                    indices[i + 2] = Index(bottomLeft);
                    var normal = Vector3.Cross(
                        heightmap[bottomLeft] - heightmap[topLeft],
                        heightmap[bottomRight] - heightmap[topLeft]
                    ).Normalized();
                    normals[indices[i + 0]] += normal;
                    normals[indices[i + 1]] += normal;
                    normals[indices[i + 2]] += normal;
                }
            }
        }

        var vertices = new float[size * size * (3 + 3)];
        for (var z = 0; z < size; z++)
        {
            for (var x = 0; x < size; x++)
            {
                var pos = origin + new Vector2i(x, z);
                var i = Index(pos);

                var position = heightmap[pos];
                var normal = normals[i].Normalized();

                vertices[i * (3 + 3) + 0] = position.X;
                vertices[i * (3 + 3) + 1] = position.Y;
                vertices[i * (3 + 3) + 2] = position.Z;
                vertices[i * (3 + 3) + 3] = normal.X;
                vertices[i * (3 + 3) + 4] = normal.Y;
                vertices[i * (3 + 3) + 5] = normal.Z;
            }
        }

        Model = Model.FromArray(vertices, indices);
        Model.AddVertexAttribute(new VertexAttribute("aPosition", VertexAttribType.Float, 3));
        Model.AddVertexAttribute(new VertexAttribute("aNormal", VertexAttribType.Float, 3));
        //TODO: Enable back face culling
    }

    private uint Index(Vector2i pos)
    {
        var index = pos - _origin;
        return (uint) (index.Y * _size + index.X);
    }
}