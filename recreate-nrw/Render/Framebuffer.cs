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
    public int Width;
    public int Height;
    private readonly bool _resizable;
    private readonly bool _nearestFiltering;
    private readonly bool _mipmaps;

    public Framebuffer(int width, int height, bool resizable, bool nearestFiltering, bool mipmaps)
    {
        Width = width;
        Height = height;
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

    private void CreateAttachments()
    {
        //TODO: optionally attach depth and stencil (render)buffer for 3d rendering
        
        if (_texture != null) Resources.Dispose(_texture);
        _texture = CreateAttachment(new TextureEmptyBuffer(Width, Height,
            SizedInternalFormat.Rgba8, TextureWrapMode.ClampToEdge, _nearestFiltering, _mipmaps));
        GL.NamedFramebufferTexture(_handle, FramebufferAttachment.ColorAttachment0, _texture.Handle, 0);
    }

    public void Resize(int width, int height)
    {
        if (!_resizable)
            throw new InvalidOperationException("Framebuffer is not resizable.");
        Width = width;
        Height = height;
        CreateAttachments();
    }
    
    public delegate void DrawToBuffer();

    public void Use(DrawToBuffer callback)
    {
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _handle);
        var oldViewport = Renderer.Viewport;
        Renderer.Viewport = new Box2i(0, 0, Width, Height);
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