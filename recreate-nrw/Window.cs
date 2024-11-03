// #define INCLUDE_TERRAIN_MODEL

using System.Globalization;
using System.Runtime.InteropServices;
using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using recreate_nrw.Controls;
using recreate_nrw.Controls.Controller;
using recreate_nrw.Ground;
using recreate_nrw.Foliage;
using recreate_nrw.Render;
using recreate_nrw.Render.UI;
using recreate_nrw.Util;

namespace recreate_nrw;

public class Window : GameWindow
{
    private bool _vsync = true;

    // private TestScene _scene = null!;
    private ImGuiController _controller = null!;
    public readonly Camera Camera = new();
    private Controls.Controls _controls = null!;
    private readonly IController[] _controllers = { new Creative() };
    private int _currentController;
#if INCLUDE_TERRAIN_MODEL
    private readonly TerrainModel _terrainModel;
    private Shader _terrainModelShader = null!;
    private ShadedModel _shadedTerrainModel = null!;
    private bool _renderTerrainModel;
    private readonly Vector3 _lightDir = new Vector3(1.0f, -1.0f, 1.0f).Normalized();
#endif

    private Terrain _terrain = null!;
    private readonly Fern? _fern = null!;
    private Grass _grass = null!;

    private Sky _sky = null!;

    private Map _map = null!;

    private readonly Profiler _initializingTask;

    public Window() : base(
        GameWindowSettings.Default,
        new NativeWindowSettings
            { Title = "Recreate NRW", Size = (960, 540), APIVersion = new Version(4, 6) }
    )
    {
        _initializingTask = Profiler.Create("Initialize");
        var constructorTask = _initializingTask.Start("Constructor");
        VSync = _vsync ? VSyncMode.On : VSyncMode.Off;
#if INCLUDE_TERRAIN_MODEL
        var loadTerrainModelData = constructorTask.Start("Loading terrain model data");
        var heightmap = new Heightmap(new Vector2i(347, 5673));
        heightmap.LoadTile(new Vector2i(0, 0));
        _terrainModel = new TerrainModel(heightmap, new Vector2i(0, 0), 1000u);
        loadTerrainModelData.Stop();
#endif
        constructorTask.Stop();
    }

    /// <summary>
    /// Prevent Callback from being garbage collected.
    /// </summary>
    private readonly DebugProc _openGlDebugCallback = OpenGlDebugCallback;

    protected override void OnLoad()
    {
        var loadingTask = _initializingTask.Start("Load");
        loadingTask.Start("Base Load", _ => base.OnLoad());
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

        var setupClasses = loadingTask.Start("Setup Classes");
        setupClasses.Start("Setup ImGui", p => _controller = new ImGuiController(ClientSize, p));
        setupClasses.Start("Setup Camera, Controls", _ =>
        {
            Camera.Init(ClientSize);
            _controls = new Controls.Controls(this, _controllers[_currentController]);
        });

#if INCLUDE_TERRAIN_MODEL
        var setupTerrainModel = setupClasses.Start("Setup Terrain Model");
        _terrainModelShader = new Shader("terrainModel");
        _terrainModelShader.AddUniform<Matrix4>("modelViewMat");
        _terrainModelShader.AddUniform<Matrix4>("projectionMat");
        _terrainModelShader.AddUniform("lightDir", _lightDir);
        
        var loadingTextures = setupTerrainModel.Start("Loading Textures");
        _terrainModelShader.AddTexture("concreteTexture", Texture.LoadImageFile("Resources/Concrete/Concrete042A_1K_Color.jpg"));
        _terrainModelShader.AddTexture("dirtTexture", Texture.LoadImageFile("Resources/Dirt/Ground023_1K_Color.jpg"));
        _terrainModelShader.AddTexture("grassTexture", Texture.LoadImageFile("Resources/Grass/Grass001_1K_Color.jpg"));
        loadingTextures.Stop();
        
        _shadedTerrainModel = new ShadedModel(_terrainModel.Model, _terrainModelShader,
            BufferUsageAccessFrequency.Static, BufferUsageAccessNature.Draw);
        setupTerrainModel.Stop();
#endif

        setupClasses.Start("Setup Ground", _ => _terrain = new Terrain());
        setupClasses.Start("Setup Foliage", _ =>
        {
            // _fern = new Fern();
            _grass = new Grass(_terrain);
        });

        _sky = new Sky();
        _map = new Map(Camera, _terrain);
        setupClasses.Stop();

        // _scene = new TestScene(_camera);
        loadingTask.Stop();
        _initializingTask.Stop();
    }

    private int _frameCount;
    private double _timeSinceLastFpsUpdate;
    private double _fps;

    protected override void OnUpdateFrame(FrameEventArgs e)
    {
        base.OnUpdateFrame(e);
        _controls.Update(e.Time);
        _terrain.Update(Camera);

        _frameCount++;
        _timeSinceLastFpsUpdate += e.Time;
        if (_timeSinceLastFpsUpdate < 1.0) return;
        _fps = _frameCount / _timeSinceLastFpsUpdate;
        _frameCount = 0;
        _timeSinceLastFpsUpdate = 0.0;
    }

    private static void Captured(Profiler? profiler, string name, Action<Profiler?> action)
    {
        var task = profiler?.Start(name);
        action(task);
        task?.Stop();
    }

    private static volatile bool _captureFrame;
    private static int _capturedFrames;

    //TODO: crashes when streamed on discord
    protected override void OnRenderFrame(FrameEventArgs e)
    {
        Profiler? profiler = null;
        if (_captureFrame)
        {
            _captureFrame = false;
            profiler = Profiler.Create($"Render Frame #{_capturedFrames++}");
        }

        Captured(profiler, "Base", _ => base.OnRenderFrame(e));

        Renderer.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        Captured(profiler, "Sky", _ =>
        {
            //TODO: Render Skybox at the end with max. depth to utilize depth testing
            Renderer.DepthTesting = false;
            _sky.Draw(Camera);
            Renderer.DepthTesting = true;
        });

        // _scene.OnRenderFrame();

        Captured(profiler, "Terrain", _ =>
        {
#if INCLUDE_TERRAIN_MODEL
        if (_renderTerrainModel)
        {
            _terrainModelShader.SetUniform("modelViewMat", _camera.ViewMat);
            _terrainModelShader.SetUniform("projectionMat", _camera.ProjectionMat);
            _shadedTerrainModel.Draw();
        }
        else
        {
#endif
            _terrain.Draw(Camera, _sky);
#if INCLUDE_TERRAIN_MODEL
        }
#endif
        });

        Captured(profiler, "Foliage", _ =>
        {
            _grass.Draw(Camera);
            _fern?.Draw();
        });


        Captured(profiler, "ImGui", task => _controller.RenderFrame(this, (float)e.Time, () =>
        {
            Captured(task, "Demo", _ => ImGui.ShowDemoWindow());
            Captured(task, "Terrain", _ => _terrain.Window(Camera));
            Captured(task, "Grass", _ => _grass.Window());
            Captured(task, "Sky", _ => _sky.Window());
            Captured(task, "Profiler", _ => Profiler.Window());
            Captured(task, "Info", _ => InfoWindow());
        }));

        Captured(profiler, "Swap Buffers", _ => SwapBuffers());

        profiler?.Stop();
    }

    private void InfoWindow()
    {
        var windowFlags = _map.Hovered ? ImGuiWindowFlags.NoScrollWithMouse : ImGuiWindowFlags.None;
        ImGui.Begin("Info", windowFlags);
        
        ImGui.AlignTextToFramePadding();
        ImGui.Value("Fps", (float)_fps);

        ImGui.SameLine();
        if (ImGui.Checkbox("VSync", ref _vsync))
            VSync = _vsync ? VSyncMode.On : VSyncMode.Off;

        ImGui.SameLine();
        if (ImGui.Button("Capture Frame")) _captureFrame = true;
        
        ImGui.Separator();
        
        var mapHeight = Math.Clamp(ImGui.GetWindowHeight() * 0.4f, 80f, 200f);
        _map.Window(new Vector2(0f, mapHeight));
        
        var latLon = Coordinate.World(Camera.Position).Wgs84();
        ImGui.AlignTextToFramePadding();
        ImGui.Text($"{latLon.X:0.000}°N {latLon.Y:0.000}°E");
        
        ImGui.SameLine();
        if (ImGui.Button("Copy"))
            ClipboardString =
                $"{latLon.X.ToString(CultureInfo.InvariantCulture)}, {latLon.Y.ToString(CultureInfo.InvariantCulture)}";

        var locateButtonSize = ImGui.GetFrameHeight();
        ImGui.SameLine(ImGui.GetContentRegionAvail().X + ImGui.GetStyle().WindowPadding.X - locateButtonSize);
        var disabled = _map.FollowPlayer;
        if (disabled) ImGui.BeginDisabled();
        if (ImGui.Button("+", new System.Numerics.Vector2(locateButtonSize)))
            _map.FollowPlayer = true;
        if (disabled) ImGui.EndDisabled();

        ImGui.Separator();

        if (ImGui.Combo("Controller", ref _currentController, "Creative"))
        {
            _controls.SwitchController(_controllers[_currentController]);
        }

        _controls.Window();

#if INCLUDE_TERRAIN_MODEL
        if (ImGui.Checkbox("Render Terrain Model", ref _renderTerrainModel))
        {
            _camera.Position += (_renderTerrainModel ? -1.0f : 1.0f) * new Vector3(1000.0f, 0.0f, 1001.0f);
        }
#endif

        ImGui.End();
    }

    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);
        Renderer.Viewport = new Box2i(0, 0, e.Width, e.Height);

        _controller.OnResize(e);
        Camera.Resize(e.Size);
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