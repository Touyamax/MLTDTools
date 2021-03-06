﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using JetBrains.Annotations;
using OpenMLTD.MillionDance.Entities.Mltd;
using OpenMLTD.MillionDance.Viewer.Extensions;
using OpenMLTD.MillionDance.Viewer.Internal;
using OpenMLTD.MillionDance.Viewer.ObjectGL;
using OpenTK.Graphics.OpenGL4;
using UnityStudio.Extensions;
using UnityStudio.Models;
using UnityStudio.Serialization;
using UnityStudio.UnityEngine;
using UnityStudio.UnityEngine.Animation;
using UnityStudio.Utilities;

namespace OpenMLTD.MillionDance.Viewer {
    internal static class ResHelper {

        [NotNull]
        public static T LoadProgram<T>([NotNull] string vertSourceFile, [NotNull] string fragSourceFile)
            where T : Program {
            var vertSource = File.ReadAllText(vertSourceFile);
            var fragSource = File.ReadAllText(fragSourceFile);

            var vert = Shader.Compile(vertSource, ShaderType.VertexShader);
            var frag = Shader.Compile(fragSource, ShaderType.FragmentShader);

            var program = Program.Link<T>(vert, frag);

            return program;
        }

        [CanBeNull]
        public static Mesh LoadBodyMesh() {
            Mesh mesh = null;

            using (var fileStream = File.Open(BodyModelFilePath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                using (var bundle = new BundleFile(fileStream, false)) {
                    foreach (var assetFile in bundle.AssetFiles) {
                        foreach (var preloadData in assetFile.PreloadDataList) {
                            if (preloadData.KnownType != KnownClassID.Mesh) {
                                continue;
                            }

                            mesh = preloadData.LoadAsMesh();
                            break;
                        }
                    }
                }
            }

            return mesh;
        }

        [CanBeNull]
        public static Mesh LoadHeadMesh() {
            var meshList = new List<Mesh>();

            using (var fileStream = File.Open(HeadModelFilePath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                using (var bundle = new BundleFile(fileStream, false)) {
                    foreach (var assetFile in bundle.AssetFiles) {
                        foreach (var preloadData in assetFile.PreloadDataList) {
                            if (preloadData.KnownType != KnownClassID.Mesh) {
                                continue;
                            }

                            var mesh = preloadData.LoadAsMesh();

                            meshList.Add(mesh);
                        }
                    }
                }
            }

            var result = CompositeMesh.FromMeshes(meshList);

            return result;
        }

        [NotNull, ItemNotNull]
        public static IReadOnlyList<BoneNode> BuildBoneHierachy([NotNull] Avatar avatar, [NotNull] Mesh mesh) {
            var boneList = new List<BoneNode>();

            for (var i = 0; i < avatar.AvatarSkeleton.Nodes.Length; i++) {
                var n = avatar.AvatarSkeleton.Nodes[i];

                var parent = n.ParentIndex >= 0 ? boneList[n.ParentIndex] : null;
                var boneId = avatar.AvatarSkeleton.NodeIDs[i];
                var bonePath = avatar.BoneNamesMap[boneId];

                var boneIndex = avatar.AvatarSkeleton.NodeIDs.FindIndex(boneId);

                if (boneIndex < 0) {
                    throw new IndexOutOfRangeException();
                }

                var initialPose = avatar.AvatarSkeletonPose.Transforms[boneIndex];

                var bone = new BoneNode(parent, i, bonePath, initialPose.Translation.ToOpenTK(), initialPose.Rotation.ToOpenTK());

                boneList.Add(bone);
            }

            foreach (var bone in boneList) {
                bone.Initialize();
            }

#if DEBUG
            Debug.Print("Model bones:");

            for (var i = 0; i < boneList.Count; i++) {
                var bone = boneList[i];
                Debug.Print("[{0}]: {1}", i, bone.ToString());
            }
#endif

            return boneList;
        }

        [CanBeNull]
        public static Avatar LoadBodyAvatar() {
            Avatar avatar = null;

            using (var fileStream = File.Open(BodyModelFilePath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                using (var bundle = new BundleFile(fileStream, false)) {
                    foreach (var assetFile in bundle.AssetFiles) {
                        foreach (var preloadData in assetFile.PreloadDataList) {
                            if (preloadData.KnownType != KnownClassID.Avatar) {
                                continue;
                            }

                            avatar = preloadData.LoadAsAvatar();
                            break;
                        }
                    }
                }
            }

            return avatar;
        }

        [CanBeNull]
        public static Avatar LoadHeadAvatar() {
            Avatar avatar = null;

            using (var fileStream = File.Open(HeadModelFilePath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                using (var bundle = new BundleFile(fileStream, false)) {
                    foreach (var assetFile in bundle.AssetFiles) {
                        foreach (var preloadData in assetFile.PreloadDataList) {
                            if (preloadData.KnownType != KnownClassID.Avatar) {
                                continue;
                            }

                            avatar = preloadData.LoadAsAvatar();
                            break;
                        }
                    }
                }
            }

            return avatar;
        }

        public static (CharacterImasMotionAsset, CharacterImasMotionAsset, CharacterImasMotionAsset) LoadDance() {
            CharacterImasMotionAsset dan = null, apa = null, apg = null;

            var ser = new MonoBehaviourSerializer();

            using (var fileStream = File.Open(DanceDataFilePath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                using (var bundle = new BundleFile(fileStream, false)) {
                    foreach (var assetFile in bundle.AssetFiles) {
                        foreach (var preloadData in assetFile.PreloadDataList) {
                            if (preloadData.KnownType != KnownClassID.MonoBehaviour) {
                                continue;
                            }

                            var behaviour = preloadData.LoadAsMonoBehaviour(true);

                            switch (behaviour.Name) {
                                case "dan_" + SongResourceName + "_" + DancerPosition + "_dan.imo":
                                    behaviour = preloadData.LoadAsMonoBehaviour(false);
                                    dan = ser.Deserialize<CharacterImasMotionAsset>(behaviour);
                                    break;
                                case "dan_" + SongResourceName + "_" + DancerPosition + "_apa.imo":
                                    behaviour = preloadData.LoadAsMonoBehaviour(false);
                                    apa = ser.Deserialize<CharacterImasMotionAsset>(behaviour);
                                    break;
                                case "dan_" + SongResourceName + "_" + DancerPosition + "_apg.imo":
                                    behaviour = preloadData.LoadAsMonoBehaviour(false);
                                    apg = ser.Deserialize<CharacterImasMotionAsset>(behaviour);
                                    break;
                            }

                            if (dan != null && apa != null && apg != null) {
                                break;
                            }
                        }
                    }
                }
            }

            return (dan, apa, apg);
        }

        private const string BodyModelFilePath = "Resources/cb_ss001_015siz.unity3d";
        private const string HeadModelFilePath = "Resources/ch_ss001_015siz.unity3d";
        private const string SongResourceName = "hzwend";
        private const string DancerPosition = "01";
        private const string DanceDataFilePath = "Resources/dan_" + SongResourceName + "_" + DancerPosition + ".imo.unity3d";

    }
}
