using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using recreate_nrw.Render;
using recreate_nrw.Render.UI;

namespace recreate_nrw.Foliage;

public class Fern
{
    private readonly Image _image;

    public Fern()
    {
        var texture = new Framebuffer(100, 200, false, false, false);
        texture.Use(() =>
        {
            var oldClearColor = Renderer.ClearColor;
            Renderer.ClearColor = new Color4(0.5f, 1.0f, 0.0f, 0.0f);

            Renderer.Clear(ClearBufferMask.ColorBufferBit);
            var image = new Image(
                Texture.LoadImageFile("Resources/awesomeface.png"),
                new Box2(-1.0f, -0.5f, 1.0f, 0.5f),
                false
            );
            image.Draw();

            Renderer.ClearColor = oldClearColor;
        });
        _image = new Image(texture, new Box2(-1.0f, -1.0f, 0.0f, 0.0f), false);
    }

    public void Draw()
    {
        Renderer.Blending = true;
        _image.Draw();
        Renderer.Blending = false;
    }
}