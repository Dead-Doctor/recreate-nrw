using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace recreate_nrw.Render;

public static class Renderer
{
    private static Color4 _clearColor;
    private static bool _depthTesting; //GL_UNSIGNED_NORMALIZED
    private static bool _blending;
    private static bool _backFaceCulling;
    private static PolygonMode _polygonMode = PolygonMode.Fill;
    private static Box2i _viewport;

    public static Color4 ClearColor
    {
        get => _clearColor;
        set
        {
            if (_clearColor == value) return;
            GL.ClearColor(value.R, value.G, value.B, value.A);
            _clearColor = value;
        }
    }

    public static bool DepthTesting
    {
        get => _depthTesting;
        set
        {
            if (_depthTesting == value) return;
            if (value)
                GL.Enable(EnableCap.DepthTest);
            else
                GL.Disable(EnableCap.DepthTest);
            _depthTesting = value;
        }
    }

    public static bool Blending
    {
        get => _blending;
        set
        {
            if (_blending == value) return;
            if (value)
                GL.Enable(EnableCap.Blend);
            else
                GL.Disable(EnableCap.Blend);
            _blending = value;
        }
    }

    public static bool BackFaceCulling
    {
        get => _backFaceCulling;
        set
        {
            if (_backFaceCulling == value) return;
            if (value)
                GL.Enable(EnableCap.CullFace);
            else
                GL.Disable(EnableCap.CullFace);
            _backFaceCulling = value;
        }
    }

    public static PolygonMode PolygonMode
    {
        get => _polygonMode;
        set
        {
            if (_polygonMode == value) return;
            GL.PolygonMode(MaterialFace.FrontAndBack, value);
            _polygonMode = value;
        }
    }

    public static Box2i Viewport
    {
        get => _viewport;
        set
        {
            if (_viewport == value) return;
            GL.Viewport(value.Min.X, value.Min.Y, value.Max.X, value.Max.Y);
            _viewport = value;
        }
    }

    public static void BlendingFunction(BlendingFactor sourceFactor, BlendingFactor destinationFactor)
    {
        GL.BlendFunc(sourceFactor, destinationFactor);
    }

    public static void Clear(ClearBufferMask clearBufferMask)
    {
        GL.Clear(clearBufferMask);
    }
}