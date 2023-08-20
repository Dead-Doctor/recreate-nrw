using OpenTK.Graphics.OpenGL4;
using recreate_nrw.Util;

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
        
        //TODO: allow changing data
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
        
        Shader.Deactivate();
        
        Resources.RegisterDisposable(this);
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
}