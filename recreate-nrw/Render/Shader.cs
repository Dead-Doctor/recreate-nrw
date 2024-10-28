using System.Text;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using recreate_nrw.Util;

namespace recreate_nrw.Render;

public class Shader : IDisposable
{
    private static Shader? _activeInstance;

    private readonly string _name;
    private int _handle;
    private readonly List<Uniform> _uniforms = new();
    private readonly List<TextureSlot> _textureSlots = new();

    public Shader(string name)
    {
        _name = name;

        var vertexShader = CreateAndCompileShader(name, ShaderType.VertexShader, $"Shaders/{name}.vert");
        var fragmentShader = CreateAndCompileShader(name, ShaderType.FragmentShader, $"Shaders/{name}.frag");
        
        CreateAndLinkProgram(vertexShader, fragmentShader);
        
        GL.DeleteShader(vertexShader);
        GL.DeleteShader(fragmentShader);
        Resources.RegisterDisposable(this);
    }

    private static int CreateAndCompileShader(string name, ShaderType shaderType, string path)
    {
        var shaderSource = Resources.GetCached(path, Source.Embedded, stream =>
        {
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return reader.ReadToEnd();
        });
        
        var shader = GL.CreateShader(shaderType);
        GL.ShaderSource(shader, shaderSource);
        GL.CompileShader(shader);

        //Check CompileStatus
        GL.GetShader(shader, ShaderParameter.CompileStatus, out var success);
        if (success == (int) All.True) return shader;

        var infoLog = GL.GetShaderInfoLog(shader);
        var shaderFile = shaderType switch
        {
            ShaderType.VertexShader => $"{name}.vert",
            ShaderType.FragmentShader => $"{name}.frag",
            _ => throw new ArgumentOutOfRangeException(nameof(shaderType), shaderType, null)
        };
        throw new Exception($"Error occurred whilst compiling Shader: {shaderFile}\n\n{infoLog}");
    }

    private void CreateAndLinkProgram(int vertexShader, int fragmentShader)
    {
        _handle = GL.CreateProgram();
        GL.AttachShader(_handle, vertexShader);
        GL.AttachShader(_handle, fragmentShader);
        GL.LinkProgram(_handle);
        GL.DetachShader(_handle, vertexShader);
        GL.DetachShader(_handle, fragmentShader);

        GL.GetProgram(_handle, GetProgramParameterName.LinkStatus, out var success);
        if (success == (int) All.True) return;

        var infoLog = GL.GetProgramInfoLog(_handle);
        throw new Exception($"Error occurred whilst linking Program ({_handle}).\n\n{infoLog}");
    }

    private int GetUniformLocation(string name)
    {
        var location = GL.GetUniformLocation(_handle, name);
        if (location == -1)
            throw new ArgumentException($"Could not find requested uniform '{name}' in shader ({_handle}). Maybe uniform got optimized away!!!!!!");
        return location;
    }

    public void AddUniform<T>(string name) where T : struct
    {
        if (_uniforms.Find(uniform => uniform.Name == name) != null)
            throw new ArgumentException(
                $"There is already a uniform with the name '{name}' on this shader ({_name}).");
        _uniforms.Add(new Uniform<T>(GetUniformLocation(name), name));
    }

    public void AddUniform<T>(string name, T initial) where T : struct
    {
        if (_uniforms.Find(uniform => uniform.Name == name) != null)
            throw new ArgumentException(
                $"There is already a uniform with the name '{name}' on this shader ({_name}).");
        _uniforms.Add(new Uniform<T>(GetUniformLocation(name), name, initial));
    }

    public void SetUniform<T>(string name, T value) where T : struct
    {
        var uniform = _uniforms.Find(uniform => uniform.Name == name);
        if (uniform == null) throw new MissingFieldException($"Uniform '{name}' could not be set as it was not registered in shader ({_handle}).");
        
        var typed = (Uniform<T>) uniform;
        if (typed.Data.Equals(value)) return;
        typed.Data = value;
        typed.Dirty = true;
    }

    public T GetUniform<T>(string name) where T : struct
    {
        var uniform = _uniforms.Find(uniform => uniform.Name == name);
        if (uniform == null) throw new MissingFieldException($"Uniform '{name}' could not be queried as it was not registered in shader ({_handle}).");
        
        var typed = (Uniform<T>) uniform;
        return typed.Data;
    }

    public void AddTexture(string name, Texture? texture = null)
    {
        var i = _textureSlots.Count;
        if (_textureSlots.Count >= 16) throw new Exception($"Too many textures on Shader ({i + 1}/16)");
        if (_textureSlots.Find(slot => slot.Name == name) != null)
            throw new ArgumentException(
                $"There is already a texture with the name '{name}' on this shader ({_handle}).");
        _textureSlots.Add(new TextureSlot(name, texture));
        try
        {
            AddUniform(name, i);
        }
        catch (ArgumentException)
        {
            //TODO: give opengl objects names to print instead of handle (e.g. from file name). also see: GL.ObjectLabel()
            throw new ArgumentException($"Could not find requested texture '{name}' in shader ({_handle}).");
        }
    }
    
    public void SetTexture(string name, Texture texture)
    {
        var textureSlot = _textureSlots.Find(slot => slot.Name == name);
        if (textureSlot == null) throw new MissingFieldException($"Texture slot '{name}' could not be set as it was not registered in shader ({_handle}).");
        textureSlot.Texture = texture;
    }

    public int GetAttribLocation(string attribName)
    {
        return GL.GetAttribLocation(_handle, attribName);
    }

    public void Activate()
    {
        if (_activeInstance == this) return;
        _activeInstance = this;
        
        GL.UseProgram(_handle);
        foreach (var uniform in _uniforms)
        {
            uniform.Upload();
        }
        
        for (var i = 0; i < _textureSlots.Count; i++)
        {
            _textureSlots[i].Texture?.Activate(i);
        }
    }

    public void Deactivate()
    {
        if (_activeInstance != this) return;
        _activeInstance = null;
        
        foreach (var texture in _textureSlots)
        {
            texture.Texture?.Deactivate();
        }
        GL.UseProgram(0);
    }

    public override string ToString() => _name;

    private bool _disposedValue;

    public void Dispose()
    {
        if (_disposedValue) return;
        GC.SuppressFinalize(this);

        GL.DeleteProgram(_handle);
        _disposedValue = true;
    }

    // Finalizer may not be called at all
    ~Shader()
    {
        if (_disposedValue) return;
        Console.WriteLine("GPU Resource leak! Did you forget to call Dispose() on Shader?");
    }
}

public abstract class Uniform
{
    public abstract void Upload();
    public readonly string Name;

    protected Uniform(string name)
    {
        Name = name;
    }
}

public class Uniform<T> : Uniform where T : struct
{
    private readonly int _handle;

    public T Data;

    public bool Dirty = true;

    public Uniform(int handle, string name) : base(name)
    {
        _handle = handle;
    }

    public Uniform(int handle, string name, T initial) : base(name)
    {
        _handle = handle;
        Data = initial;
    }

    public override void Upload()
    {
        if (!Dirty) return;
        Dirty = false;
        if (typeof(T) == typeof(int))
        {
            GL.Uniform1(_handle, Convert.ToInt32(Data));
        }
        else if (typeof(T) == typeof(float))
        {
            GL.Uniform1(_handle, Convert.ToSingle(Data));
        }
        else if (typeof(T) == typeof(Vector2))
        {
            GL.Uniform2(_handle, (Vector2)Convert.ChangeType(Data, typeof(Vector2)));
        }
        else if (typeof(T) == typeof(Vector3))
        {
            GL.Uniform3(_handle, (Vector3)Convert.ChangeType(Data, typeof(Vector3)));
        }
        else if (typeof(T) == typeof(Vector4))
        {
            GL.Uniform4(_handle, (Vector4)Convert.ChangeType(Data, typeof(Vector4)));
        }
        else if (typeof(T) == typeof(Color4))
        {
            GL.Uniform4(_handle, (Color4)Convert.ChangeType(Data, typeof(Color4)));
        }
        else if (typeof(T) == typeof(Matrix3))
        {
            var data = (Matrix3) Convert.ChangeType(Data, typeof(Matrix3));
            GL.UniformMatrix3(_handle, true, ref data);
        }
        else if (typeof(T) == typeof(Matrix4))
        {
            var data = (Matrix4) Convert.ChangeType(Data, typeof(Matrix4));
            GL.UniformMatrix4(_handle, true, ref data);
        }
        else throw new NotSupportedException($"Uniform of type '{typeof(T).Name}' is not supported.");
    }
}

public class TextureSlot
{
    public string Name { get; }
    public Texture? Texture { get; set; }
    
    public TextureSlot(string name, Texture? texture)
    {
        Name = name;
        Texture = texture;
    }
}