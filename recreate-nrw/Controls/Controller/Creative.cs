using ImGuiNET;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using recreate_nrw.Render;
using recreate_nrw.Util;

namespace recreate_nrw.Controls.Controller;

public class Creative : IController
{
    private const float Speed = 5f;
    private const float SprintingSpeed = 100f;
    private const float Sensitivity = 0.05f / (2.0f * (float) Math.PI);
    // Time to zoom in/out in seconds
    private const float ZoomFactor = 1f / 3f;
    private const float ZoomSpeed = 0.1f;
    private const float Fov = MathHelper.PiOver2;

    private bool _sprinting = true;
    private float _zoom;
    
    private readonly Func<KeyboardState, float> _forwardsAxis = Controls.Axis(Keys.E, Keys.D);
    private readonly Func<KeyboardState, float> _sidewardsAxis = Controls.Axis(Keys.F, Keys.S);
    private readonly Func<KeyboardState, float> _upwardsAxis = Controls.Axis(Keys.Space, Keys.A);
    
    private Camera _camera = null!;

    public void Activate(Camera camera)
    {
        _camera = camera;
        if (_camera.Position == Vector3.Zero) _camera.Position = new Vector3(0f, 100f, 0f);
    }

    public void Update(KeyboardState keyboard, MouseState mouse, double deltaTime)
    {
        if (keyboard.IsKeyPressed(Keys.B)) _sprinting ^= true;
        var currentSpeed = _sprinting ? SprintingSpeed : Speed;

        var velocity = _forwardsAxis(keyboard) * _camera.Front + _sidewardsAxis(keyboard) * _camera.Right +
                       _upwardsAxis(keyboard) * _camera.Up;
        if (velocity != Vector3.Zero)
            _camera.Move(velocity.Normalized() * currentSpeed * (float) deltaTime);

        var zoomChange = (float) deltaTime / ZoomSpeed;
        if (!keyboard.IsKeyDown(Keys.V)) zoomChange *= -1.0f;
        _zoom = Math.Clamp(_zoom + zoomChange, 0.0f, 1.0f);

        // interpolate range: (1..ZoomFovMultiplier)
        var zoomFactorInterpolated = 1 + Calc.EaseInOutSine(_zoom) * (ZoomFactor - 1.0f);

        _camera.Fov = Fov * zoomFactorInterpolated;

        var turnDistance = Sensitivity * zoomFactorInterpolated;

        var turn = mouse.Delta * turnDistance;
        _camera.Turn(turn.X, -turn.Y);
    }

    public void InfoWindow()
    {
        if (ImGuiExtension.Vector3("Position", _camera.Position, out var newPosition))
            _camera.Position = newPosition;

        var cameraPosition = Coordinate.World(_camera.Position);
        var terrainData = cameraPosition.Epsg25832();
        if (ImGuiExtension.Vector2("EPSG:25832", terrainData, out var newCoordinates))
            _camera.Position = Coordinate.Epsg25832(newCoordinates, _camera.Position.Y).World();

        var dataTile = cameraPosition.TerrainDataIndex();
        if (ImGuiExtension.Vector2("Data Tile", dataTile, out var newDataTile))
            _camera.Position = Coordinate.TerrainDataIndex(newDataTile.FloorToInt()).World(_camera.Position.Y);

        var latLon = cameraPosition.Wgs84();
        if (ImGuiExtension.Vector2("WGS 84", latLon, out var newLatLon))
            _camera.Position = Coordinate.Wgs84(newLatLon, _camera.Position.Y).World();

        if (ImGui.Button("Home Sweet Home!"))
            _camera.Position = Coordinate.Epsg25832(new Vector2(347000, 5673000), _camera.Position.Y).World();
        ImGui.SameLine();
        if (ImGui.Button("Schloss Burg"))
            _camera.Position = Coordinate
                .Epsg25832(new Vector2(370552.5815306349f, 5666744.753800459f), _camera.Position.Y).World();
        
        ImGui.Checkbox("Sprinting", ref _sprinting);
    }
}