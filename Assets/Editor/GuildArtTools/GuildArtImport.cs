using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace ChainKnights.GuildArtTools
{
    /// <summary>Configure the generated art without changing gameplay or scene layouts.</summary>
    public static class GuildArtImport
    {
        [Serializable] private class Manifest { public Entry[] assets; }
        [Serializable] private class Entry
        {
            public string folder, name, kind;
            public int width, height, border;
        }
        [Serializable] private class Report
        {
            public int textures, sprites, audioClips, walkClips;
            public string[] errors;
        }
        private const string ArtRoot = "Assets/Resources/Art";
        private const string AudioRoot = "Assets/Resources/Audio";

        [MenuItem("Chain Knights/Art/Configure and verify guild assets")]
        public static void Configure()
        {
            var errors = new List<string>();
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText("Docs/Art/guild_assets_manifest.json"));
            var entries = manifest.assets.ToDictionary(x => x.folder + "/" + x.name);
            var report = new Report();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var path in Directory.GetFiles(ArtRoot, "*.png", SearchOption.AllDirectories))
            {
                string assetPath = path.Replace('\\', '/');
                string key = assetPath.Substring(ArtRoot.Length + 1);
                key = key.Substring(0, key.Length - 4);
                entries.TryGetValue(key, out var entry);
                var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
                if (importer == null) { errors.Add("Missing importer: " + assetPath); continue; }
                bool walk = entry != null && entry.kind == "animation";
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = walk ? SpriteImportMode.Multiple : SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.sRGBTexture = true;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 4096;
                importer.spritePixelsPerUnit = 100;
                int border = entry != null ? entry.border : 0;
                importer.spriteBorder = new Vector4(border, border, border, border);
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spriteGenerateFallbackPhysicsShape = false;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
                if (walk) SliceWalk(importer, entry.name);
                var sprites = AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<Sprite>().ToArray();
                int expected = walk ? 4 : 1;
                if (sprites.Length != expected) errors.Add(assetPath + ": expected " + expected + " sprites; got " + sprites.Length);
                if (entry != null)
                {
                    importer.GetSourceTextureWidthAndHeight(out int width, out int height);
                    if (width != entry.width || height != entry.height) errors.Add(assetPath + ": incorrect dimensions");
                }
                if (walk && sprites.Length == 4) report.walkClips += MakeWalkClips(entry.name, sprites);
                report.textures++;
                report.sprites += sprites.Length;
            }
            if (Directory.Exists(AudioRoot))
                foreach (var path in Directory.GetFiles(AudioRoot).Where(x => x.EndsWith(".wav") || x.EndsWith(".ogg")))
                {
                    var assetPath = path.Replace('\\', '/');
                    var importer = (AudioImporter)AssetImporter.GetAtPath(assetPath);
                    if (importer == null) { errors.Add("Missing audio importer: " + assetPath); continue; }
                    var settings = importer.defaultSampleSettings;
                    settings.loadType = assetPath.EndsWith(".ogg") ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
                    settings.compressionFormat = assetPath.EndsWith(".ogg") ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.PCM;
                    settings.quality = 0.8f;
                    settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                    importer.defaultSampleSettings = settings;
                    importer.forceToMono = assetPath.EndsWith(".wav");
                    importer.SaveAndReimport();
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
                    if (clip == null || clip.length <= 0) errors.Add("Invalid audio: " + assetPath);
                    else report.audioClips++;
                }
            report.errors = errors.ToArray();
            Directory.CreateDirectory("Docs/Art");
            File.WriteAllText("Docs/Art/guild_unity_import_report.json", JsonUtility.ToJson(report, true));
            AssetDatabase.SaveAssets();
            foreach (var error in errors) Debug.LogError(error);
            Debug.Log("Guild art audit: " + JsonUtility.ToJson(report));
        }

        private static void SliceWalk(TextureImporter importer, string name)
        {
            var factories = new SpriteDataProviderFactories();
            factories.Init();
            var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            var existing = provider.GetSpriteRects();
            var rects = Enumerable.Range(0, 4).Select(i => new SpriteRect
            {
                name = name + "_" + i,
                rect = new Rect(i * 128, 0, 128, 128),
                alignment = SpriteAlignment.BottomCenter,
                pivot = new Vector2(0.5f, 0),
                spriteID = existing.FirstOrDefault(x => x.name == name + "_" + i)?.spriteID ?? GUID.Generate()
            }).ToArray();
            provider.SetSpriteRects(rects);
            var names = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            names.SetNameFileIdPairs(rects.Select(x => new SpriteNameFileIdPair(x.name, x.spriteID)));
            provider.Apply();
            importer.SaveAndReimport();
        }

        private static int MakeWalkClips(string name, Sprite[] sprites)
        {
            string folder = ArtRoot + "/Arthurs/Animations";
            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            sprites = sprites.OrderBy(x => x.name).ToArray();
            foreach (bool left in new[] { false, true })
            {
                string path = folder + "/" + name + (left ? "_left" : "_right") + ".anim";
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                bool create = clip == null;
                if (create) clip = new AnimationClip();
                clip.frameRate = 8;
                var keys = Enumerable.Range(0, 5).Select(i => new ObjectReferenceKeyframe { time = i / 8f, value = sprites[i % 4] }).ToArray();
                AnimationUtility.SetObjectReferenceCurve(clip, new EditorCurveBinding { path = "", type = typeof(SpriteRenderer), propertyName = "m_Sprite" }, keys);
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(SpriteRenderer), "m_FlipX"),
                    AnimationCurve.Constant(0, 0.5f, left ? 1 : 0));
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = true;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                if (create) AssetDatabase.CreateAsset(clip, path);
                else EditorUtility.SetDirty(clip);
            }
            return 2;
        }

        public static void ConfigureAndExit()
        {
            try
            {
                Configure();
                var report = JsonUtility.FromJson<Report>(File.ReadAllText("Docs/Art/guild_unity_import_report.json"));
                EditorApplication.Exit(report.errors.Length == 0 ? 0 : 1);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }
    }
}

