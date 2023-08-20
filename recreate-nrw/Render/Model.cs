using OpenTK.Graphics.OpenGL4;
using Buffer = System.Buffer;

namespace recreate_nrw.Render;

public class Model
{
    public readonly byte[] Vertices;
    public readonly List<VertexAttribute> VertexAttributes = new();
    public readonly uint[] Indices;

    //TODO: load from file (using resource manager)
    public static Model FromArray<T>(T[] vertices, uint[] indices)
    {
        var bytes = new byte[Buffer.ByteLength(vertices)];
        Buffer.BlockCopy(vertices, 0, bytes, 0, bytes.Length);
        return new Model(bytes, indices);
    }

    private Model(byte[] vertices, uint[] indices)
    {
        Vertices = vertices;
        Indices = indices;
    }

    public int VertexCount
    {
        get
        {
            if (GetVertexSize == 0)
                throw new ArgumentException(
                    "Could not calculate vertex count because vertex size is 0. Maybe forgot to define vertex attributes?");
            return sizeof(byte) * Vertices.Length / GetVertexSize;
        }
    }

    public int GetVertexSize => VertexAttributes.Sum(a => a.GetSize());

    public void AddVertexAttribute(VertexAttribute attribute)
    {
        VertexAttributes.Add(attribute);
    }
}

public readonly struct VertexAttribute
{
    public readonly string Name;
    public readonly VertexAttribType Type;
    public readonly int Count;
    public readonly bool Normalized;

    public VertexAttribute(string name, VertexAttribType type, int count, bool normalized = false)
    {
        Name = name;
        Type = type;
        Count = count;
        Normalized = normalized;
    }

    public int GetSize()
    {
        // ReSharper disable once SwitchExpressionHandlesSomeKnownEnumValuesWithExceptionInDefault
        return Count * Type switch
        {
            VertexAttribType.Byte => sizeof(sbyte),
            VertexAttribType.UnsignedByte => sizeof(byte),
            VertexAttribType.Short => sizeof(short),
            VertexAttribType.UnsignedShort => sizeof(ushort),
            VertexAttribType.Int => sizeof(int),
            VertexAttribType.UnsignedInt => sizeof(uint),
            VertexAttribType.Float => sizeof(float),
            VertexAttribType.Double => sizeof(double),
            _ => throw new Exception($"Unknown type: {Type}")
        };
    }
}