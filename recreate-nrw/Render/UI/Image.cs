using JetBrains.Annotations;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace recreate_nrw.Render.UI;

public class Image
{
    private static readonly Shader Shader = new("image");

    private static readonly uint[] Indices =
    {
        0u, 1u, 2u,
        0u, 2u, 3u
    };

    private readonly Model _model;
    private readonly ShadedModel _shadedModel;

    static Image()
    {
        Shader.AddTexture("imageTexture");
    }

    private Box2 _position;
    private readonly bool _moveable;

    public Image(Texture texture, Box2 position, bool moveable)
    {
        Texture = texture;
        _position = position;
        _moveable = moveable;

        _model = Model.FromArray(GenerateVertices(), Indices, !moveable);
        _model.AddVertexAttribute(new VertexAttribute("aPos", VertexAttribType.Float, 2));
        _model.AddVertexAttribute(new VertexAttribute("aUV", VertexAttribType.Float, 2));

        var frequency = moveable ? BufferUsageAccessFrequency.Dynamic : BufferUsageAccessFrequency.Static;
        _shadedModel = new ShadedModel(_model, Shader, frequency, BufferUsageAccessNature.Draw);
    }

    private Texture Texture { get; set; }

    public Box2 Position
    {
        get => _position;
        set
        {
            if (!_moveable)
                throw new InvalidOperationException("Can't move a non-movable image.");
            _position = value;
            _model.UpdateVertices(GenerateVertices());
        }
    }

    private float[] GenerateVertices() =>
        new[]
        {
            _position.Min.X, _position.Min.Y, 0.0f, 1.0f,
            _position.Max.X, _position.Min.Y, 1.0f, 1.0f,
            _position.Max.X, _position.Max.Y, 1.0f, 0.0f,
            _position.Min.X, _position.Max.Y, 0.0f, 0.0f,
        };


    public void Draw()
    {
        Shader.SetTexture("imageTexture", Texture);
        _shadedModel.Draw();
    }
}