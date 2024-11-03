using OpenTK.Mathematics;
using recreate_nrw.Util;

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
    private float _pitch;
    private float _fov;
    private float _depthNear;
    private float _depthFar;
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

    public void Init(Vector2i size, float fov = MathHelper.PiOver2, float depthNear = 0.1f, float depthFar = 1048576.0f)
    {
        _aspect = size.X / (float) size.Y;
        _position = Vector3.Zero;
        SetEuler(0.0f, 0.0f);
        _fov = fov;
        _depthNear = depthNear;
        _depthFar = depthFar;
        CalculateProjectionMatrix();
    }
    
    public void Move(Vector3 distance) => Position += distance;

    private const float Epsilon = 0.001f;
    private void SetEuler(float yaw, float pitch)
    {
        Yaw = yaw.Modulo(MathHelper.TwoPi);
        const float limit = MathHelper.PiOver2 - Epsilon;
        _pitch = Math.Clamp(pitch, -limit, limit);

        var pitchSin = (float) Math.Sin(_pitch);
        var pitchCos = (float) Math.Cos(_pitch);
        var yawSin   = (float) Math.Sin(Yaw);
        var yawCos   = (float) Math.Cos(Yaw);
        Front.X = pitchCos * yawSin;
        Front.Y = pitchSin;
        Front.Z = pitchCos * -yawCos;
        Front.Normalize();
        CalculateRight();
    }

    public void Turn(float yaw, float pitch)
    {
        SetEuler(Yaw + yaw, _pitch + pitch);
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

    public float Yaw { get; private set; }
}