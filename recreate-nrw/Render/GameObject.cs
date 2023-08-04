using OpenTK.Mathematics;

namespace recreate_nrw.Render;

public class GameObject
{
    private Vector3 _position;
    private Vector3 _scale;
    private Vector3 _rotation;
    
    public Vector3 Position
    {
        get => _position;
        set
        {
            _position = value;
            CalculateModelMat();
        }
    }
    public Vector3 Scale
    {
        get => _scale;
        set
        {
            _scale = value;
            CalculateModelMat();
        }
    }
    public Vector3 Rotation
    {
        get => _rotation;
        set
        {
            _rotation = value;
            CalculateModelMat();
        }
    }

    private readonly ShadedModel _shadedModel;
    public Matrix4 ModelMat;

    public GameObject(Vector3 position, Vector3 scale, Vector3 rotation, ShadedModel shadedModel)
    {
        Position = position;
        Scale = scale;
        Rotation = rotation;
        _shadedModel = shadedModel;
        _shadedModel.Shader.AddUniform<Matrix4>("projectionMat");
        _shadedModel.Shader.AddUniform<Matrix4>("modelViewMat");
    }
    
    private void CalculateModelMat() => ModelMat =
        Matrix4.CreateScale(Scale) *
        Matrix4.CreateTranslation(Position) *
        Matrix4.CreateRotationX(Rotation.X) * Matrix4.CreateRotationY(Rotation.Y) * Matrix4.CreateRotationZ(Rotation.Z);

    public void Draw(Camera camera)
    {
        _shadedModel.Shader.SetUniform("projectionMat", camera.ProjectionMat);
        _shadedModel.Shader.SetUniform("modelViewMat", ModelMat * camera.ViewMat);
        _shadedModel.Draw();
    }
}