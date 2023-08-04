using OpenTK.Graphics.OpenGL4;

namespace recreate_nrw.Render;

public class ShadedModel : IDisposable
{
    private static ShadedModel? _activeInstance;
    
    private readonly Model _model;
    public readonly Shader Shader;
    
    private readonly int _vao;
    private readonly int _vbo;
    private readonly int _ebo;

    public ShadedModel(Model model, Shader shader)
    {
        _model = model;
        Shader = shader;
        Shader.Activate();
        
        GL.CreateVertexArrays(1, out _vao);
        GL.CreateBuffers(1, out _vbo);
        GL.CreateBuffers(1, out _ebo);
        
        GL.NamedBufferData(_vbo, _model.GetVertexSize * _model.VertexCount, _model.Vertices, BufferUsageHint.StaticDraw);
        GL.NamedBufferData(_ebo, _model.Indices.Length * sizeof(uint), _model.Indices, BufferUsageHint.StaticDraw);

        const int bindingIndex = 0;

        var offset = 0;
        foreach (var attribute in _model.VertexAttributes)
        {
            var attribIndex = Shader.GetAttribLocation(attribute.Name);
            GL.EnableVertexArrayAttrib(_vao, attribIndex);
            GL.VertexArrayAttribBinding(_vao, attribIndex, bindingIndex);
            GL.VertexArrayAttribFormat(_vao, attribIndex, attribute.Count, attribute.Type, attribute.Normalized, offset);
            offset += attribute.GetSize();
        }
        
        GL.VertexArrayVertexBuffer(_vao, bindingIndex, _vbo, (IntPtr)0, _model.GetVertexSize);
        GL.VertexArrayElementBuffer(_vao, _ebo);
        
        /*_vao = GL.GenVertexArray();
        GL.BindVertexArray(_vao);

        _vbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, _model.GetVertexSize * _model.VertexCount, _model.Vertices, BufferUsageHint.StaticDraw);
        
        _ebo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebo);
        GL.BufferData(BufferTarget.ElementArrayBuffer, _model.Indices.Length * sizeof(uint), _model.Indices, BufferUsageHint.StaticDraw);

        
        var stride = _model.GetVertexSize;
        var offset = 0;
        for (var i = 0; i < _model.VertexAttributes.Count; i++)
        {
            var attribute = _model.VertexAttributes[i];
            GL.VertexAttribPointer(i, attribute.Count, attribute.Type, attribute.Normalized, stride, offset);
            offset += attribute.GetSize();
            GL.EnableVertexAttribArray(Shader.GetAttribLocation(attribute.Name));
        }*/
        
        Shader.Deactivate();
    }

    public void Draw()
    {
        Activate();
        GL.DrawElements(PrimitiveType.Triangles, _model.Indices.Length, DrawElementsType.UnsignedInt, 0);
        Deactivate();
    }

    public void DrawInstanced(int count)
    {
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

        //TODO: Usage of DSA
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        GL.DeleteBuffer(_vbo);

        GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
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
}