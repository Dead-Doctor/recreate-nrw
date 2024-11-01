using ImGuiNET;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using recreate_nrw.Render;

namespace recreate_nrw;

public class Controls
{
    private const float Speed = 5f;
    private const float SprintingSpeed = 100f;
    private const float Sensitivity = 0.05f / (2.0f * (float) Math.PI);

    // Time to zoom in/out in seconds
    private const float ZoomSpeed = 0.1f;
    private const float Fov = MathHelper.PiOver2;

    private const float ZoomFactor = 1f / 3f;
    private float _zoom;

    private bool _paused = true;
    private bool _sprinting = true;
    public static volatile bool CaptureFrame = false;
    public static int CapturedFrames = 0;

    private readonly Func<KeyboardState, float> _forwardsAxis = Axis(Keys.E, Keys.D);
    private readonly Func<KeyboardState, float> _sidewardsAxis = Axis(Keys.F, Keys.S);
    private readonly Func<KeyboardState, float> _upwardsAxis = Axis(Keys.Space, Keys.A);

    private readonly Window _window;

    public Controls(Window window)
    {
        _window = window;
    }

    public void Update(double deltaTime, Camera camera)
    {
        if (!_window.IsFocused) return;
        
        var input = _window.KeyboardState;
        if (input.IsKeyPressed(Keys.Escape)) Pause(!_paused);

        if (_paused) return;
        
        if (input.IsKeyPressed(Keys.P)) _window.Debug ^= true;
        if (input.IsKeyPressed(Keys.B)) _sprinting ^= true;
        if (input.IsKeyPressed(Keys.C)) CaptureFrame = true;
        var currentSpeed = _sprinting ? SprintingSpeed : Speed;

        var velocity = _forwardsAxis(input) * camera.Front + _sidewardsAxis(input) * camera.Right +
                       _upwardsAxis(input) * camera.Up;
        if (velocity != Vector3.Zero)
            camera.Move(velocity.Normalized() * currentSpeed * (float) deltaTime);

        var mouse = _window.MouseState;

        var zoomChange = (float) deltaTime / ZoomSpeed;
        if (!input.IsKeyDown(Keys.V)) zoomChange *= -1.0f;
        _zoom = Math.Clamp(_zoom + zoomChange, 0.0f, 1.0f);

        // interpolate range: (1..ZoomFovMultiplier)
        var zoomFactorInterpolated = 1 + EaseInOutSine(_zoom) * (ZoomFactor - 1.0f);

        camera.Fov = Fov * zoomFactorInterpolated;

        var turnDistance = Sensitivity * zoomFactorInterpolated;

        var turn = mouse.Delta * turnDistance;
        camera.Turn(turn.X, -turn.Y);
    }

    private static Func<KeyboardState, float> Axis(Keys positive, Keys negative) => input =>
        (input.IsKeyDown(positive) ? 1.0f : 0.0f) - (input.IsKeyDown(negative) ? 1.0f : 0.0f);

    private static float EaseInOutSine(float v)
    {
        return -((float) Math.Cos(Math.PI * v) - 1.0f) / 2.0f;
    }

    public void Pause(bool pause)
    {
        if (_paused == pause) return;
        _window.CursorState = pause ? CursorState.Normal : CursorState.Grabbed;
        var io = ImGui.GetIO();
        if (pause)
            io.ConfigFlags &= ~ImGuiConfigFlags.NoMouse;
        else
            io.ConfigFlags |= ImGuiConfigFlags.NoMouse;
        _paused = pause;
    }
}