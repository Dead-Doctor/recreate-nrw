using OpenTK.Graphics.OpenGL4;
using recreate_nrw.Util;

namespace recreate_nrw.Render;

public class ShadedModel : IDisposable
{
    private static ShadedModel? _activeInstance;

    private readonly Model _model;
    public readonly Shader Shader;
    
    private readonly BufferUsageAccessFrequency _frequency;
    private readonly BufferUsageAccessNature _nature;

    private readonly int _vao;
    private readonly int _vbo;
    private readonly int _ebo;

    public ShadedModel(Model model, Shader shader, BufferUsageAccessFrequency frequency, BufferUsageAccessNature nature)
    {
        _model = model;
        Shader = shader;
        
        _frequency = frequency;
        _nature = nature;
        
        Shader.Activate();

        GL.CreateVertexArrays(1, out _vao);
        GL.CreateBuffers(1, out _vbo);
        GL.CreateBuffers(1, out _ebo);

        var bufferUsageHint = GetBufferUsage(frequency, nature);
        GL.NamedBufferData(_vbo, _model.GetVertexSize * _model.VertexCount, _model.Vertices, bufferUsageHint);
        GL.NamedBufferData(_ebo, _model.Indices.Length * sizeof(uint), _model.Indices, bufferUsageHint);
        _model.Dirty = false;

        const int bindingIndex = 0;

        var offset = 0;
        foreach (var attribute in _model.VertexAttributes)
        {
            var attribIndex = Shader.GetAttribLocation(attribute.Name);
            if (attribIndex == -1)
                throw new ArgumentException($"Could not find vertex attribute '{attribute.Name}' in shader ({Shader}). Maybe uniform got optimized away!!!!!!");
            GL.EnableVertexArrayAttrib(_vao, attribIndex);
            GL.VertexArrayAttribBinding(_vao, attribIndex, bindingIndex);
            GL.VertexArrayAttribFormat(_vao, attribIndex, attribute.Count, attribute.Type, attribute.Normalized,
                offset);
            offset += attribute.GetSize();
        }

        GL.VertexArrayVertexBuffer(_vao, bindingIndex, _vbo, (IntPtr)0, _model.GetVertexSize);
        GL.VertexArrayElementBuffer(_vao, _ebo);

        Shader.Deactivate();

        Resources.RegisterDisposable(this);
    }

    private void RefreshModel()
    {
        if (_frequency != BufferUsageAccessFrequency.Dynamic)
            throw new InvalidOperationException($"Can't change model data since buffer usage access frequency is {_frequency}.");
        if (!_model.Dirty)
            Console.WriteLine("[WARNING]: Tried to refresh model but its data is unchanged.");
        _model.Dirty = false;
        
        GL.NamedBufferSubData(_vbo, IntPtr.Zero, _model.GetVertexSize * _model.VertexCount, _model.Vertices);
        GL.NamedBufferSubData(_ebo, IntPtr.Zero, _model.Indices.Length * sizeof(uint), _model.Indices);
    }

    public void Draw()
    {
        if (_model.Dirty) RefreshModel();
        
        Activate();
        GL.DrawElements(PrimitiveType.Triangles, _model.Indices.Length, DrawElementsType.UnsignedInt, 0);
        Deactivate();
    }

    public void DrawInstanced(int count)
    {
        if (_model.Dirty) RefreshModel();
        
        Activate();
        GL.DrawElementsInstanced(PrimitiveType.Triangles, _model.Indices.Length, DrawElementsType.UnsignedInt,
            (IntPtr)0, count);
        Deactivate();
    }

    private void Activate()
    {
        if (_activeInstance == this) return;
        _activeInstance = this;

        Shader.Activate();
        GL.BindVertexArray(_vao);
    }

    private void Deactivate()
    {
        if (_activeInstance != this) return;
        _activeInstance = null;

        GL.BindVertexArray(0);
        Shader.Deactivate();
    }

    private bool _disposedValue;

    public void Dispose()
    {
        if (_disposedValue) return;
        GC.SuppressFinalize(this);

        GL.DeleteBuffer(_vbo);
        GL.DeleteBuffer(_ebo);

        GL.BindVertexArray(0);
        GL.DeleteVertexArray(_vao);

        _disposedValue = true;
    }

    // Finalizer may not be called at all
    ~ShadedModel()
    {
        if (_disposedValue) return;
        Console.WriteLine("GPU Resource leak! Did you forget to call Dispose() on ShadedModel?");
    }

    private static BufferUsageHint GetBufferUsage(BufferUsageAccessFrequency frequency, BufferUsageAccessNature nature)
        => (BufferUsageHint)((int)BufferUsageHint.StreamDraw + (int)frequency * 4 + (int)nature);
}

public enum BufferUsageAccessFrequency
{
    Stream,
    Static,
    Dynamic
}

public enum BufferUsageAccessNature
{
    Draw,
    Read,
    Copy
}