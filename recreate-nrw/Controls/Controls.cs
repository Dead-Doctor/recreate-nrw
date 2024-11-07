using ImGuiNET;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using recreate_nrw.Render;
using recreate_nrw.Util;

namespace recreate_nrw.Controls;

public class Controls
{

    private bool _paused = true;

    private readonly Window _window;
    private IController _controller = null!;

    public Controls(Window window, IController controller)
    {
        _window = window;
        Input.RegisterAxis("vertical", Keys.D, Keys.E);
        Input.RegisterAxis("horizontal", Keys.S, Keys.F);
        Input.RegisterAxis("accelerate", Keys.A, Keys.Space);
        Input.RegisterKeyBind("sprint", Keys.B);
        Input.RegisterKeyBind("zoom", Keys.V);
        SwitchController(controller);
    }

    public void SwitchController(IController controller)
    {
        _controller = controller;
        _controller.Activate(_window.Camera);
    }

    public void Update(double deltaTime)
    {
        var keyboard = _window.KeyboardState;
        Input.Update(keyboard, _window.MouseState);
        if (!_window.IsFocused) return;

        var pressedEscape = keyboard.IsKeyPressed(Keys.Escape);
        if (Input.WantsToAssignKeyBinding)
        {
            if (!_paused || pressedEscape)
            {
                Input.SelectKeyBinding(null);
                return;
            }

            const int lastKeyCode = (int)Keys.Menu;
            for (var keyCode = 0; keyCode <= lastKeyCode; keyCode++)
            {
                var key = (Keys)keyCode;
                if (keyboard.IsKeyPressed(key))
                {
                    Input.SelectKeyBinding(key);
                }
            }
            
            return;
        }
        if (pressedEscape) Pause(!_paused);
        if (_paused) return;

        _controller.Update(deltaTime);
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

    public void InfoWindow()
    {
        _controller.InfoWindow();
    }
}

public static class Input
{
    private static readonly Dictionary<string, Keys> KeyBindings = new();
    private static readonly Dictionary<string, (Keys, Keys)> AxisBindings = new();
    
    public static void RegisterKeyBind(string id, Keys key)
    {
        if (!KeyBindings.TryAdd(id, key)) throw new Exception($"Key bind '{id}' already registered");
    }
    public static void RegisterAxis(string id, Keys negative, Keys positive)
    {
        if (!AxisBindings.TryAdd(id, (negative, positive))) throw new Exception($"Axis binding '{id}' already registered");
    }
    
    
    private static KeyboardState _keyboardState = null!;
    private static MouseState _mouseState = null!;
    
    public static void Update(KeyboardState keyboardState, MouseState mouseState)
    {
        _keyboardState = keyboardState;
        _mouseState = mouseState;
    }

    private static string? _editingKeyBinding;
    private static int _editingKeyBindingType;
    public static void Window()
    {
        ImGui.Begin("Input");
        const float labelWidth = 100f;
        
        ImGui.SeparatorText("Key bindings (press ESC to cancel)");
        foreach (var (id, key) in KeyBindings)
        {
            ImGui.Text(id);
            ImGui.SameLine(labelWidth);
            ImGui.PushID(id);
            KeyBindButton(id, 0, key);
            ImGui.PopID();
        }
        
        ImGui.SeparatorText("Axis bindings (negative, positive)");
        foreach (var (id, keys) in AxisBindings)
        {
            ImGui.Text(id);
            ImGui.SameLine(labelWidth);
            ImGui.PushID(id);
            KeyBindButton(id, 1, keys.Item1);
            ImGui.SameLine();
            KeyBindButton(id, 2, keys.Item2);
            ImGui.PopID();
        }
        ImGui.End();
    }

    private static void KeyBindButton(string id, int type, Keys key)
    {
        const float buttonWidth = 50f;
        var buttonSize = new Vector2(buttonWidth, ImGui.GetFrameHeight()).ToSystem();
        var activeButtonColor = new Vector4(0.06f, 0.53f, 0.98f, 1f).ToSystem();
        
        ImGui.PushID(type);
        if (_editingKeyBinding == id && _editingKeyBindingType == type)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, activeButtonColor);
            ImGui.Button("...", buttonSize);
            ImGui.PopStyleColor();
        }
        else if (ImGui.Button(key.ToString(), buttonSize))
        {
            _editingKeyBinding = id;
            _editingKeyBindingType = type;
        }
        ImGui.PopID();
    }

    public static bool WantsToAssignKeyBinding => _editingKeyBinding != null;
    
    public static void SelectKeyBinding(Keys? key)
    {
        //TODO: save keybindings in file (like imgui.ini)
        if (key.HasValue)
            switch (_editingKeyBindingType)
            {
                case 0:
                    KeyBindings[_editingKeyBinding!] = key.Value;
                    break;
                case 1:
                case 2:
                    var axisBinding = AxisBindings[_editingKeyBinding!];
                    if (_editingKeyBindingType == 1) axisBinding.Item1 = key.Value;
                    else axisBinding.Item2 = key.Value;
                    AxisBindings[_editingKeyBinding!] = axisBinding;
                    break;
            }
        _editingKeyBinding = null;
    }

    public static bool Held(string id) => _keyboardState.IsKeyDown(KeyBindings[id]);
    public static bool Pressed(string id) => _keyboardState.IsKeyPressed(KeyBindings[id]);
    public static float Axis(string id) => (_keyboardState.IsKeyDown(AxisBindings[id].Item2) ? 1.0f : 0.0f)
                                           - (_keyboardState.IsKeyDown(AxisBindings[id].Item1) ? 1.0f : 0.0f);
    public static Vector2 Analog() => _mouseState.Delta;
}

public interface IController
{
    void Activate(Camera camera);
    void Update(double deltaTime);
    void InfoWindow();
}