using OpenTK.Graphics.OpenGL4;
using StbImageSharp;

namespace recreate_nrw.Render;

public class Texture : IDisposable
{
    private static Texture?[] _activeInstances = new Texture?[TextureUnit.Texture31 - TextureUnit.Texture0 + 1];

    private readonly int _handle;

    private static readonly Dictionary<string, Texture> LoadedTextures = new();

    public static Texture Load(string id, Func<string, TextureData> source)
    {
        if (LoadedTextures.TryGetValue(id, out var texture)) return texture;

        var loadedTexture = new Texture(source(id));

        LoadedTextures.Add(id, loadedTexture);
        return loadedTexture;
    }

    //TODO: Resource Manager (https://stackoverflow.com/questions/3314140/how-to-read-embedded-resource-text-file)
    public static Texture LoadImageFile(string texturePath) =>
        Load($"data:{texturePath}", ReadImageFile);

    private static TextureData ReadImageFile(string texturePath)
    {
        if (!texturePath.StartsWith("data:"))
            throw new ArgumentException($"Image file ids have to start with 'data:'. Tried to load: {texturePath}");
        texturePath = texturePath["data:".Length..];

        StbImage.stbi_set_flip_vertically_on_load(1);

        using var stream = File.OpenRead(texturePath) ??
                           throw new ArgumentNullException($"Could'nt load texture file: {texturePath}");
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


        return new TextureData(image.Data, image.Width, image.Height, format, PixelType.UnsignedByte,
            SizedInternalFormat.Rgba8, TextureWrapMode.Repeat, false, true);
    }

    public static void DisposeAll()
    {
        foreach (var texture in LoadedTextures)
        {
            texture.Value.Dispose();
        }

        LoadedTextures.Clear();
    }

    private Texture(TextureData textureData)
    {
        GL.CreateTextures(TextureTarget.Texture2D, 1, out _handle);

        var minFilter = textureData.NearestFiltering
            ? textureData.Mipmaps ? TextureMinFilter.NearestMipmapNearest : TextureMinFilter.Nearest
            : textureData.Mipmaps
                ? TextureMinFilter.LinearMipmapLinear
                : TextureMinFilter.Linear;
        var magFilter = textureData.NearestFiltering ? TextureMagFilter.Nearest : TextureMagFilter.Linear;

        GL.TextureParameter(_handle, TextureParameterName.TextureMinFilter, (int) minFilter);
        GL.TextureParameter(_handle, TextureParameterName.TextureMagFilter, (int) magFilter);

        GL.TextureParameter(_handle, TextureParameterName.TextureWrapS, (int) textureData.WrapMode);
        GL.TextureParameter(_handle, TextureParameterName.TextureWrapT, (int) textureData.WrapMode);

        var levels = 1;
        if (textureData.Mipmaps)
            levels += (int)Math.Floor(Math.Log2(Math.Max(textureData.Width, textureData.Height)));
        
        GL.TextureStorage2D(_handle, levels, textureData.InternalFormat, textureData.Width, textureData.Height);
        GL.TextureSubImage2D(_handle, 0, 0, 0, textureData.Width, textureData.Height, textureData.Format,
            textureData.Type, textureData.Data);

        if (textureData.Mipmaps)
            GL.GenerateTextureMipmap(_handle);

        /*_handle = GL.GenTexture();
        Activate();

        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, textureData.Width, textureData.Height, 0,
            textureData.Format, textureData.Type, textureData.Data);
        
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
        
        GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
        
        Deactivate();*/
    }

    public void Activate(int i)
    {
        if (_activeInstances[i] == this) return;
        _activeInstances[i] = this;
        GL.BindTextureUnit(i, _handle);
    }

    public void Deactivate()
    {
        var i = Array.IndexOf(_activeInstances, this);
        if (i == -1) return;
        _activeInstances[i] = null;
        GL.BindTextureUnit(i, _handle);
    }

    private bool _disposedValue;

    public void Dispose()
    {
        if (_disposedValue) return;
        GC.SuppressFinalize(this);

        GL.DeleteTexture(_handle);
        _disposedValue = true;
    }

    // Finalizer may not be called at all
    ~Texture()
    {
        if (_disposedValue) return;
        Console.WriteLine("GPU Resource leak! Did you forget to call Dispose() on Texture?");
    }
}

public readonly record struct TextureData(byte[] Data, int Width, int Height, PixelFormat Format, PixelType Type,
    SizedInternalFormat InternalFormat, TextureWrapMode WrapMode, bool NearestFiltering, bool Mipmaps);