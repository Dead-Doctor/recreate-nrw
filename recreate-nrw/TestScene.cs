using System.Diagnostics;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using recreate_nrw.Render;

namespace recreate_nrw;

public class TestScene
{
    private readonly float[] _vertices =
    {
        -0.5f, -0.5f, -0.5f, 0.0f, 0.0f, -1.0f, 0.0f, 0.0f,
        0.5f, -0.5f, -0.5f, 0.0f, 0.0f, -1.0f, 1.0f, 0.0f,
        0.5f, 0.5f, -0.5f, 0.0f, 0.0f, -1.0f, 1.0f, 1.0f,
        0.5f, 0.5f, -0.5f, 0.0f, 0.0f, -1.0f, 1.0f, 1.0f,
        -0.5f, 0.5f, -0.5f, 0.0f, 0.0f, -1.0f, 0.0f, 1.0f,
        -0.5f, -0.5f, -0.5f, 0.0f, 0.0f, -1.0f, 0.0f, 0.0f,

        -0.5f, -0.5f, 0.5f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f,
        0.5f, -0.5f, 0.5f, 0.0f, 0.0f, 1.0f, 1.0f, 0.0f,
        0.5f, 0.5f, 0.5f, 0.0f, 0.0f, 1.0f, 1.0f, 1.0f,
        0.5f, 0.5f, 0.5f, 0.0f, 0.0f, 1.0f, 1.0f, 1.0f,
        -0.5f, 0.5f, 0.5f, 0.0f, 0.0f, 1.0f, 0.0f, 1.0f,
        -0.5f, -0.5f, 0.5f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f,

        -0.5f, 0.5f, 0.5f, -1.0f, 0.0f, 0.0f, 1.0f, 0.0f,
        -0.5f, 0.5f, -0.5f, -1.0f, 0.0f, 0.0f, 1.0f, 1.0f,
        -0.5f, -0.5f, -0.5f, -1.0f, 0.0f, 0.0f, 0.0f, 1.0f,
        -0.5f, -0.5f, -0.5f, -1.0f, 0.0f, 0.0f, 0.0f, 1.0f,
        -0.5f, -0.5f, 0.5f, -1.0f, 0.0f, 0.0f, 0.0f, 0.0f,
        -0.5f, 0.5f, 0.5f, -1.0f, 0.0f, 0.0f, 1.0f, 0.0f,

        0.5f, 0.5f, 0.5f, 1.0f, 0.0f, 0.0f, 1.0f, 0.0f,
        0.5f, 0.5f, -0.5f, 1.0f, 0.0f, 0.0f, 1.0f, 1.0f,
        0.5f, -0.5f, -0.5f, 1.0f, 0.0f, 0.0f, 0.0f, 1.0f,
        0.5f, -0.5f, -0.5f, 1.0f, 0.0f, 0.0f, 0.0f, 1.0f,
        0.5f, -0.5f, 0.5f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f,
        0.5f, 0.5f, 0.5f, 1.0f, 0.0f, 0.0f, 1.0f, 0.0f,

        -0.5f, -0.5f, -0.5f, 0.0f, -1.0f, 0.0f, 0.0f, 1.0f,
        0.5f, -0.5f, -0.5f, 0.0f, -1.0f, 0.0f, 1.0f, 1.0f,
        0.5f, -0.5f, 0.5f, 0.0f, -1.0f, 0.0f, 1.0f, 0.0f,
        0.5f, -0.5f, 0.5f, 0.0f, -1.0f, 0.0f, 1.0f, 0.0f,
        -0.5f, -0.5f, 0.5f, 0.0f, -1.0f, 0.0f, 0.0f, 0.0f,
        -0.5f, -0.5f, -0.5f, 0.0f, -1.0f, 0.0f, 0.0f, 1.0f,

        -0.5f, 0.5f, -0.5f, 0.0f, 1.0f, 0.0f, 0.0f, 1.0f,
        0.5f, 0.5f, -0.5f, 0.0f, 1.0f, 0.0f, 1.0f, 1.0f,
        0.5f, 0.5f, 0.5f, 0.0f, 1.0f, 0.0f, 1.0f, 0.0f,
        0.5f, 0.5f, 0.5f, 0.0f, 1.0f, 0.0f, 1.0f, 0.0f,
        -0.5f, 0.5f, 0.5f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f,
        -0.5f, 0.5f, -0.5f, 0.0f, 1.0f, 0.0f, 0.0f, 1.0f
    };

    private readonly uint[] _indices =
    {
        0, 1, 2,
        3, 4, 5,

        6, 7, 8,
        9, 10, 11,

        12, 13, 14,
        15, 16, 17,

        18, 19, 20,
        21, 22, 23,

        24, 25, 26,
        27, 28, 29,

        30, 31, 32,
        33, 34, 35
    };

    private readonly Camera _camera;
    private readonly Shader _shader;

    private readonly ShadedModel _shadedModel;

    private readonly Stopwatch _time = Stopwatch.StartNew();

    private readonly GameObject[] _cubes = new GameObject[10];

    private readonly Vector3 _lightPos = new(-10.0f, 10.0f, 10.0f);

    public TestScene(Camera camera)
    {
        _camera = camera;
        
        var model = Model.FromArray(_vertices, _indices);
        model.AddVertexAttribute(new VertexAttribute("aPosition", VertexAttribType.Float, 3));
        model.AddVertexAttribute(new VertexAttribute("aNormal", VertexAttribType.Float, 3));
        model.AddVertexAttribute(new VertexAttribute("aUV", VertexAttribType.Float, 2));

        _shader = new Shader("Shaders/shader.vert", "Shaders/shader.frag");
        _shader.AddUniform<float>("time");
        _shader.AddUniform<Vector3>("lightPosView");
        _shader.AddUniform<Matrix3>("normalMat");

        _shader.AddTexture("container", Texture.LoadImageFile("Resources/container.jpg"));
        _shader.AddTexture("awesomeface", Texture.LoadImageFile("Resources/awesomeface.png"));
        
        _shadedModel = new ShadedModel(model, _shader);

        var rng = new Random(8245);

        for (var i = 0; i < _cubes.Length; i++)
        {
            var position = new Vector3((float) (rng.NextDouble() * 10f - 5f), (float) (rng.NextDouble() * 10f - 5f),
                (float) (rng.NextDouble() * 10f - 5f));
            var rotation = new Vector3((float) (rng.NextDouble() * 2 * Math.PI),
                (float) (rng.NextDouble() * 2 * Math.PI), (float) (rng.NextDouble() * 2 * Math.PI));

            _cubes[i] = new GameObject(position, new Vector3(2.0f, 1.0f, 1.0f), rotation, _shadedModel);
        }
    }

    public void OnRenderFrame()
    {
        for (var i = 0; i < _cubes.Length; i++)
        {
            _shader.SetUniform("time", (float) _time.Elapsed.TotalSeconds + i * 13.0f);
            _shader.SetUniform("lightPosView", _camera.WorldToViewCoords(_lightPos));
            _shader.SetUniform("normalMat", new Matrix3(Matrix4.Transpose(_cubes[i].ModelMat.Inverted())) * new Matrix3(_camera.ViewMat));
            _cubes[i].Draw(_camera);
        }
    }

    public void OnUnload()
    {
        _shader.Dispose();
        _shadedModel.Dispose();
    }
}