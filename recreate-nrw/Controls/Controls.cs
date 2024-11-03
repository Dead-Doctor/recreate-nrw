using ImGuiNET;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using recreate_nrw.Render;

namespace recreate_nrw.Controls;

public class Controls
{

    private bool _paused = true;

    private readonly Window _window;
    private IController _controller = null!;

    public Controls(Window window, IController controller)
    {
        _window = window;
        SwitchController(controller);
    }

    public void SwitchController(IController controller)
    {
        _controller = controller;
        _controller.Activate(_window.Camera);
    }

    public void Update(double deltaTime)
    {
        if (!_window.IsFocused) return;
        
        if (_window.KeyboardState.IsKeyPressed(Keys.Escape)) Pause(!_paused);
        if (_paused) return;

        _controller.Update(_window.KeyboardState, _window.MouseState, deltaTime);
    }

    public static Func<KeyboardState, float> Axis(Keys positive, Keys negative) => input =>
        (input.IsKeyDown(positive) ? 1.0f : 0.0f) - (input.IsKeyDown(negative) ? 1.0f : 0.0f);


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

    public void Window()
    {
        _controller.InfoWindow();
    }
}

public interface IController
{
    void Activate(Camera camera);
    void Update(KeyboardState keyboard, MouseState mouse, double deltaTime);
    void InfoWindow();
}