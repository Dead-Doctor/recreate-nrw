﻿using System.Globalization;
using System.Runtime.InteropServices;
using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using recreate_nrw.Ground;
using recreate_nrw.Foliage;
using recreate_nrw.Render;
using recreate_nrw.Render.UI;
using recreate_nrw.Util;

namespace recreate_nrw;

public class Window : GameWindow
{
    private bool _vsync = true;

    private readonly TerrainModel _terrainModel;

    // private TestScene _scene = null!;
    private ImGuiController _controller = null!;
    private Camera _camera = null!;
    private Controls _controls = null!;
    private Shader _terrainModelShader = null!;
    private ShadedModel _shadedTerrainModel = null!;
    private bool _renderTerrainModel;

    private Terrain _terrain = null!;
    private Fern _fern = null!;
    private Grass _grass = null!;

    private Sky _sky = null!;

    private Map _map = null!;

    private readonly Vector3 _lightDir = new Vector3(1.0f, -1.0f, 1.0f).Normalized();
    public bool Debug;

    public Window() : base(
        GameWindowSettings.Default,
        new NativeWindowSettings
            { Title = "Recreate NRW", Size = (960, 540), APIVersion = new Version(4, 6) }
    )
    {
        VSync = _vsync ? VSyncMode.On : VSyncMode.Off;
        var heightmap = new Heightmap(new Vector2i(347, 5673));
        heightmap.LoadTile(new Vector2i(0, 0));
        _terrainModel = new TerrainModel(heightmap, new Vector2i(0, 0), 1000u);
    }

    /// <summary>
    /// Prevent Callback from being garbage collected.
    /// </summary>
    private readonly DebugProc _openGlDebugCallback = OpenGlDebugCallback;

    protected override void OnLoad()
    {
        base.OnLoad();
#if DEBUG
        GL.Enable(EnableCap.DebugOutput);
        GL.Enable(EnableCap.DebugOutputSynchronous);
        // glfwWindowHint(GLFW_OPENGL_DEBUG_CONTEXT, GLFW_TRUE);
        GL.DebugMessageCallback(_openGlDebugCallback, (IntPtr)0);
#endif
        Renderer.ClearColor = new Color4(0.2f, 0.3f, 0.3f, 1.0f);
        Renderer.DepthTesting = true;
        Renderer.BackFaceCulling = true;
        Renderer.BlendingFunction(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        _controller = new ImGuiController(ClientSize);
        Resources.RegisterDisposable(_controller);

        _camera = new Camera(Size, new Vector3(0.0f, 100.0f, 0.0f));
        _controls = new Controls(this);

        _terrainModelShader = new Shader("terrainModel");
        _terrainModelShader.AddUniform<Matrix4>("modelViewMat");
        _terrainModelShader.AddUniform<Matrix4>("projectionMat");
        _terrainModelShader.AddUniform("lightDir", _lightDir);

        _terrainModelShader.AddTexture("concreteTexture",
            Texture.LoadImageFile("Resources/Concrete/Concrete042A_1K_Color.jpg"));
        _terrainModelShader.AddTexture("dirtTexture", Texture.LoadImageFile("Resources/Dirt/Ground023_1K_Color.jpg"));
        _terrainModelShader.AddTexture("grassTexture", Texture.LoadImageFile("Resources/Grass/Grass001_1K_Color.jpg"));

        _shadedTerrainModel = new ShadedModel(_terrainModel.Model, _terrainModelShader,
            BufferUsageAccessFrequency.Static, BufferUsageAccessNature.Draw);

        _terrain = new Terrain();
        _fern = new Fern();
        _grass = new Grass(_terrain);

        _sky = new Sky();

        _map = new Map(_camera);

        // _scene = new TestScene(_camera);
    }

    private int _frameCount;
    private double _timeSinceLastFpsUpdate;
    private double _fps;

    protected override void OnUpdateFrame(FrameEventArgs e)
    {
        base.OnUpdateFrame(e);
        _controls.Update(e.Time, _camera);
        _terrain.Update(_camera);

        _frameCount++;
        _timeSinceLastFpsUpdate += e.Time;
        if (_timeSinceLastFpsUpdate < 1.0) return;
        _fps = _frameCount / _timeSinceLastFpsUpdate;
        _frameCount = 0;
        _timeSinceLastFpsUpdate = 0.0;
    }

    //TODO: crashes when streamed on discord
    protected override void OnRenderFrame(FrameEventArgs e)
    {
        base.OnRenderFrame(e);

        Renderer.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        //TODO: Render Skybox at the end with max. depth to utilize depth testing
        Renderer.DepthTesting = false;
        _sky.Draw(_camera);
        Renderer.DepthTesting = true;

        // _scene.OnRenderFrame();
        if (_renderTerrainModel)
        {
            _terrainModelShader.SetUniform("modelViewMat", _camera.ViewMat);
            _terrainModelShader.SetUniform("projectionMat", _camera.ProjectionMat);
            _shadedTerrainModel.Draw();
        }
        else
        {
            if (Debug)
            {
                GL.Enable(EnableCap.PolygonOffsetFill);
                GL.PolygonOffset(1, 1);
            }
            _terrain.Draw(_camera, _sky);
            
            if (Debug)
            {
                GL.Disable(EnableCap.PolygonOffsetFill);
                
                Renderer.PolygonMode = PolygonMode.Line;
                _terrain.Draw(_camera, _sky, true);
                Renderer.PolygonMode = PolygonMode.Fill;
            }
        }

        _grass.Draw(_camera);
        // _fern.Draw();

        _controller.RenderFrame(this, (float)e.Time, () =>
        {
            ImGui.ShowDemoWindow();
            _terrain.Window();
            _grass.Window();
            _sky.Window();
            Profiler.Window();
            InfoWindow();
        });

        //GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0);
        SwapBuffers();
    }

    private void InfoWindow()
    {
        var windowFlags = _map.Hovered ? ImGuiWindowFlags.NoScrollWithMouse : ImGuiWindowFlags.None;
        ImGui.Begin("Info", windowFlags);

        var mapHeight = Math.Clamp(ImGui.GetWindowHeight() * 0.4f, 80f, 200f);
        _map.Window(new Vector2(0f, mapHeight));
        
        ImGui.AlignTextToFramePadding();
        ImGui.Value("Fps", (float)_fps);
        
        ImGui.SameLine();
        if (ImGui.Checkbox("VSync", ref _vsync))
            VSync = _vsync ? VSyncMode.On : VSyncMode.Off;
        
        var locateButtonSize = ImGui.GetFrameHeight();
        ImGui.SameLine(ImGui.GetContentRegionAvail().X + ImGui.GetStyle().WindowPadding.X - locateButtonSize);
        var disabled = _map.FollowPlayer;
        if (disabled) ImGui.BeginDisabled();
        if (ImGui.Button("+", new System.Numerics.Vector2(locateButtonSize)))
            _map.FollowPlayer = true;
        if (disabled) ImGui.EndDisabled();

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

        if (ImGui.Button("Copy Location"))
            ClipboardString =
                $"{latLon.X.ToString(CultureInfo.InvariantCulture)}, {latLon.Y.ToString(CultureInfo.InvariantCulture)}";

        if (ImGui.Button("Home Sweet Home!"))
            _camera.Position = Coordinate.Epsg25832(new Vector2(347000, 5673000), _camera.Position.Y).World();
        ImGui.SameLine();
        if (ImGui.Button("Schloss Burg"))
            _camera.Position = Coordinate.Epsg25832(new Vector2(370552.5815306349f, 5666744.753800459f), _camera.Position.Y).World();

        ImGui.Value("Terrain Height", TerrainData.GetHeightAt(cameraPosition.TerrainTile()) ?? float.NaN);
        
        if (ImGui.Checkbox("Render Terrain Model", ref _renderTerrainModel))
        {
            _camera.Position += (_renderTerrainModel ? -1.0f : 1.0f) * new Vector3(1000.0f, 0.0f, 1001.0f);
        }

        ImGui.End();
    }

    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);
        Renderer.Viewport = new Box2i(0, 0, e.Width, e.Height);
    
        _controller.OnResize(e);
        _camera.Resize(e.Size);
    }

    protected override void OnFocusedChanged(FocusedChangedEventArgs e)
    {
        base.OnFocusedChanged(e);
        if (!e.IsFocused) _controls.Pause(true);
        ImGuiController.OnFocusedChanged(e);
    }

    protected override void OnKeyDown(KeyboardKeyEventArgs e)
    {
        base.OnKeyDown(e);
        ImGuiController.OnKeyDown(e);
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        ImGuiController.OnTextInput(e);
    }

    protected override void OnKeyUp(KeyboardKeyEventArgs e)
    {
        base.OnKeyUp(e);
        ImGuiController.OnKeyUp(e);
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        ImGuiController.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        ImGuiController.OnMouseUp(e);
    }

    protected override void OnMouseMove(MouseMoveEventArgs e)
    {
        base.OnMouseMove(e);
        ImGuiController.OnMouseMove(e);
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        ImGuiController.OnMouseWheel(e);
    }

    protected override void OnUnload()
    {
        base.OnUnload();
        // _scene.OnUnload();
        Resources.DisposeAll();
    }

    //TODO: push pop debug group (error handler (logger class))
    private static void OpenGlDebugCallback(DebugSource source, DebugType type, int id, DebugSeverity severity,
        int length, IntPtr message, IntPtr userParam)
    {
        //131185: allocation of video memory
        if (id == 131185) return;

        var typeString = type switch
        {
            DebugType.DontCare => "Dont Care",
            DebugType.DebugTypeError => "Error",
            DebugType.DebugTypeDeprecatedBehavior => "Deprecated Behavior",
            DebugType.DebugTypeUndefinedBehavior => "Undefined Behavior",
            DebugType.DebugTypePortability => "Portability",
            DebugType.DebugTypePerformance => "Performance",
            DebugType.DebugTypeOther => "Other",
            DebugType.DebugTypeMarker => "Marker",
            DebugType.DebugTypePushGroup => "Push Group",
            DebugType.DebugTypePopGroup => "Pop Group",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
        var severityString = severity switch
        {
            DebugSeverity.DontCare => "Dont Care",
            DebugSeverity.DebugSeverityNotification => "Notification",
            DebugSeverity.DebugSeverityHigh => "High",
            DebugSeverity.DebugSeverityMedium => "Medium",
            DebugSeverity.DebugSeverityLow => "Low",
            _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, null)
        };
        var sourceString = source switch
        {
            DebugSource.DontCare => "Dont Care",
            DebugSource.DebugSourceApi => "Api",
            DebugSource.DebugSourceWindowSystem => "Window System",
            DebugSource.DebugSourceShaderCompiler => "Shader Compiler",
            DebugSource.DebugSourceThirdParty => "Third Party",
            DebugSource.DebugSourceApplication => "Application",
            DebugSource.DebugSourceOther => "Other",
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, null)
        };
        var log =
            $"OpenGL ({id}): [{typeString}] <{severityString}> @{sourceString} '{Marshal.PtrToStringUTF8(message)}'";
        if (type is DebugType.DebugTypeError)
            throw new Exception(log);
        Console.WriteLine(log);
    }
}