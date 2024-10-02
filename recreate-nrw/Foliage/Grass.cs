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
    private int _n = 256;
    private float _gridSize = 0.5f;

    public Grass(Terrain terrain)
    {
        var model = Model.FromArray(Vertices, Indices);
        model.AddVertexAttribute(new VertexAttribute("aPosition", VertexAttribType.Float, 3));
        model.AddVertexAttribute(new VertexAttribute("aUV",       VertexAttribType.Float, 2));
        
        _shader = new Shader("foliage");
        _shader.AddUniform("n", _n);
        _shader.AddUniform("gridSize", _gridSize);
        _shader.AddUniform<Vector3>("origin");
        _shader.AddUniform<Matrix4>("viewMat");
        _shader.AddUniform<Matrix4>("projectionMat");
        _shader.AddTexture("foliageTexture", Texture.LoadImageFile("Resources/grass.png", TextureWrapMode.ClampToEdge));
        terrain.AddDependentShader(_shader);
        
        _shadedModel = new ShadedModel(model, _shader, BufferUsageAccessFrequency.Static, BufferUsageAccessNature.Draw);
    }

    public void Draw(Camera camera)
    {
        var snapOffset = (camera.Position.Modulo(_gridSize) + new Vector3(_gridSize)).Modulo(_gridSize);
        var origin = camera.Position - snapOffset;
        _shader.SetUniform("origin", origin);
        _shader.SetUniform("viewMat", camera.ViewMat);
        _shader.SetUniform("projectionMat", camera.ProjectionMat);

        var oldBackFaceCulling = Renderer.BackFaceCulling;
        Renderer.BackFaceCulling = false;
        _shadedModel.DrawInstanced(_n * _n);
        Renderer.BackFaceCulling = oldBackFaceCulling;
    }

    public void Window()
    {
        ImGui.Begin("Grass");
        if (ImGui.SliderInt("N", ref _n, 1, 1024))
            _shader.SetUniform("n", _n);
        if (ImGui.SliderFloat("Grid Size", ref _gridSize, 0.1f, 2.0f))
            _shader.SetUniform("gridSize", _gridSize);
        ImGui.End();
    }
}