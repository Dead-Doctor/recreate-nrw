using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using recreate_nrw.Render;
using recreate_nrw.Render.UI;
using recreate_nrw.Util;

namespace recreate_nrw;

public class Map
{
    private readonly Framebuffer _framebuffer = new(200, 150, true, true, false);
    private Vector2 _center = Vector2.Zero;
    private float _halfSize = 100.0f;
    private readonly Image _image;

    public Map()
    {
        _image = new Image(Texture.LoadImageFile("Resources/awesomeface.png"), CalculatePosition(), true);
        Redraw();
    }
    
    private Box2 CalculatePosition() =>
        new(
            _center.X - _halfSize / _framebuffer.Width,
            _center.Y - _halfSize / _framebuffer.Height,
            _center.X + _halfSize / _framebuffer.Width,
            _center.Y + _halfSize / _framebuffer.Height
        );

    private void Redraw()
    {
        _framebuffer.Use(() =>
        {
            var oldClearColor = Renderer.ClearColor;
            Renderer.ClearColor = new Color4(0.5f, 1.0f, 0.0f, 1.0f);

            Renderer.Clear(ClearBufferMask.ColorBufferBit);
            _image.Position = CalculatePosition();
            _image.Draw();

            Renderer.ClearColor = oldClearColor;
        });
    }

    public void Window()
    {
        ImGui.Begin("Map");

        var contentRegionAvail = ImGui.GetContentRegionAvail();
        if ((int)contentRegionAvail.X != _framebuffer.Width)
        {
            _framebuffer.Resize((int)contentRegionAvail.X, _framebuffer.Height);
            Redraw();
        }

        ImGui.Image((IntPtr)_framebuffer.GetHandle(), contentRegionAvail with { Y = _framebuffer.Height });
        if (ImGuiExtension.Vector2("Center", ref _center)) Redraw();
        if (ImGui.DragFloat("Half Size", ref _halfSize)) Redraw();
        
        ImGui.End();
    }
}