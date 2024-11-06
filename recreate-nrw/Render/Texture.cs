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
        Resources.GetCached(id, Source.Memory, _ => StaticTexture.CreateFrom(source()));

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

                return (
                    new TextureInfo2D(SizedInternalFormat.Rgba8, new Vector2i(image.Width, image.Height), textureWrapMode, false, true),
                    new TextureData2D(image.Data, format, PixelType.UnsignedByte)
                );
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
    public int Handle;
    private ITextureInfo _info = null!;

    private StaticTexture()
    {
    }

    public static StaticTexture CreateFrom((ITextureInfo, ITextureData) texture) =>
        CreateFrom(texture.Item1, texture.Item2);
    
    public static StaticTexture CreateFrom(ITextureInfo textureInfo, ITextureData? textureData = null)
    {
        var t = new StaticTexture();
        t._info = textureInfo;
        
        var target = textureInfo switch
        {
            TextureInfo1D => TextureTarget.Texture1D,
            TextureInfo2D => TextureTarget.Texture2D,
            TextureInfo2DArray => TextureTarget.Texture2DArray,
            _ => throw new ArgumentException("Invalid texture target", nameof(textureInfo))
        };
        GL.CreateTextures(target, 1, out t.Handle);

        var levels = 1;

        if (textureInfo is ITextureInfo2DBase textureInfo2D)
        {
            levels += (int)MathF.Floor(MathF.Log2(Math.Max(textureInfo2D.Size.X, textureInfo2D.Size.Y)));

            var minFilter = textureInfo2D.NearestFiltering
                ? textureInfo2D.Mipmaps ? TextureMinFilter.NearestMipmapNearest : TextureMinFilter.Nearest
                : textureInfo2D.Mipmaps
                    ? TextureMinFilter.LinearMipmapLinear
                    : TextureMinFilter.Linear;
            var magFilter = textureInfo2D.NearestFiltering ? TextureMagFilter.Nearest : TextureMagFilter.Linear;

            GL.TextureParameter(t.Handle, TextureParameterName.TextureMinFilter, (int)minFilter);
            GL.TextureParameter(t.Handle, TextureParameterName.TextureMagFilter, (int)magFilter);

            GL.TextureParameter(t.Handle, TextureParameterName.TextureWrapS, (int)textureInfo2D.WrapMode);
            GL.TextureParameter(t.Handle, TextureParameterName.TextureWrapT, (int)textureInfo2D.WrapMode);
        }

        switch (textureInfo)
        {
            case TextureInfo1D texture1D:
                GL.TextureStorage1D(t.Handle, levels, textureInfo.InternalFormat, texture1D.Size);
                break;
            case TextureInfo2D texture2D:
                GL.TextureStorage2D(t.Handle, levels, textureInfo.InternalFormat, texture2D.Size.X, texture2D.Size.Y);
                break;
            case TextureInfo2DArray texture2DArray:
                GL.TextureStorage3D(t.Handle, levels, textureInfo.InternalFormat, texture2DArray.Size.X,
                    texture2DArray.Size.Y, texture2DArray.LayerCount);
                break;
        }

        if (textureData is not null)
            t.UploadImageData(textureData);

        Resources.RegisterDisposable(t);
        return t;
    }

    public void UploadImageData(ITextureData textureData)
    {
        dynamic data = textureData.Data ??
                       throw new ArgumentException("Supplied texture data was null.", nameof(textureData));
        switch (textureData)
        {
            case TextureData1D texture1D:
            {
                var offset = texture1D.Offset ?? 0;
                var size = texture1D.Size ?? ((TextureInfo1D)_info).Size - offset;
                GL.TextureSubImage1D(Handle, 0, offset, size,
                    textureData.Format, textureData.Type, data);
                break;
            }
            case TextureData2D texture2D:
            {
                var offset = texture2D.Offset ?? Vector2i.Zero;
                var size = texture2D.Size ?? ((TextureInfo2D)_info).Size - offset;
                GL.TextureSubImage2D(Handle, 0, offset.X, offset.Y, size.X, size.Y,
                    textureData.Format, textureData.Type, data);
                break;
            }
            case TextureData2DArray texture2DArray:
            {
                var offset = texture2DArray.Offset ?? Vector2i.Zero;
                var layerStart = texture2DArray.LayerStart ?? 0;
                var size = texture2DArray.Size ?? ((TextureInfo2DArray)_info).Size - offset;
                var layerCount = texture2DArray.LayerCount ?? ((TextureInfo2DArray)_info).LayerCount - layerStart;
                GL.TextureSubImage3D(Handle, 0, offset.X, offset.Y, layerStart, size.X, size.Y, layerCount,
                    textureData.Format, textureData.Type, data);
                break;
            }
        }
        
        if (_info is ITextureInfo2DBase { Mipmaps: true })
            GenerateMipmap();
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

public delegate (ITextureInfo, ITextureData) LazyTextureData();

public interface ITextureInfo
{
    SizedInternalFormat InternalFormat { get; }
}

public interface ITextureInfo1D : ITextureInfo
{
    int Size { get; }
}

public interface ITextureInfo2DBase : ITextureInfo
{
    Vector2i Size { get; }
    TextureWrapMode WrapMode { get; }

    bool NearestFiltering { get; }
    bool Mipmaps { get; }
}

public interface ITextureInfo2D : ITextureInfo2DBase
{
}

public interface ITextureInfo2DArray : ITextureInfo2DBase
{
    int LayerCount { get; }
}

public interface ITextureData
{
    Array Data { get; }
    PixelFormat Format { get; }
    PixelType Type { get; }
}

public interface ITextureData1D : ITextureData
{
    int? Offset { get; }
    int? Size { get; }
}

public interface ITextureData2DBase : ITextureData
{
    Vector2i? Offset { get; }
    Vector2i? Size { get; }
}

public interface ITextureData2D : ITextureData2DBase
{
}

public interface ITextureData2DArray : ITextureData2DBase
{
    int? LayerStart { get; }
    int? LayerCount { get; }
}

public readonly record struct TextureInfo1D(
    SizedInternalFormat InternalFormat,
    int Size) : ITextureInfo1D;

public readonly record struct TextureInfo2D(
    SizedInternalFormat InternalFormat,
    Vector2i Size,
    TextureWrapMode WrapMode,
    bool NearestFiltering,
    bool Mipmaps) : ITextureInfo2D;

public readonly record struct TextureInfo2DArray(
    SizedInternalFormat InternalFormat,
    Vector2i Size,
    int LayerCount,
    TextureWrapMode WrapMode,
    bool NearestFiltering,
    bool Mipmaps) : ITextureInfo2DArray;

public readonly record struct TextureData1D(
    Array Data,
    PixelFormat Format,
    PixelType Type,
    int? Offset = null,
    int? Size = null) : ITextureData1D;

public readonly record struct TextureData2D(
    Array Data,
    PixelFormat Format,
    PixelType Type,
    Vector2i? Offset = null,
    Vector2i? Size = null) : ITextureData2D;

public readonly record struct TextureData2DArray(
    Array Data,
    PixelFormat Format,
    PixelType Type,
    Vector2i? Offset = null,
    int? LayerStart = null,
    Vector2i? Size = null,
    int? LayerCount = null) : ITextureData2DArray;