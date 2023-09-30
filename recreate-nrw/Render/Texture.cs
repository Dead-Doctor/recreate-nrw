using JetBrains.Annotations;
using OpenTK.Graphics.OpenGL4;
using recreate_nrw.Util;
using StbImageSharp;

namespace recreate_nrw.Render;

public abstract class Texture
{
    private static readonly Texture?[] ActiveInstances = new Texture?[TextureUnit.Texture31 - TextureUnit.Texture0 + 1];
    
    [PublicAPI]
    public static Texture Load(string id, LazyTextureData source) =>
        Resources.GetCached(id, Source.Memory, _ => new StaticTexture(source()));

    [PublicAPI]
    public static Texture LoadImageFile(string texturePath, TextureWrapMode textureWrapMode = TextureWrapMode.Repeat) => Load(texturePath, () =>
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

            return new TextureDataBuffer(image.Data, image.Width, image.Height, format, PixelType.UnsignedByte,
                SizedInternalFormat.Rgba8, textureWrapMode, false, true);
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

    protected abstract int GetHandle();
}

public class StaticTexture : Texture, IDisposable
{
    public readonly int Handle;
    
    public StaticTexture(ITextureData textureData)
    {
        GL.CreateTextures(TextureTarget.Texture2D, 1, out Handle);

        var minFilter = textureData.NearestFiltering
            ? textureData.Mipmaps ? TextureMinFilter.NearestMipmapNearest : TextureMinFilter.Nearest
            : textureData.Mipmaps
                ? TextureMinFilter.LinearMipmapLinear
                : TextureMinFilter.Linear;
        var magFilter = textureData.NearestFiltering ? TextureMagFilter.Nearest : TextureMagFilter.Linear;

        GL.TextureParameter(Handle, TextureParameterName.TextureMinFilter, (int) minFilter);
        GL.TextureParameter(Handle, TextureParameterName.TextureMagFilter, (int) magFilter);

        GL.TextureParameter(Handle, TextureParameterName.TextureWrapS, (int) textureData.WrapMode);
        GL.TextureParameter(Handle, TextureParameterName.TextureWrapT, (int) textureData.WrapMode);

        var levels = 1;
        if (textureData.Mipmaps)
            levels += (int) Math.Floor(Math.Log2(Math.Max(textureData.Width, textureData.Height)));

        GL.TextureStorage2D(Handle, levels, textureData.InternalFormat, textureData.Width, textureData.Height);
        if (textureData is TextureDataBuffer textureDataBuffer)
        {
            GL.TextureSubImage2D(Handle, 0, 0, 0, textureDataBuffer.Width, textureDataBuffer.Height,
                textureDataBuffer.Format,
                textureDataBuffer.Type, textureDataBuffer.Data);
        }

        if (textureData.Mipmaps)
            GenerateMipmap();
        
        Resources.RegisterDisposable(this);
    }

    public void GenerateMipmap()
    {
        GL.GenerateTextureMipmap(Handle);
    }

    protected override int GetHandle() => Handle;

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

public delegate ITextureData LazyTextureData();

public interface ITextureData
{
    int Width { get; }
    int Height { get; }
    SizedInternalFormat InternalFormat { get; }
    TextureWrapMode WrapMode { get; }
    bool NearestFiltering { get; }
    bool Mipmaps { get; }
}

public readonly record struct TextureDataFramebufferAttachment(int Width, int Height,
    SizedInternalFormat InternalFormat, TextureWrapMode WrapMode, bool NearestFiltering, bool Mipmaps) : ITextureData;

public readonly record struct TextureDataBuffer(byte[] Data, int Width, int Height, PixelFormat Format, PixelType Type,
    SizedInternalFormat InternalFormat, TextureWrapMode WrapMode, bool NearestFiltering, bool Mipmaps) : ITextureData;