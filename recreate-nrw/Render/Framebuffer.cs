using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using recreate_nrw.Util;

namespace recreate_nrw.Render;

public class Framebuffer : Texture, IDisposable
{
    private static int _framebufferAttachments;

    private static StaticTexture CreateAttachment(TextureDataFramebufferAttachment data) =>
        Resources.GetCached($"framebufferAttachment{_framebufferAttachments++}", Source.Memory,
            _ => new StaticTexture(data));

    private readonly int _handle;
    private readonly StaticTexture _texture;
    private readonly int _width;
    private readonly int _height;
    private readonly bool _mipmaps;

    public Framebuffer(int width, int height, bool nearestFiltering, bool mipmaps)
    {
        _width = width;
        _height = height;
        _mipmaps = mipmaps;

        GL.CreateFramebuffers(1, out _handle);

        _texture = CreateAttachment(new TextureDataFramebufferAttachment(width, height,
            SizedInternalFormat.Rgba8, TextureWrapMode.ClampToEdge, nearestFiltering, mipmaps));
        GL.NamedFramebufferTexture(_handle, FramebufferAttachment.ColorAttachment0, _texture.Handle, 0);

        //TODO: optionally attach depth and stencil (render)buffer for 3d rendering

        var framebufferStatus = GL.CheckNamedFramebufferStatus(_handle, FramebufferTarget.Framebuffer);
        if (framebufferStatus != FramebufferStatus.FramebufferComplete)
            throw new Exception($"Framebuffer creation unsuccessful ({framebufferStatus}).");

        Resources.RegisterDisposable(this);
    }

    public delegate void DrawToBuffer();

    public void Use(DrawToBuffer callback)
    {
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _handle);
        var oldViewport = Renderer.Viewport;
        Renderer.Viewport = new Box2i(0, 0, _width, _height);
        callback();
        Renderer.Viewport = oldViewport;
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        if (_mipmaps) _texture.GenerateMipmap();
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

    protected override int GetHandle() => _texture.Handle;
}