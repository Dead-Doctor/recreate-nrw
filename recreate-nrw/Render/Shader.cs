using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace recreate_nrw.Render;

public class Shader : IDisposable
{
    private static Shader? _activeInstance;

    private int _handle;
    private readonly List<Uniform> _uniforms = new();
    private readonly List<Texture> _textures = new();

    public Shader(string vertexPath, string fragmentPath)
    {
        var vertexShaderSource = File.ReadAllText(vertexPath);
        var fragmentShaderSource = File.ReadAllText(fragmentPath);

        var vertexShader = CreateAndCompileShader(ShaderType.VertexShader, vertexShaderSource);
        var fragmentShader = CreateAndCompileShader(ShaderType.FragmentShader, fragmentShaderSource);

        CreateAndLinkProgram(vertexShader, fragmentShader);

        GL.DetachShader(_handle, vertexShader);
        GL.DetachShader(_handle, fragmentShader);
        GL.DeleteShader(vertexShader);
        GL.DeleteShader(fragmentShader);
    }

    private static int CreateAndCompileShader(ShaderType shaderType, string shaderSource)
    {
        var shader = GL.CreateShader(shaderType);
        GL.ShaderSource(shader, shaderSource);
        GL.CompileShader(shader);

        //Check CompileStatus
        GL.GetShader(shader, ShaderParameter.CompileStatus, out var success);
        if (success == (int) All.True) return shader;

        var infoLog = GL.GetShaderInfoLog(shader);
        throw new Exception($"Error occurred whilst compiling Shader ({shader}) of type: {shaderType}.\n\n{infoLog}");
    }

    private void CreateAndLinkProgram(int vertexShader, int fragmentShader)
    {
        _handle = GL.CreateProgram();
        GL.AttachShader(_handle, vertexShader);
        GL.AttachShader(_handle, fragmentShader);
        GL.LinkProgram(_handle);

        GL.GetProgram(_handle, GetProgramParameterName.LinkStatus, out var success);
        if (success == (int) All.True) return;

        var infoLog = GL.GetProgramInfoLog(_handle);
        throw new Exception($"Error occurred whilst linking Program ({_handle}).\n\n{infoLog}");
    }

    private int GetUniformLocation(string name)
    {
        var location = GL.GetUniformLocation(_handle, name);
        if (location == -1)
            throw new ArgumentException($"Could not find requested uniform '{name}' in shader ({_handle}).");
        return location;
    }

    public void AddUniform<T>(string name) where T : struct
    {
        _uniforms.Add(new Uniform<T>(GetUniformLocation(name), name));
    }

    public void AddUniform<T>(string name, T initial) where T : struct
    {
        _uniforms.Add(new Uniform<T>(GetUniformLocation(name), name, initial));
    }

    public void SetUniform<T>(string name, T value) where T : struct
    {
        var uniform = _uniforms.Find(uniform => uniform.Name == name);
        if (uniform == null) throw new MissingFieldException($"Uniform '{name}' could not be set as it was not registered in shader ({_handle}).");
        
        var typed = (Uniform<T>) uniform;
        typed.Data = value;
    }

    public T GetUniform<T>(string name) where T : struct
    {
        var uniform = _uniforms.Find(uniform => uniform.Name == name);
        if (uniform == null) throw new MissingFieldException($"Uniform '{name}' could not be queried as it was not registered in shader ({_handle}).");
        
        var typed = (Uniform<T>) uniform;
        return typed.Data;
    }

    public void AddTexture(string name, Texture texture)
    {
        var i = _textures.Count;
        if (_textures.Count >= 16) throw new Exception($"Too many textures on Shader ({i + 1}/16)");
        _textures.Add(texture);
        try
        {
            AddUniform(name, i);
        }
        catch (ArgumentException)
        {
            //TODO: give opengl objects names to print instead of handle (e.g. from file name)
            throw new ArgumentException($"Could not find requested texture '{name}' in shader ({_handle}).");
        }
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
        
        for (var i = 0; i < _textures.Count; i++)
        {
            _textures[i].Activate(i);
        }
    }

    public void Deactivate()
    {
        if (_activeInstance != this) return;
        _activeInstance = null;
        
        foreach (var texture in _textures)
        {
            texture.Deactivate();
        }
        GL.UseProgram(0);
    }

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

    private T _data;

    public T Data
    {
        get => _data;
        set
        {
            if (_data.Equals(value)) return;
            _data = value;
            _dirty = true;
        }
    }

    private bool _dirty;

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
        if (!_dirty) return;
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