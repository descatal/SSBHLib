﻿using CrossMod.Rendering.ShapeMeshes;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using SFGraphics.Cameras;
using SFGraphics.GLObjects.Shaders;
using SSBHLib.Tools;
using System;
using System.Collections.Generic;
using System.IO;

namespace CrossMod.Rendering
{
    public class RSkeleton : IRenderable
    {
        // Processing
        public List<RBone> Bones = new List<RBone>();
        public List<RHelperBone> HelperBone = new List<RHelperBone>();

        // Rendering
        private PrimBonePrism bonePrism;
        public static Shader boneShader = null;
        private static Matrix4 prismRotation = Matrix4.CreateFromAxisAngle(new Vector3(0, 0, 1), (float)Math.PI / 2f);

        public void Reset()
        {
            foreach (var bone in Bones)
            {
                // TODO: Find a better way to reset the animation.
                bone.AnimationTrackTransform = null;
            }
        }

        public Matrix4[] GetAnimationTransforms()
        {
            Matrix4[] transforms = new Matrix4[Bones.Count];
            for (int i = 0; i < Bones.Count; i++)
            {
                transforms[i] = Bones[i].InvWorldTransform * GetAnimationTransform(Bones[i]);
            }

            return transforms;
        }

        public Matrix4 GetAnimationSingleBindsTransform(int index)
        {
            if (index != -1 && Bones.Count > 0)
                return GetAnimationTransform(Bones[index]);

            return Matrix4.Identity;
        }

        public int GetBoneIndex(string boneName)
        {
            for (int i = 0; i < Bones.Count; i++)
            {
                if (Bones[i].Name.Equals(boneName))
                    return i;
            }
            return -1;
        }

        public void Render(Camera camera)
        {
            // Render skeleton on top.
            GL.Disable(EnableCap.DepthTest);

            if (bonePrism == null)
                bonePrism = new PrimBonePrism();

            if (boneShader == null)
            {
                boneShader = new Shader();
                boneShader.LoadShaders(File.ReadAllText("Shaders/Bone.vert"), File.ReadAllText("Shaders/Bone.frag"));
            }

            boneShader.UseProgram();

            boneShader.SetVector4("boneColor", RenderSettings.Instance.BoneColor);

            Matrix4 mvp = camera.MvpMatrix;
            boneShader.SetMatrix4x4("mvp", ref mvp);
            boneShader.SetMatrix4x4("rotation", ref prismRotation);

            foreach (RBone b in Bones)
            {
                Matrix4 transform = GetAnimationTransform(b);
                boneShader.SetMatrix4x4("bone", ref transform);
                boneShader.SetInt("hasParent", b.ParentId != -1 ? 1 : 0);
                if (b.ParentId != -1)
                {
                    Matrix4 parentTransform = GetAnimationTransform(Bones[b.ParentId]);
                    boneShader.SetMatrix4x4("parent", ref parentTransform);
                }
                bonePrism.Draw(boneShader);

                // leaf node
                boneShader.SetInt("hasParent", 0);
                bonePrism.Draw(boneShader);
            }
        }

        private Matrix4 GetAnimationTransform(RBone b)
        {
            // TODO: How do the scaling types work?
            if (b.AnimationTrackTransform is AnimTrackTransform transform)
            {
                var inheritScale = ShouldInheritScale(transform);
                return AccumulateTransforms(b, inheritScale);
            }
            else
            {
                return AccumulateTransforms(b, true);
            }
        }

        private static bool ShouldInheritScale(AnimTrackTransform transform)
        {
            return transform.ScaleType != 1;
        }

        private Matrix4 AccumulateTransforms(RBone b, bool includeScale)
        {
            if (b.AnimationTrackTransform is AnimTrackTransform transform)
            {
                // Recursively accumulate the parent transform.
                if (b.ParentId == -1)
                {
                    return GetMatrix(transform, includeScale);
                }

                var currentTransform = GetMatrix(transform, includeScale);

                // If any bone in the chain doesn't inherit scale,
                // the scale of all all the subsequent ancestors should be ignored.
                // This is accomplished through an and condition.
                var inheritScale = ShouldInheritScale(transform);
                var parentTransform = AccumulateTransforms(Bones[b.ParentId], includeScale && inheritScale);

                return currentTransform * parentTransform;
            }
            else
            {
                // If the animation is reset, just accumulate the skeletal transforms instead.
                // TODO: Find a less convoluted way of resetting an animation.
                if (b.ParentId == -1)
                {
                    return b.Transform;
                }
                else
                {
                    return b.Transform * GetAnimationTransform(Bones[b.ParentId]);
                }
            }
        }

        private static Matrix4 GetMatrix(AnimTrackTransform transform, bool includeScale)
        {
            var matrix = Matrix4.CreateFromQuaternion(new Quaternion(transform.Rx, transform.Ry, transform.Rz, transform.Rw)) *
                    Matrix4.CreateTranslation(transform.X, transform.Y, transform.Z);

            var scale = new Vector3(transform.Sx, transform.Sy, transform.Sz);
            if (transform.ScaleType == 3)
                scale = new Vector3(transform.CompensateScale);

            if (includeScale)
                return Matrix4.CreateScale(scale) * matrix;


            return matrix;

        }
    }

    public class RBone
    {
        public string Name { get; set; }
        public int Id { get; set; }
        public int ParentId { get; set; }

        public Matrix4 Transform
        {
            get
            {
                return transform;
            }
            set
            {
                transform = value;
                AnimationTransform = value;
            }
        }
        private Matrix4 transform;

        public Matrix4 InvTransform { get; set; }
        public Matrix4 WorldTransform { get; set; }
        public Matrix4 InvWorldTransform { get; set; }

        // TODO: Rebuild this using the anim values (translate, scale, etc) instead of matrices?
        // This should make it easier to apply scale compensation without affecting rotations.
        public Matrix4 AnimationTransform { get; set; }
        public AnimTrackTransform? AnimationTrackTransform { get; set; }
    }

    public class RHelperBone
    {
        public string WatcherBone { get; set; }
        public string ParentBone { get; set; }
        public string HelperBoneName { get; set; }

        public Vector3 Aoi { get; set; }
        public Quaternion WatchRotation { get; set; }
        public Quaternion HelperTargetRotation { get; set; }
        public Vector3 MinRange { get; set; }
        public Vector3 MaxRange { get; set; }
    }
}
