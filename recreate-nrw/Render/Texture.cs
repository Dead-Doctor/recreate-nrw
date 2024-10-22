using JetBrains.Annotations;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using recreate_nrw.Util;
using StbImageSharp;

namespace recreate_nrw.Render;

/// <summary>
/// The Texture class represents any kind of OpenGL-object which has a texture handle and can be used as one.
/// At the moment this is either an OpenGl-Texture or an OpenGL-Framebuffer
/// </summary>
public abstract class Texture
{
    private static readonly Texture?[] ActiveInstances = new Texture?[TextureUnit.Texture31 - TextureUnit.Texture0 + 1];

    /// <summary>
    /// Creates an OpenGL-Texture from the lazy texture source and caches it but only if this texture has not been created before
    /// </summary>
    /// <param name="id">Unique identifier</param>
    /// <param name="source">Lazy loaded texture source</param>
    /// <returns>The created or retrieved texture object</returns>
    [PublicAPI]
    public static Texture Load(string id, LazyTextureData source) =>
        Resources.GetCached(id, Source.Memory, _ => new StaticTexture(source()));

    /// <summary>
    /// Creates an OpenGL-Texture from the image file and caches it but only if this image has not been loaded before
    /// </summary>
    /// <param name="texturePath">Path to image file</param>
    /// <param name="textureWrapMode">The behaviour of the texture if sampled outside the zero to one range</param>
    /// <returns>The created or retrieved texture object</returns>
    /// <exception cref="ArgumentException">Gets thrown when the image has an invalid color format</exception>
    /// <exception cref="ArgumentOutOfRangeException">Gets thrown when the image has an unimplemented color format</exception>
    [PublicAPI]
    public static Texture LoadImageFile(string texturePath, TextureWrapMode textureWrapMode = TextureWrapMode.Repeat) =>
        Load(texturePath, () =>
            Resources.GetCached(texturePath, Source.WorkingDirectory, stream =>
            {
                StbImage.stbi_set_flip_vertically_on_load(1);

                var image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);

                var format = image.Comp switch
                {
                    ColorComponents.Default => throw new ArgumentException(
                        "Tried to load texture with color components: Default."),
                    ColorComponents.Grey => PixelFormat.Luminance,
                    ColorComponents.GreyAlpha => PixelFormat.LuminanceAlpha,
                    ColorComponents.RedGreenBlue => PixelFormat.Rgb,
                    ColorComponents.RedGreenBlueAlpha => PixelFormat.Rgba,
                    _ => throw new ArgumentOutOfRangeException(image.Comp.ToString())
                };

                return new TextureInfo2D(new TextureData(image.Data, format, PixelType.UnsignedByte),
                    SizedInternalFormat.Rgba8, new Vector2i(image.Width, image.Height), textureWrapMode, false, true);
            }));

    public void Activate(int i)
    {
        if (ActiveInstances[i] == this) return;
        ActiveInstances[i] = this;
        GL.BindTextureUnit(i, GetHandle());
    }

    public void Deactivate()
    {
        var i = Array.IndexOf(ActiveInstances, this);
        if (i == -1) return;
        ActiveInstances[i] = null;
        GL.BindTextureUnit(i, 0);
    }

    public abstract int GetHandle();
}

public class StaticTexture : Texture, IDisposable
{
    public readonly int Handle;

    public StaticTexture(ITextureInfo textureInfo)
    {
        var target = textureInfo switch
        {
            ITextureInfo1D => TextureTarget.Texture1D,
            ITextureInfo2D => TextureTarget.Texture2D,
            _ => throw new ArgumentException("Invalid texture target", nameof(textureInfo))
        };
        GL.CreateTextures(target, 1, out Handle);

        switch (textureInfo)
        {
            case ITextureInfo1D texture1D:
                GL.TextureStorage1D(Handle, 1, textureInfo.InternalFormat, texture1D.Size);
                break;
            case ITextureInfo2D texture2D:
            {
                var minFilter = texture2D.NearestFiltering
                    ? texture2D.Mipmaps ? TextureMinFilter.NearestMipmapNearest : TextureMinFilter.Nearest
                    : texture2D.Mipmaps
                        ? TextureMinFilter.LinearMipmapLinear
                        : TextureMinFilter.Linear;
                var magFilter = texture2D.NearestFiltering ? TextureMagFilter.Nearest : TextureMagFilter.Linear;

                GL.TextureParameter(Handle, TextureParameterName.TextureMinFilter, (int)minFilter);
                GL.TextureParameter(Handle, TextureParameterName.TextureMagFilter, (int)magFilter);

                GL.TextureParameter(Handle, TextureParameterName.TextureWrapS, (int)texture2D.WrapMode);
                GL.TextureParameter(Handle, TextureParameterName.TextureWrapT, (int)texture2D.WrapMode);

                var levels = 1;
                if (texture2D.Mipmaps)
                    levels += (int)Math.Floor(Math.Log2(Math.Max(texture2D.Size.X, texture2D.Size.Y)));

                GL.TextureStorage2D(Handle, levels, textureInfo.InternalFormat, texture2D.Size.X, texture2D.Size.Y);
                break;
            }
        }

        if (textureInfo.Data is not null)
            UploadImageData(textureInfo);

        Resources.RegisterDisposable(this);
    }

    public void UploadImageData(ITextureInfo textureData)
    {
        var data = textureData.Data ?? throw new ArgumentException("Supplied texture data was null.", nameof(textureData));
        dynamic dynamicData = data.Data;
        switch (textureData)
        {
            case ITextureInfo1D texture1D:
                GL.TextureSubImage1D(Handle, 0, 0, texture1D.Size,
                    data.Format, data.Type, dynamicData);
                break;
            case ITextureInfo2D texture2D:
                GL.TextureSubImage2D(Handle, 0, 0, 0, texture2D.Size.X, texture2D.Size.Y,
                    data.Format, data.Type, dynamicData);
                if (texture2D.Mipmaps)
                    GenerateMipmap();
                break;
        }
    }

    public void GenerateMipmap()
    {
        GL.GenerateTextureMipmap(Handle);
    }

    public override int GetHandle() => Handle;

    private bool _disposedValue;

    public void Dispose()
    {
        if (_disposedValue) return;
        GC.SuppressFinalize(this);

        GL.DeleteTexture(Handle);
        _disposedValue = true;
    }

    // Finalizer may not be called at all
    ~StaticTexture()
    {
        if (_disposedValue) return;
        Console.WriteLine("GPU Resource leak! Did you forget to call Dispose() on Texture?");
    }
}

public delegate ITextureInfo LazyTextureData();

public interface ITextureInfo
{
    TextureData? Data { get; }
    SizedInternalFormat InternalFormat { get; }
}

public interface ITextureInfo1D : ITextureInfo
{
    int Size { get; }
}

public interface ITextureInfo2D : ITextureInfo
{
    Vector2i Size { get; }
    TextureWrapMode WrapMode { get; }
    
    bool NearestFiltering { get; }
    bool Mipmaps { get; }
}

public readonly record struct TextureInfo1D(
    TextureData? Data,
    SizedInternalFormat InternalFormat,
    int Size) : ITextureInfo1D;

public readonly record struct TextureInfo2D(
    TextureData? Data,
    SizedInternalFormat InternalFormat,
    Vector2i Size,
    TextureWrapMode WrapMode,
    bool NearestFiltering,
    bool Mipmaps) : ITextureInfo2D;

public readonly record struct TextureData(
    Array Data,
    PixelFormat Format,
    PixelType Type);