using ImGuiNET;
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

    public readonly Vector3 Up = new Vector3(0.0f, 1.0f, 0.0f).Normalized();
    public Vector3 Front;
    public Vector3 Right;
    private float _yaw;
    private float _pitch;
    private float _fov;
    private readonly float _depthNear;
    private readonly float _depthFar;
    private float _aspect;

    public Matrix4 ViewMat { private set; get; }
    public Matrix4 ProjectionMat { private set; get; }

    public float Fov
    {
        get => _fov;
        set {
            _fov = value;
            CalculateProjectionMatrix();
        }
    }

    public Camera(Vector2i size, Vector3 position, float yaw = 0.0f, float pitch = 0.0f, float fov = MathHelper.PiOver2,
        float depthNear = 0.1f, float depthFar = 1024.0f)
    {
        _aspect = size.X / (float) size.Y;
        _position = position;
        SetEuler(yaw, pitch);
        _fov = fov;
        _depthNear = depthNear;
        _depthFar = depthFar;
        CalculateProjectionMatrix();
    }
    
    public void Move(Vector3 distance) => Position += distance;

    private const float Epsilon = 0.1f;
    private void SetEuler(float yaw, float pitch)
    {
        _yaw = yaw;
        const float limit = MathHelper.PiOver2 - Epsilon;
        _pitch = Math.Clamp(pitch, -limit, limit);
        
        Front.X = (float) Math.Cos(pitch) * (float) Math.Sin(yaw);
        Front.Y = (float) Math.Sin(pitch);
        Front.Z = (float) Math.Cos(pitch) * -(float) Math.Cos(yaw);
        Front.Normalize();
        CalculateRight();
    }

    public void Turn(float yaw, float pitch)
    {
        SetEuler(_yaw + yaw, _pitch + pitch);
    }

    public void Resize(Vector2i size)
    {
        _aspect = size.X / (float) size.Y;
        CalculateProjectionMatrix();
    }

    private void CalculateRight()
    {
        Right = Vector3.Cross(Front, Up).Normalized();
        CalculateViewMatrix();
    }

    private void CalculateViewMatrix()
    {
        ViewMat = Matrix4.LookAt(_position, _position + Front, Up);
    }

    private void CalculateProjectionMatrix()
    {
        ProjectionMat = Matrix4.CreatePerspectiveFieldOfView(_fov, _aspect, _depthNear, _depthFar);
    }

    public Vector3 WorldToViewCoords(Vector3 position) => (new Vector4(position, 1.0f) * ViewMat).Xyz;
}