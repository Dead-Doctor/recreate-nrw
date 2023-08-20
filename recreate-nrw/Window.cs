﻿using System.Globalization;
using System.Runtime.InteropServices;
using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using recreate_nrw.Foliage;
using recreate_nrw.Render;
using recreate_nrw.Terrain;
using recreate_nrw.Util;

namespace recreate_nrw;

public class Window : GameWindow
{
    private bool _vsync = true;

    private readonly TerrainModel _terrainModel;

    // = null! equivalent to Kotlin's lateinit
    // private TestScene _scene = null!;
    private ImGuiController _controller = null!;
    private Camera _camera = null!;
    private Controls _controls = null!;
    private Shader _terrainModelShader = null!;
    private ShadedModel _shadedTerrainModel = null!;
    private bool _renderTerrainModel = false;

    private Terrain.Terrain _terrain = null!;
    private Fern _fern = null!;
    
    private readonly Vector3 _lightDir = new Vector3(1.0f, -1.0f, 1.0f).Normalized();
    public bool Debug;

    public Window() : base(
        GameWindowSettings.Default,
        new NativeWindowSettings
            {Title = "Recreate NRW", Size = (960, 540), APIVersion = new Version(4, 6)}
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
        GL.DebugMessageCallback(_openGlDebugCallback, (IntPtr) 0);
#endif
        Renderer.ClearColor = new Color4(0.2f, 0.3f, 0.3f, 1.0f);
        Renderer.DepthTesting = true;
        Renderer.BackFaceCulling = true;
        Renderer.BlendingFunction(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        _controller = new ImGuiController(ClientSize.X, ClientSize.Y);

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

        _shadedTerrainModel = new ShadedModel(_terrainModel.Model, _terrainModelShader);

        _terrain = new Terrain.Terrain(_lightDir);
        _fern = new Fern();

        // _scene = new TestScene(_camera);
    }

    protected override void OnRenderFrame(FrameEventArgs e)
    {
        base.OnRenderFrame(e);

        Renderer.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        if (Debug)
            Renderer.PolygonMode = PolygonMode.Line;

        // _scene.OnRenderFrame();
        if (_renderTerrainModel)
        {
            _terrainModelShader.SetUniform("modelViewMat", _camera.ViewMat);
            _terrainModelShader.SetUniform("projectionMat", _camera.ProjectionMat);
            _shadedTerrainModel.Draw();
        }
        else
        {
            _terrain.Draw(_camera);
        }

        if (Debug)
            Renderer.PolygonMode = PolygonMode.Fill;

        ImGui.DockSpaceOverViewport(ImGui.GetMainViewport(),
            ImGuiDockNodeFlags.PassthruCentralNode | ImGuiDockNodeFlags.NoDockingInCentralNode);

        ImGui.ShowDemoWindow();
        _terrain.Window();
        _fern.Draw();
        InfoWindow();

        _controller.Render();
        ImGuiController.CheckGLError("End of frame");

        SwapBuffers();
    }

    private int _frameCount;
    private double _timeSinceLastFpsUpdate;
    private double _fps;

    protected override void OnUpdateFrame(FrameEventArgs e)
    {
        base.OnUpdateFrame(e);
        _controls.Update(e.Time, _camera);
        _controller.Update(this, (float) e.Time);
        
        _frameCount++;
        _timeSinceLastFpsUpdate += e.Time;
        if (_timeSinceLastFpsUpdate < 1.0) return;
        _fps = _frameCount / _timeSinceLastFpsUpdate;
        _frameCount = 0;
        _timeSinceLastFpsUpdate = 0.0;
    }

    private void InfoWindow()
    {
        ImGui.Begin("Info");

        ImGui.Value("Fps", (float) _fps);
        if (ImGui.Checkbox("VSync", ref _vsync))
            VSync = _vsync ? VSyncMode.On : VSyncMode.Off;
        
        if (ImGuiExtension.ImGuiVector3("Position", _camera.Position, out var newPosition))
            _camera.Position = newPosition;

        var terrainData = Coordinate.World(_camera.Position).Epsg25832();
        if (ImGuiExtension.ImGuiVector2("EPSG:25832", terrainData, out var newCoordinates))
            _camera.Position = Coordinate.Epsg25832(newCoordinates, _camera.Position.Y).World();

        if (ImGui.Button("To Coords"))
            ImGuiExtension.OpenUrl(
                $"https://epsg.io/transform#s_srs=25832&t_srs=4326&ops=1149&x={terrainData.X.ToString(CultureInfo.InvariantCulture)}&y={terrainData.Y.ToString(CultureInfo.InvariantCulture)}");

        if (_camera.Position.X is >= 0.0f and < Coordinate.TerrainTileSize &&
            _camera.Position.Z is >= 0.0f and < Coordinate.TerrainTileSize)
            ImGui.Value("Tile",
                _terrain.Tile00.Data[(int) _camera.Position.Z * Coordinate.TerrainTileSize + (int) _camera.Position.X]);

        if (ImGui.Button("Home Sweet Home!"))
            _camera.Position = Coordinate.Epsg25832(new Vector2(347200, 5673200), _camera.Position.Y).World();

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

        _controller.WindowResized(e.Width, e.Height);
        _camera.Resize(e.Size);
    }

    protected override void OnFocusedChanged(FocusedChangedEventArgs e)
    {
        base.OnFocusedChanged(e);
        if (!e.IsFocused) _controls.Pause(true);
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        _controller.PressChar((char) e.Unicode);
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        _controller.MouseScroll(e.Offset);
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
            DebugType.DebugTypePortability => "Portability", DebugType.DebugTypePerformance => "Performance",
            DebugType.DebugTypeOther => "Other", DebugType.DebugTypeMarker => "Marker",
            DebugType.DebugTypePushGroup => "Push Group", DebugType.DebugTypePopGroup => "Pop Group",
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
            $@"OpenGL ({id}): [{typeString}] <{severityString}> @{sourceString} '{Marshal.PtrToStringUTF8(message)}'";
        if (type is DebugType.DebugTypeError) throw new Exception(log);
        Console.WriteLine(log);
    }
}