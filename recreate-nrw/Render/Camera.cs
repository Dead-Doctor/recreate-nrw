using OpenTK.Mathematics;

namespace recreate_nrw.Render;

public class Camera
{
    private Vector3 _position;

    public Vector3 Position
    {
        get => _position;
        set
        {
            _position = value;
            CalculateViewMatrix();
        }
    }
    
    private Quaternion _rotation;
    private Matrix4 _rotationMat;
    
    public Quaternion Rotation
    {
        get => _rotation;
        set
        {
            _rotation = value;
            _rotationMat = Matrix4.CreateFromQuaternion(_rotation);
            CalculateViewMatrix();
        }
    }
    
    private float _fov;

    public float Fov
    {
        get => _fov;
        set {
            _fov = value;
            CalculateProjectionMatrix();
        }
    }
    
    private float _depthNear;
    private float _depthFar;
    private float _aspect;

    public Matrix4 ViewMat { private set; get; }
    public Matrix4 ProjectionMat { private set; get; }

    public void Init(Vector2i size, float fov = MathHelper.PiOver2, float depthNear = 0.1f, float depthFar = 1048576.0f)
    {
        _position = Vector3.Zero;
        _rotation = Quaternion.Identity;
        CalculateViewMatrix();
        _aspect = size.X / (float) size.Y;
        _fov = fov;
        _depthNear = depthNear;
        _depthFar = depthFar;
        CalculateProjectionMatrix();
    }

    public void Resize(Vector2i size)
    {
        _aspect = size.X / (float) size.Y;
        CalculateProjectionMatrix();
    }

    private void CalculateViewMatrix()
    {
        ViewMat = CalculateViewMatrixAt(_position);
    }

    public Matrix4 CalculateViewMatrixAt(Vector3 eye)
    {
        var translation = Matrix4.CreateTranslation(-eye);
        var viewMat = translation * _rotationMat;
        return viewMat;
    }

    private void CalculateProjectionMatrix()
    {
        ProjectionMat = Matrix4.CreatePerspectiveFieldOfView(_fov, _aspect, _depthNear, _depthFar);
    }

    public float Yaw => Rotation.ToEulerAngles().Y;
    
    public Vector3 Front => -_rotationMat.Column2.Xyz;
}