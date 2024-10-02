using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using recreate_nrw.Render;
using ErrorCode = OpenTK.Graphics.OpenGL4.ErrorCode;

namespace recreate_nrw.Util
{
    public sealed class ImGuiController : IDisposable
    {
        private bool _frameBegun;

        private int _vertexArray;
        private int _vertexBuffer;
        private int _vertexBufferSize;
        private int _indexBuffer;
        private int _indexBufferSize;

        private Texture _fontTexture = null!;
        private Shader _shader = null!;

        private int _windowWidth;
        private int _windowHeight;

        private readonly System.Numerics.Vector2 _scaleFactor = System.Numerics.Vector2.One;
        
        public ImGuiController(int width, int height)
        {
            var context = ImGui.CreateContext();
            ImGui.SetCurrentContext(context);
            
            WindowResized(width, height);
            
            var io = ImGui.GetIO();
            io.Fonts.AddFontDefault();
            io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;
            io.ConfigFlags |= ImGuiConfigFlags.DockingEnable;
            
            for (var i = 0; i < (int)ImGuiKey.COUNT; i++)
                io.KeyMap[i] = (int)Enum.Parse(typeof(Keys), ((ImGuiKey) i).ToString().Replace("Arrow", ""));
            
            CreateDeviceResources();
            
            ImGui.NewFrame();
            _frameBegun = true;
        }

        public void WindowResized(int width, int height)
        {
            _windowWidth = width;
            _windowHeight = height;
            
            var io = ImGui.GetIO();
            io.DisplaySize = new System.Numerics.Vector2(_windowWidth / _scaleFactor.X, _windowHeight / _scaleFactor.Y);
            io.DisplayFramebufferScale = _scaleFactor;
        }

        private void CreateDeviceResources()
        {
            _vertexBufferSize = 10000;
            _indexBufferSize = 2000;

            var prevVao = GL.GetInteger(GetPName.VertexArrayBinding);
            var prevArrayBuffer = GL.GetInteger(GetPName.ArrayBufferBinding);

            _vertexArray = GL.GenVertexArray();
            GL.BindVertexArray(_vertexArray);
            LabelObject(ObjectLabelIdentifier.VertexArray, _vertexArray, "VAO: ImGui");

            _vertexBuffer = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vertexBuffer);
            LabelObject(ObjectLabelIdentifier.Buffer, _vertexBuffer, "VBO: ImGui");
            GL.BufferData(BufferTarget.ArrayBuffer, _vertexBufferSize, IntPtr.Zero, BufferUsageHint.DynamicDraw);

            _indexBuffer = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _indexBuffer);
            LabelObject(ObjectLabelIdentifier.Buffer, _indexBuffer, "EBO: ImGui");
            GL.BufferData(BufferTarget.ElementArrayBuffer, _indexBufferSize, IntPtr.Zero, BufferUsageHint.DynamicDraw);

            RecreateFontDeviceTexture();

            _shader = new Shader("imgui");
            _shader.AddUniform<Matrix4>("projection_matrix");
            _shader.AddTexture("in_fontTexture");

            var stride = Unsafe.SizeOf<ImDrawVert>();
            var offset = 0;
            RegisterVertexAttribute(_shader, "in_position", 2, VertexAttribPointerType.Float, false, stride,
                ref offset);
            RegisterVertexAttribute(_shader, "in_texCoord", 2, VertexAttribPointerType.Float, false, stride,
                ref offset);
            RegisterVertexAttribute(_shader, "in_color", 4, VertexAttribPointerType.UnsignedByte, true, stride,
                ref offset);

            GL.BindVertexArray(prevVao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, prevArrayBuffer);
        }

        private static void LabelObject(ObjectLabelIdentifier objLabelIdent, int glObject, string name)
        {
            GL.ObjectLabel(objLabelIdent, glObject, name.Length, name);
        }

        private static void RegisterVertexAttribute(Shader shader, string name, int count, VertexAttribPointerType type,
            bool normalized, int stride, ref int offset)
        {
            var index = shader.GetAttribLocation(name);
            GL.EnableVertexAttribArray(index);
            GL.VertexAttribPointer(index, count, type, normalized, stride, offset);
            offset += count * type switch
            {
                VertexAttribPointerType.UnsignedByte => sizeof(byte),
                VertexAttribPointerType.Float => sizeof(float),
                _ => throw new Exception($"Unknown type: {type}")
            };
        }

        private void RecreateFontDeviceTexture()
        {
            var io = ImGui.GetIO();
            io.Fonts.GetTexDataAsRGBA32(out IntPtr pixels, out var width, out var height, out _);
            var textureData = new byte[width * height * 4];
            Marshal.Copy(pixels, textureData, 0, textureData.Length);

            _fontTexture = Texture.Load("ImGui Text Atlas", () => new TextureDataBuffer(
                textureData,
                width, height,
                PixelFormat.Rgba, PixelType.UnsignedByte,
                SizedInternalFormat.Rgba8,
                TextureWrapMode.Repeat, true, false
            ));

            io.Fonts.SetTexID((IntPtr)_fontTexture.GetHandle());
            io.Fonts.ClearTexData();
        }

        private readonly List<char> _pressedChars = new();
        private static readonly Keys[] AllKeys = (Keys[])Enum.GetValues(typeof(Keys));

        public void Update(GameWindow wnd, float deltaTime)
        {
            if (_frameBegun) ImGui.Render();
            _frameBegun = true;

            var io = ImGui.GetIO();
            io.DeltaTime = deltaTime;
            
            var mouseState = wnd.MouseState;
            var keyboardState = wnd.KeyboardState;
            io.MouseDown[0] = mouseState[MouseButton.Left];
            io.MouseDown[1] = mouseState[MouseButton.Right];
            io.MouseDown[2] = mouseState[MouseButton.Middle];

            var screenPoint = new Vector2i((int)mouseState.X, (int)mouseState.Y);
            io.MousePos = new System.Numerics.Vector2(screenPoint.X, screenPoint.Y);

            foreach (var key in AllKeys)
            {
                if (key == Keys.Unknown) continue;
                io.KeysDown[(int)key] = keyboardState.IsKeyDown(key);
            }

            foreach (var c in _pressedChars) io.AddInputCharacter(c);
            _pressedChars.Clear();

            io.KeyCtrl = keyboardState.IsKeyDown(Keys.LeftControl) || keyboardState.IsKeyDown(Keys.RightControl);
            io.KeyAlt = keyboardState.IsKeyDown(Keys.LeftAlt) || keyboardState.IsKeyDown(Keys.RightAlt);
            io.KeyShift = keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift);
            io.KeySuper = keyboardState.IsKeyDown(Keys.LeftSuper) || keyboardState.IsKeyDown(Keys.RightSuper);

            ImGui.NewFrame();
        }

        internal void TextInput(char keyChar)
        {
            _pressedChars.Add(keyChar);
        }

        internal static void MouseScroll(Vector2 offset)
        {
            var io = ImGui.GetIO();
            io.MouseWheel = offset.Y;
            io.MouseWheelH = offset.X;
        }
        
        public void Render()
        {
            if (!_frameBegun) return;
            _frameBegun = false;
            ImGui.Render();
            RenderImDrawData(ImGui.GetDrawData());
        }

        private void RenderImDrawData(ImDrawDataPtr drawData)
        {
            if (drawData.CmdListsCount == 0) return;

            // Get initial state.
            var prevBlendEnabled = Renderer.Blending;
            var prevScissorTestEnabled = Renderer.ScissorTest;
            var prevCullFaceEnabled = Renderer.BackFaceCulling;
            var prevDepthTestEnabled = Renderer.DepthTesting;
            var prevBlendEquationRgb = (BlendEquationMode)GL.GetInteger(GetPName.BlendEquationRgb);
            var prevBlendEquationAlpha = (BlendEquationMode)GL.GetInteger(GetPName.BlendEquationAlpha);
            var prevBlendFuncSrcRgb = (BlendingFactorSrc)GL.GetInteger(GetPName.BlendSrcRgb);
            var prevBlendFuncSrcAlpha = (BlendingFactorSrc)GL.GetInteger(GetPName.BlendSrcAlpha);
            var prevBlendFuncDstRgb = (BlendingFactorDest)GL.GetInteger(GetPName.BlendDstRgb);
            var prevBlendFuncDstAlpha = (BlendingFactorDest)GL.GetInteger(GetPName.BlendDstAlpha);
            var prevScissorBox = new int[4];
            GL.GetInteger(GetPName.ScissorBox, prevScissorBox);

            // Bind the element buffer (through the VAO) so that we can resize it.
            GL.BindVertexArray(_vertexArray);
            // Bind the vertex buffer so that we can resize it.
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vertexBuffer);
            for (var i = 0; i < drawData.CmdListsCount; i++)
            {
                var cmdList = drawData.CmdListsRange[i];
                ResizeBuffer(ref _vertexBufferSize, cmdList.VtxBuffer.Size * Unsafe.SizeOf<ImDrawVert>(),
                    BufferTarget.ArrayBuffer);
                ResizeBuffer(ref _indexBufferSize, cmdList.IdxBuffer.Size * sizeof(ushort),
                    BufferTarget.ElementArrayBuffer);
            }
            
            var io = ImGui.GetIO();
            var mvp = Matrix4.CreateOrthographicOffCenter(
                0.0f,
                io.DisplaySize.X,
                io.DisplaySize.Y,
                0.0f,
                -1.0f,
                1.0f);
            mvp.Transpose();
            _shader.SetUniform("projection_matrix", mvp);
            _shader.Activate();
            
            GL.BindVertexArray(_vertexArray);
            drawData.ScaleClipRects(io.DisplayFramebufferScale);

            Renderer.Blending = true;
            Renderer.ScissorTest = true;
            Renderer.BackFaceCulling = false;
            Renderer.DepthTesting = false;
            GL.BlendEquation(BlendEquationMode.FuncAdd);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            
            for (var n = 0; n < drawData.CmdListsCount; n++)
            {
                var cmdList = drawData.CmdListsRange[n];

                GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero,
                    cmdList.VtxBuffer.Size * Unsafe.SizeOf<ImDrawVert>(), cmdList.VtxBuffer.Data);
                GL.BufferSubData(BufferTarget.ElementArrayBuffer, IntPtr.Zero, cmdList.IdxBuffer.Size * sizeof(ushort),
                    cmdList.IdxBuffer.Data);

                for (var cmdI = 0; cmdI < cmdList.CmdBuffer.Size; cmdI++)
                {
                    var pCmd = cmdList.CmdBuffer[cmdI];
                    if (pCmd.UserCallback != IntPtr.Zero) throw new NotImplementedException();

                    GL.BindTextureUnit(0, (int)pCmd.TextureId);

                    // We do _windowHeight - (int)clip.W instead of (int)clip.Y because gl has flipped Y when it comes to these coordinates
                    var clip = pCmd.ClipRect;
                    GL.Scissor((int)clip.X, _windowHeight - (int)clip.W, (int)(clip.Z - clip.X),
                        (int)(clip.W - clip.Y));

                    if ((io.BackendFlags & ImGuiBackendFlags.RendererHasVtxOffset) != 0)
                    {
                        GL.DrawElementsBaseVertex(PrimitiveType.Triangles, (int)pCmd.ElemCount,
                            DrawElementsType.UnsignedShort, (IntPtr)(pCmd.IdxOffset * sizeof(ushort)),
                            unchecked((int)pCmd.VtxOffset));
                    }
                    else
                    {
                        GL.DrawElements(BeginMode.Triangles, (int)pCmd.ElemCount, DrawElementsType.UnsignedShort,
                            (int)pCmd.IdxOffset * sizeof(ushort));
                    }
                }
            }

            // Reset State
            GL.Scissor(prevScissorBox[0], prevScissorBox[1], prevScissorBox[2], prevScissorBox[3]);
            GL.BlendEquationSeparate(prevBlendEquationRgb, prevBlendEquationAlpha);
            GL.BlendFuncSeparate(prevBlendFuncSrcRgb, prevBlendFuncDstRgb, prevBlendFuncSrcAlpha,
                prevBlendFuncDstAlpha);
            Renderer.Blending = prevBlendEnabled;
            Renderer.DepthTesting = prevDepthTestEnabled;
            Renderer.BackFaceCulling = prevCullFaceEnabled;
            Renderer.ScissorTest = prevScissorTestEnabled;
        }

        private static void ResizeBuffer(ref int currentSize, int minSize, BufferTarget bufferType)
        {
            if (minSize <= currentSize) return;
            currentSize = (int)Math.Max(currentSize * 1.5f, minSize);
            GL.BufferData(bufferType, currentSize, IntPtr.Zero, BufferUsageHint.DynamicDraw);
        }

        public void Dispose()
        {
            GL.DeleteVertexArray(_vertexArray);
            GL.DeleteBuffer(_vertexBuffer);
            GL.DeleteBuffer(_indexBuffer);
        }
    }
}