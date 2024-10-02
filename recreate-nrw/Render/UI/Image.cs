using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace recreate_nrw.Render.UI;

public class Image
{
    private static readonly Shader Shader = new("image");
    private static readonly uint[] Indices = {
        0u, 1u, 2u,
        0u, 2u, 3u
    };
    private readonly ShadedModel _shadedModel;

    static Image()
    {
        Shader.AddTexture("imageTexture");
    }

    public Image(Box2 position, Texture texture)
    {
        Position = position;
        Texture = texture;
        _shadedModel = new ShadedModel(_model, Shader, BufferUsageAccessFrequency.Static, BufferUsageAccessNature.Draw);

        var vertices = new[]
        {
            position.Min.X, position.Min.Y, 0.0f, 0.0f,
            position.Max.X, position.Min.Y, 1.0f, 0.0f,
            position.Max.X, position.Max.Y, 1.0f, 1.0f,
            position.Min.X, position.Max.Y, 0.0f, 1.0f,
        };
        var model = Model.FromArray(vertices, Indices);
        model.AddVertexAttribute(new VertexAttribute("aPos", VertexAttribType.Float, 2));
        model.AddVertexAttribute(new VertexAttribute("aUV", VertexAttribType.Float, 2));
        _shadedModel = new ShadedModel(model, Shader);
    }

    public Box2 Position { get; init; }
    public Texture Texture { get; set; }

    public void Draw()
    {
        Shader.SetTexture("imageTexture", Texture);
        _shadedModel.Draw();
    }
}