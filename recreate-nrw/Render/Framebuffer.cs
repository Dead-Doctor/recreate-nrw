using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using recreate_nrw.Util;

namespace recreate_nrw.Render;

public class Framebuffer : Texture, IDisposable
{
    private static int _framebufferAttachments;

    private static StaticTexture CreateAttachment(TextureEmptyBuffer data) =>
        Resources.GetCached($"framebufferAttachment{_framebufferAttachments++}", Source.Memory,
            _ => new StaticTexture(data));
    
    
    private readonly int _handle;
    private StaticTexture? _texture;
    private Vector2i _size;
    private readonly bool _resizable;
    private readonly bool _nearestFiltering;
    private readonly bool _mipmaps;

    public Framebuffer(Vector2i size, bool resizable, bool nearestFiltering, bool mipmaps)
    {
        _size = size;
        _resizable = resizable;
        _nearestFiltering = nearestFiltering;
        _mipmaps = mipmaps;

        GL.CreateFramebuffers(1, out _handle);

        CreateAttachments();

        var framebufferStatus = GL.CheckNamedFramebufferStatus(_handle, FramebufferTarget.Framebuffer);
        if (framebufferStatus != FramebufferStatus.FramebufferComplete)
            throw new Exception($"Framebuffer creation unsuccessful ({framebufferStatus}).");

        Resources.RegisterDisposable(this);
    }

    public Vector2i Size
    {
        get => _size;
        set {
            if (!_resizable)
                throw new InvalidOperationException("Framebuffer is not resizable.");
            if (_size == value) return;
            _size = value;
            CreateAttachments();
        }
    }

    private void CreateAttachments()
    {
        //TODO: optionally attach depth and stencil (render)buffer for 3d rendering
        
        if (_texture != null) Resources.Dispose(_texture);
        _texture = CreateAttachment(new TextureEmptyBuffer(_size,SizedInternalFormat.Rgba8, TextureWrapMode.ClampToEdge, _nearestFiltering, _mipmaps));
        GL.NamedFramebufferTexture(_handle, FramebufferAttachment.ColorAttachment0, _texture.Handle, 0);
    }
    
    public delegate void DrawToBuffer();

    public void Use(DrawToBuffer callback)
    {
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _handle);
        var oldViewport = Renderer.Viewport;
        Renderer.Viewport = new Box2i(Vector2i.Zero, _size);
        callback();
        Renderer.Viewport = oldViewport;
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        if (_mipmaps) _texture!.GenerateMipmap();
    }

    private bool _disposedValue;

    public void Dispose()
    {
        if (_disposedValue) return;
        GC.SuppressFinalize(this);
        GL.DeleteFramebuffer(_handle);
        _disposedValue = true;
    }

    // Finalizer may not be called at all
    ~Framebuffer()
    {
        if (_disposedValue) return;
        Console.WriteLine("GPU Resource leak! Did you forget to call Dispose() on Framebuffer?");
    }
    public override int GetHandle() => _texture!.Handle;
}