using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using recreate_nrw.Render;

namespace recreate_nrw.Util
{
    public sealed class ImGuiController : IDisposable
    {
        private int _vertexArray;
        private int _vertexBuffer;
        private int _vertexBufferSize;
        private int _indexBuffer;
        private int _indexBufferSize;

        private Texture _fontTexture = null!;
        private Shader _shader = null!;

        private Vector2i _windowSize;
        private readonly Vector2 _scaleFactor = Vector2.One;

        private Vector2i WindowSize
        {
            get => _windowSize;
            set
            {
                _windowSize = value;

                var io = ImGui.GetIO();
                io.DisplaySize = (value.ToVector2() / _scaleFactor).ToSystem();
                io.DisplayFramebufferScale = _scaleFactor.ToSystem();
            }
        }

        public ImGuiController(Vector2i windowSize, Profiler profiler)
        {
            var configure = profiler.Start("Configure");
            var context = ImGui.CreateContext();
            ImGui.SetCurrentContext(context);

            WindowSize = windowSize;

            var io = ImGui.GetIO();
            io.Fonts.AddFontDefault();
            io.BackendFlags |= ImGuiBackendFlags.HasSetMousePos;
            io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;
            io.ConfigFlags |= ImGuiConfigFlags.DockingEnable;
            configure.Stop();
            var createDeviceResources = profiler.Start("Creating Device Resources");
            CreateDeviceResources();
            createDeviceResources.Stop();
            var createFontTexture = profiler.Start("Create Font Texture");
            CreateFontTexture(createFontTexture);
            createFontTexture.Stop();
            Resources.RegisterDisposable(this);
        }

        private void CreateDeviceResources()
        {
            var prevVao = GL.GetInteger(GetPName.VertexArrayBinding);
            var prevArrayBuffer = GL.GetInteger(GetPName.ArrayBufferBinding);

            _vertexArray = GL.GenVertexArray();
            GL.BindVertexArray(_vertexArray);
            LabelObject(ObjectLabelIdentifier.VertexArray, _vertexArray, "VAO: ImGui");

            _vertexBuffer = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vertexBuffer);
            LabelObject(ObjectLabelIdentifier.Buffer, _vertexBuffer, "VBO: ImGui");

            _indexBuffer = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _indexBuffer);
            LabelObject(ObjectLabelIdentifier.Buffer, _indexBuffer, "EBO: ImGui");

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

        private void CreateFontTexture(Profiler profiler)
        {
            var io = ImGui.GetIO();
            
            var getCopy = profiler.Start("Get, Copy");
            io.Fonts.GetTexDataAsRGBA32(out IntPtr pixels, out var width, out var height, out var sizeOfPixel);
            var textureData = new byte[width * height * sizeOfPixel];
            Marshal.Copy(pixels, textureData, 0, textureData.Length);
            getCopy.Stop();

            var create = profiler.Start("Create");
            _fontTexture = Texture.Load("ImGui Text Atlas", () => (new TextureInfo2D(
                SizedInternalFormat.Rgba8,
                new Vector2i(width, height),
                TextureWrapMode.Repeat, true, false
            ), new TextureData2D(
                textureData,
                PixelFormat.Rgba,
                PixelType.UnsignedByte
            )));
            create.Stop();

            var registerDelete = profiler.Start("Register, Delete");
            io.Fonts.SetTexID((IntPtr)_fontTexture.GetHandle());
            io.Fonts.ClearTexData();
            registerDelete.Stop();
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

        public void RenderFrame(GameWindow wnd, float deltaTime, Action render)
        {
            var io = ImGui.GetIO();
            io.DeltaTime = deltaTime;

            if (io.WantSetMousePos)
                wnd.MousePosition = io.MousePos.ToVector2();

            ImGui.NewFrame();

            ImGui.PushStyleColor(ImGuiCol.WindowBg, new System.Numerics.Vector4(0f, 0f, 0f, 0.2f));
            ImGui.DockSpaceOverViewport(0, ImGui.GetMainViewport(),
                ImGuiDockNodeFlags.PassthruCentralNode | ImGuiDockNodeFlags.NoDockingOverCentralNode);
            ImGui.PopStyleColor();

            render();

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

            GL.BindVertexArray(_vertexArray);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vertexBuffer);

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
            GL.BlendFuncSeparate(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha, BlendingFactorSrc.One,
                BlendingFactorDest.OneMinusSrcAlpha);

            for (var n = 0; n < drawData.CmdListsCount; n++)
            {
                var drawList = drawData.CmdLists[n];

                var vtxBufferSize = drawList.VtxBuffer.Size * Unsafe.SizeOf<ImDrawVert>();
                var idxBufferSize = drawList.IdxBuffer.Size * sizeof(ushort);

                if (_vertexBufferSize < vtxBufferSize)
                {
                    _vertexBufferSize = vtxBufferSize;
                    GL.BufferData(BufferTarget.ArrayBuffer, _vertexBufferSize, IntPtr.Zero,
                        BufferUsageHint.StreamDraw);
                }

                if (_indexBufferSize < idxBufferSize)
                {
                    _indexBufferSize = idxBufferSize;
                    GL.BufferData(BufferTarget.ElementArrayBuffer, _indexBufferSize, IntPtr.Zero,
                        BufferUsageHint.StreamDraw);
                }

                GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, vtxBufferSize, drawList.VtxBuffer.Data);
                GL.BufferSubData(BufferTarget.ElementArrayBuffer, IntPtr.Zero, idxBufferSize, drawList.IdxBuffer.Data);

                for (var cmdI = 0; cmdI < drawList.CmdBuffer.Size; cmdI++)
                {
                    var pCmd = drawList.CmdBuffer[cmdI];
                    if (pCmd.UserCallback != IntPtr.Zero) throw new NotImplementedException();
                    GL.BindTextureUnit(0, (int)pCmd.TextureId);

                    // We do _windowHeight - (int)clip.W instead of (int)clip.Y because gl has flipped Y when it comes to these coordinates
                    var clip = pCmd.ClipRect;
                    GL.Scissor((int)clip.X, WindowSize.Y - (int)clip.W, (int)(clip.Z - clip.X),
                        (int)(clip.W - clip.Y));

                    GL.DrawElementsBaseVertex(PrimitiveType.Triangles, (int)pCmd.ElemCount,
                        DrawElementsType.UnsignedShort, (IntPtr)(pCmd.IdxOffset * sizeof(ushort)), (int)pCmd.VtxOffset);
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

        public void Dispose()
        {
            GL.DeleteVertexArray(_vertexArray);
            GL.DeleteBuffer(_vertexBuffer);
            GL.DeleteBuffer(_indexBuffer);
        }

        private static void KeyEvent(Keys key, bool down)
        {
            var io = ImGui.GetIO();
            if (!TryMapKey(key, out var mappedKey, out var modifier)) return;
            io.AddKeyEvent(mappedKey, down);
            if (modifier.HasValue) io.AddKeyEvent(modifier.Value, down);
        }

        private static bool TryMapKey(Keys key, out ImGuiKey mapped, out ImGuiKey? modifier)
        {
            ImGuiKey? newModifier = null;
            mapped = key switch
            {
                Keys.Tab => ImGuiKey.Tab,
                Keys.Left => ImGuiKey.LeftArrow,
                Keys.Right => ImGuiKey.RightArrow,
                Keys.Up => ImGuiKey.UpArrow,
                Keys.Down => ImGuiKey.DownArrow,
                >= Keys.PageUp and <= Keys.End => MapKeyRange(Keys.PageUp, ImGuiKey.PageUp),
                Keys.Insert => ImGuiKey.Insert,
                Keys.Delete => ImGuiKey.Delete,
                Keys.Backspace => ImGuiKey.Backspace,
                Keys.Space => ImGuiKey.Space,
                Keys.Enter => ImGuiKey.Enter,
                Keys.Escape => ImGuiKey.Escape,
                Keys.LeftControl => Modifier(ImGuiKey.LeftCtrl, ImGuiKey.ModCtrl),
                Keys.LeftShift => Modifier(ImGuiKey.LeftShift, ImGuiKey.ModShift),
                Keys.LeftAlt => Modifier(ImGuiKey.LeftAlt, ImGuiKey.ModAlt),
                Keys.LeftSuper => Modifier(ImGuiKey.LeftSuper, ImGuiKey.ModSuper),
                Keys.RightControl => Modifier(ImGuiKey.RightCtrl, ImGuiKey.ModCtrl),
                Keys.RightShift => Modifier(ImGuiKey.RightShift, ImGuiKey.ModShift),
                Keys.RightAlt => Modifier(ImGuiKey.RightAlt, ImGuiKey.ModAlt),
                Keys.RightSuper => Modifier(ImGuiKey.RightSuper, ImGuiKey.ModSuper),
                Keys.Menu => ImGuiKey.Menu,
                >= Keys.D0 and <= Keys.D9 => MapKeyRange(Keys.D0, ImGuiKey._0),
                >= Keys.A and <= Keys.Z => MapKeyRange(Keys.A, ImGuiKey.A),
                >= Keys.F1 and <= Keys.F24 => MapKeyRange(Keys.F1, ImGuiKey.F1),
                Keys.Apostrophe => ImGuiKey.Apostrophe,
                Keys.Comma => ImGuiKey.Comma,
                Keys.Minus => ImGuiKey.Minus,
                Keys.Period => ImGuiKey.Period,
                Keys.Slash => ImGuiKey.Slash,
                Keys.Semicolon => ImGuiKey.Semicolon,
                Keys.Equal => ImGuiKey.Equal,
                Keys.LeftBracket => ImGuiKey.LeftBracket,
                Keys.Backslash => ImGuiKey.Backslash,
                Keys.RightBracket => ImGuiKey.RightBracket,
                Keys.GraveAccent => ImGuiKey.GraveAccent,
                Keys.CapsLock => ImGuiKey.CapsLock,
                Keys.ScrollLock => ImGuiKey.ScrollLock,
                Keys.NumLock => ImGuiKey.NumLock,
                Keys.PrintScreen => ImGuiKey.PrintScreen,
                Keys.Pause => ImGuiKey.Pause,
                >= Keys.KeyPad0 and <= Keys.KeyPadEqual => MapKeyRange(Keys.KeyPad0, ImGuiKey.Keypad0),
                _ => ImGuiKey.None
            };
            modifier = newModifier;
            return mapped != ImGuiKey.None;

            ImGuiKey MapKeyRange(Keys startKey, ImGuiKey startImGuiKey) => startImGuiKey + (key - startKey);

            ImGuiKey Modifier(ImGuiKey mapped, ImGuiKey mappedModifier)
            {
                newModifier = mappedModifier;
                return mapped;
            }
        }

        public void OnResize(ResizeEventArgs e)
        {
            WindowSize = e.Size;
        }

        public static void OnFocusedChanged(FocusedChangedEventArgs e)
        {
            var io = ImGui.GetIO();
            io.AddFocusEvent(e.IsFocused);
        }

        public static void OnKeyDown(KeyboardKeyEventArgs e)
        {
            if (e.IsRepeat) return;
            KeyEvent(e.Key, true);
        }

        public static void OnTextInput(TextInputEventArgs e)
        {
            var io = ImGui.GetIO();
            io.AddInputCharacter((uint)e.Unicode);
        }

        public static void OnKeyUp(KeyboardKeyEventArgs e)
        {
            if (e.IsRepeat) return;
            KeyEvent(e.Key, false);
        }

        public static void OnMouseDown(MouseButtonEventArgs e)
        {
            var io = ImGui.GetIO();
            io.AddMouseButtonEvent((int)e.Button, true);
        }

        public static void OnMouseUp(MouseButtonEventArgs e)
        {
            var io = ImGui.GetIO();
            io.AddMouseButtonEvent((int)e.Button, false);
        }

        public static void OnMouseMove(MouseMoveEventArgs e)
        {
            var io = ImGui.GetIO();
            io.AddMousePosEvent(e.X, e.Y);
        }

        public static void OnMouseWheel(MouseWheelEventArgs e)
        {
            var io = ImGui.GetIO();
            io.AddMouseWheelEvent(e.OffsetX, e.OffsetY);
        }
    }
}