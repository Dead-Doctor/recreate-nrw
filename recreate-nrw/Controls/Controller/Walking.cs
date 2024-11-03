using System.Diagnostics;
using ImGuiNET;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using recreate_nrw.Ground;
using recreate_nrw.Render;

namespace recreate_nrw.Controls.Controller;

public class Walking : IController
{
    private const float EyeHeight = 1.6f; // m
    private const float Gravity = 9.81f; // m/s^2
    private const float JumpVelocity = 3f; // m/s
    private const float WalkingAcceleration = 10f; // m/s^2
    private const float MaxWalkingSpeed = 1.5f; // m/s
    private const float GroundResistance = 6f; // ?
    // https://openstax.org/books/college-physics/pages/5-2-drag-forces
    private const float Weight = 80f; // kg
    private const float AirDensity = 1.204f; // kg/m^3
    private static readonly Vector3 WindSpeed = Vector3.Zero; // m/s
    private const float DragCoefficientVertical = 0.7f;
    private const float VerticalCrossSectionalArea = 0.18f; // m^2
    
    private const float Sensitivity = 0.05f / (2.0f * (float) Math.PI);
    
    private static readonly Func<KeyboardState, float> ForwardsAxis = Controls.Axis(Keys.E, Keys.D);
    private static readonly Func<KeyboardState, float> SidewardsAxis = Controls.Axis(Keys.F, Keys.S);
    private static readonly Func<KeyboardState, float> UpwardsAxis = Controls.Axis(Keys.Space, Keys.A);
    
    private Camera _camera = null!;
    private Vector3 _position;
    private Vector3 _velocity;
    
    private float VerticalDragForce
    {
        get
        {
            var relativeVerticalSpeed = _velocity.Y - WindSpeed.Y;
            var absoluteForce = 0.5f * AirDensity * relativeVerticalSpeed*relativeVerticalSpeed * DragCoefficientVertical * VerticalCrossSectionalArea;
            return -Math.Sign(relativeVerticalSpeed) * absoluteForce;
        }
    }

    public void Activate(Camera camera)
    {
        _camera = camera;
        if (_camera.Position != Vector3.Zero)
            _position = _camera.Position - new Vector3(0, EyeHeight, 0);
        _velocity = Vector3.Zero;
    }

    public void Update(KeyboardState keyboard, MouseState mouse, double deltaTime /* s */)
    {
        var turn = mouse.Delta * Sensitivity;
        _camera.Turn(turn.X, -turn.Y);
        
        var heightAt = TerrainData.GetHeightAt(_position.Xz);
        if (heightAt == null) return;
        
        var acceleration = Vector3.Zero;
            
        var distanceToGround = _position.Y - heightAt.Value;
        //TODO: step down distance
        if (distanceToGround <= 0.0)
        {
            _position.Y += -distanceToGround;
            if (_velocity.Y <= 0) _velocity.Y = 0;
            if (UpwardsAxis(keyboard) >= 1f) _velocity.Y = JumpVelocity;

            var cameraHorizontalFront = Vector3.Cross(_camera.Up, _camera.Right);
            var moveDirection = ForwardsAxis(keyboard) * cameraHorizontalFront.Xz + SidewardsAxis(keyboard) * _camera.Right.Xz;
            if (moveDirection.LengthSquared > 1e-3f)
            {
                acceleration.Xz += moveDirection.Normalized() * WalkingAcceleration;
            }
        }
        else
        {
            acceleration.Y -= Gravity;
        }
        acceleration.Y += VerticalDragForce / Weight;
        
        _velocity += acceleration * (float)deltaTime;
        var horizontalSpeed = _velocity.Xz.Length;
        var horizontalDirection = _velocity.Xz.Normalized();
        if (distanceToGround <= 0.0 && horizontalSpeed > 0.0f)
        {
            Debug.Assert(!float.IsNaN(horizontalDirection.X));
            Debug.Assert(!float.IsNaN(horizontalDirection.Y));
            
            if (horizontalSpeed >= MaxWalkingSpeed)
                _velocity.Xz = horizontalDirection * MaxWalkingSpeed;
            _velocity.Xz -= horizontalDirection * Math.Min(GroundResistance * (float)deltaTime, horizontalSpeed);
        }
        
        _position += _velocity * (float)deltaTime;
        _camera.Position = _position + new Vector3(0f, EyeHeight, 0f);
    }

    public void InfoWindow()
    {
        ImGui.Text($"Velocity: {_velocity.Length * 3.6f}km/h");
    }
}