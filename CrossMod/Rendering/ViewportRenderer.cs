﻿using CrossMod.Nodes;
using CrossMod.Rendering.GlTools;
using CrossMod.Rendering.Resources;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using OpenTK.Input;
using SFGraphics.Cameras;
using SFGraphics.Controls;
using SFGraphics.GLObjects.Framebuffers;
using SFGraphics.GLObjects.GLObjectManagement;
using SFGraphics.GLObjects.Textures;
using System;

namespace CrossMod.Rendering
{
    public class ViewportRenderer
    {
        // TODO: Handle input somewhere else.
        // Previous mouse state.
        private Vector2 mousePosition;
        private float mouseScrollWheel;

        private Framebuffer colorHdrFbo;
        private Framebuffer colorBrightHdrFbo0;

        public IRenderable ItemToRender
        {
            get => itemToRender;
            set
            {
                itemToRender = value;
                FrameRenderableModels();
            }
        }
        private IRenderable itemToRender;
     
        public IRenderableAnimation RenderableAnimation { get; set; }

        public ScriptNode ScriptNode { get; set; }

        public bool IsPlayingAnimation { get; set; }

        private readonly GLViewport glViewport;

        public Camera Camera { get; } = new Camera() { FarClipPlane = 500000 };

        public void UpdateMouseScroll() => mouseScrollWheel = Mouse.GetState().WheelPrecise;

        public ViewportRenderer(GLViewport viewport)
        {
            glViewport = viewport;
        }

        public void SwapBuffers() => glViewport.SwapBuffers();

        public void PauseRendering() => glViewport.PauseRendering();

        public void RestartRendering() => glViewport.RestartRendering();

        public bool IsRendering => glViewport.IsRendering;

        public void ClearRenderableNodes()
        {
            SwitchContextToCurrentThreadAndPerformAction(() =>
            {
                ItemToRender = null;
                GC.WaitForPendingFinalizers();
                GLObjectManager.DeleteUnusedGLObjects();
            });
        }

        public void FrameRenderableModels()
        {
            if (itemToRender is IRenderableModel model && model.RenderModel != null)
                Camera.FrameBoundingSphere(model.RenderModel.BoundingSphere, 0);
        }

        public void UpdateCameraFromMouse()
        {
            var mouseState = Mouse.GetState();

            Vector2 newMousePosition = new Vector2(mouseState.X, mouseState.Y);
            float newMouseScrollWheel = mouseState.WheelPrecise;

            if (mouseState.IsButtonDown(MouseButton.Left))
            {
                Camera.RotationXRadians += (newMousePosition.Y - mousePosition.Y) / 100f;
                Camera.RotationYRadians += (newMousePosition.X - mousePosition.X) / 100f;
            }
            if (mouseState.IsButtonDown(MouseButton.Right))
            {
                Camera.Pan(newMousePosition.X - mousePosition.X, newMousePosition.Y - mousePosition.Y);
            }

            Camera.Zoom((newMouseScrollWheel - mouseScrollWheel) * 0.1f);

            mousePosition = newMousePosition;
            mouseScrollWheel = newMouseScrollWheel;
        }

        public void ReloadShaders()
        {
            SwitchContextToCurrentThreadAndPerformAction(() =>
            {
                ShaderContainer.ReloadShaders();
            });
        }

        public void RenderNodes(float currentFrame = 0)
        {
            // TODO: Resize framebuffers.
            if (colorHdrFbo == null)
                colorHdrFbo = new Framebuffer(FramebufferTarget.Framebuffer, glViewport.Width, glViewport.Height, PixelInternalFormat.Rgba16f, 2);
            if (colorBrightHdrFbo0 == null)
                colorBrightHdrFbo0 = new Framebuffer(FramebufferTarget.Framebuffer, glViewport.Width / 4, glViewport.Height / 4, PixelInternalFormat.Rgba16f);

            SetUpViewport();

            // TODO: FBOs aren't working on Intel Integrated currently.
            bool usePostProcessing = false;

            // WIP Bloom.
            // TODO: The color passes fbos could be organized better.
            if (usePostProcessing)
                colorHdrFbo.Bind();

            // TODO: Handle gamma correction automatically.
            // TODO: Add background color to render settings.
            GL.Disable(EnableCap.DepthTest);
            //var trainingBackgroundGammaCorrected = (float)Math.Pow(0.9333, 2.2);
            ScreenDrawing.DrawGradient(new Vector3(0.25f), new Vector3(0.25f));

            SetRenderState();
            DrawItemToRender(currentFrame);

            if (usePostProcessing)
            {
                // TODO: This should be included in texture/screen drawing.
                GL.Disable(EnableCap.DepthTest);

                // Render the brighter portions into a smaller buffer.
                // TODO: Investigate if Ultimate does any blurring.
                colorBrightHdrFbo0.Bind();
                GL.Viewport(0, 0, colorBrightHdrFbo0.Width, colorBrightHdrFbo0.Height);
                ScreenDrawing.DrawTexture(colorHdrFbo.Attachments[1] as Texture2D);

                // TODO: Why does this required so many casts?
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
                GL.Viewport(0, 0, glViewport.Width, glViewport.Height);
                ScreenDrawing.DrawBloomCombined(colorHdrFbo.Attachments[0] as Texture2D, colorHdrFbo.Attachments[1] as Texture2D);
            }

            ParamNodeContainer.Render(Camera);
            ScriptNode?.Render(Camera);
        }

        private void DrawItemToRender(float currentFrame)
        {
            if (itemToRender is IRenderableModel model)
            {
                RenderableAnimation?.SetFrameModel(model.RenderModel, currentFrame);
                RenderableAnimation?.SetFrameSkeleton(model.Skeleton, currentFrame);
            }
            itemToRender?.Render(Camera);
        }

        public System.Drawing.Bitmap GetScreenshot()
        {
            // Make sure the context is current on this thread.
            var wasRendering = glViewport.IsRendering;
            glViewport.PauseRendering();

            var bmp = Framebuffer.ReadDefaultFramebufferImagePixels(glViewport.Width, glViewport.Height, true);

            if (wasRendering)
                glViewport.RestartRendering();

            return bmp;
        }

        public void SwitchContextToCurrentThreadAndPerformAction(Action action)
        {
            // Make sure the context is current on this thread.
            var wasRendering = glViewport.IsRendering;
            glViewport.PauseRendering();

            action();

            if (wasRendering)
                glViewport.RestartRendering();
        }

        private void SetUpViewport()
        {
            ClearBuffers();
        }

        private void ClearBuffers()
        {
            GL.ClearColor(0, 0, 0, 0);

            colorHdrFbo.Bind();
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            colorBrightHdrFbo0.Bind();
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        }

        private static void SetRenderState()
        {
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Lequal);

            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            GL.Enable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);
        }
    }
}
