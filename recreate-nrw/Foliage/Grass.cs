using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using recreate_nrw.Render;
using recreate_nrw.Util;
using recreate_nrw.Ground;

namespace recreate_nrw.Foliage;

public class Grass
{
    private static readonly float[] Vertices = {
        // vertex attributes           
        -1.0f, 0.0f, 0.0f,  0.0f, 0.0f,
        +1.0f, 0.0f, 0.0f,  1.0f, 0.0f,
        +1.0f, 2.0f, 0.0f,  1.0f, 1.0f,
        -1.0f, 2.0f, 0.0f,  0.0f, 1.0f
    };
    
    private static readonly uint[] Indices = {
        0u, 1u, 2u,
        0u, 2u, 3u
    };

    private readonly Shader _shader;
    private readonly ShadedModel _shadedModel;
    private Vector2 _offset = new(0.0f, 0.0f);
    private float _rotation = 0.0f;

    public Grass(Terrain terrain)
    {
        var model = Model.FromArray(Vertices, Indices);
        model.AddVertexAttribute(new VertexAttribute("aPosition", VertexAttribType.Float, 3));
        model.AddVertexAttribute(new VertexAttribute("aUV",       VertexAttribType.Float, 2));
        
        _shader = new Shader("foliage");
        _shader.AddUniform("aOffset", _offset);
        _shader.AddUniform("aRotation", _rotation);
        _shader.AddUniform<Matrix4>("modelViewMat");
        _shader.AddUniform<Matrix4>("projectionMat");
        _shader.AddTexture("foliageTexture", Texture.LoadImageFile("Resources/grass.png", TextureWrapMode.ClampToEdge));
        terrain.AddDependentShader(_shader);
        
        _shadedModel = new ShadedModel(model, _shader, BufferUsageAccessFrequency.Static, BufferUsageAccessNature.Draw);
    }

    public void Draw(Camera camera)
    {
        _shader.SetUniform("modelViewMat", camera.ViewMat);
        _shader.SetUniform("projectionMat", camera.ProjectionMat);

        var oldBackFaceCulling = Renderer.BackFaceCulling;
        Renderer.BackFaceCulling = false;
        _shadedModel.Draw();
        Renderer.BackFaceCulling = oldBackFaceCulling;
    }

    public void Window()
    {
        ImGui.Begin("Grass");
        if (ImGuiExtension.Vector2("Offset", _offset, out _offset))
            _shader.SetUniform("aOffset", _offset);
        if (ImGui.SliderAngle("Rotation", ref _rotation))
            _shader.SetUniform("aRotation", _rotation);
        ImGui.End();
    }
}