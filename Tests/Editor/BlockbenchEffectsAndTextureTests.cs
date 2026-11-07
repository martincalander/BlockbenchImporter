using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MartinCalander.BlockbenchImporter.Builders;
using MartinCalander.BlockbenchImporter.Model;
using MartinCalander.BlockbenchImporter.Parsing;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MartinCalander.BlockbenchImporter.Tests
{
    public sealed class BlockbenchEffectsAndTextureTests
    {
        private readonly List<Object> objects = new List<Object>();
        private readonly List<MaterialBuilder> builders = new List<MaterialBuilder>();

        [TearDown]
        public void TearDown()
        {
            foreach (MaterialBuilder builder in builders)
            {
                foreach (Material material in builder.GetAllMaterials())
                    Object.DestroyImmediate(material);
                foreach (Texture2D texture in builder.GetAllTextures())
                    Object.DestroyImmediate(texture);
            }
            builders.Clear();
            foreach (Object value in objects)
                if (value != null)
                    Object.DestroyImmediate(value);
            objects.Clear();
        }

        [Test]
        public void EffectKeyframesBecomeSortedAnimationEventsWithSharedEffectRecords()
        {
            var effects = new BbAnimator { name = "Effects", type = "effect" };
            effects.keyframes.Add(Effect("sound", 0.5f, new BbDataPoint { effect = "mob.step", file = "sounds/step.ogg" }));
            effects.keyframes.Add(Effect("particle", 1f, new BbDataPoint { effect = "smoke", locator = "head", script = "v.size = 1;" }));
            effects.keyframes.Add(Effect("timeline", 0.25f, new BbDataPoint { script = "v.step = 1;" }));
            effects.keyframes.Add(Effect("sound", 0.75f, new BbDataPoint { effect = "mob.step", file = "sounds/step.ogg" }));
            effects.keyframes.Add(Effect("sound", 0.8f, new BbDataPoint()));
            var animation = new BbAnimation { name = "Walk", length = 0.5f };
            animation.animators["effects"] = effects;
            var model = new BbModel();
            model.animations.Add(animation);
            var builder = CreateAnimationBuilder(model);

            AnimationClip clip = Keep(builder.BuildAnimationClips().Single());

            AnimationEvent[] events = AnimationUtility.GetAnimationEvents(clip);
            Assert.That(events.Select(value => value.time), Is.EqualTo(new[] { 0.25f, 0.5f, 0.75f, 1f }));
            Assert.That(events.Select(value => value.functionName), Is.All.EqualTo("OnBlockbenchEffect"));
            Assert.That(events[1].intParameter, Is.EqualTo(events[2].intParameter), "Repeated effects share one record.");
            Assert.That(builder.EffectEvents, Has.Count.EqualTo(3));
            BlockbenchAnimationEvents.Effect particle = builder.EffectEvents[events[3].intParameter];
            Assert.That(particle.Type, Is.EqualTo(BlockbenchAnimationEvents.EffectType.Particle));
            Assert.That(particle.Name, Is.EqualTo("smoke"));
            Assert.That(particle.Locator, Is.EqualTo("head"));
            Assert.That(particle.Script, Is.EqualTo("v.size = 1;"));
            BlockbenchAnimationEvents.Effect instruction = builder.EffectEvents[events[0].intParameter];
            Assert.That(instruction.Type, Is.EqualTo(BlockbenchAnimationEvents.EffectType.Instruction));
            Assert.That(instruction.Script, Is.EqualTo("v.step = 1;"));
            Assert.That(builder.EffectEvents[events[1].intParameter].File, Is.EqualTo("sounds/step.ogg"));
            Assert.That(AnimationUtility.GetAnimationClipSettings(clip).stopTime, Is.EqualTo(1f),
                "Clips extend to their last effect keyframe.");
        }

        [Test]
        public void DisabledEffectEventsSkipEffectOnlyAnimations()
        {
            var effects = new BbAnimator { type = "effect" };
            effects.keyframes.Add(Effect("sound", 0.5f, new BbDataPoint { effect = "mob.step" }));
            var animation = new BbAnimation { name = "EffectsOnly_" + Guid.NewGuid().ToString("N") };
            animation.animators["effects"] = effects;
            var model = new BbModel();
            model.animations.Add(animation);
            var builder = new AnimationBuilder(model, new Dictionary<string, string>(), new Dictionary<string, Transform>(),
                new BlockbenchImporterSettings { importEffectEvents = false });

            Assert.That(builder.BuildAnimationClips(), Is.Empty);
            Assert.That(builder.EffectEvents, Is.Empty);
        }

        [Test]
        public void EffectReceiverRaisesTheIndexedEffectAndIgnoresUnknownIndices()
        {
            var host = Keep(new GameObject("Effects"));
            var receiver = host.AddComponent<BlockbenchAnimationEvents>();
            var sound = new BlockbenchAnimationEvents.Effect(BlockbenchAnimationEvents.EffectType.Sound, "mob.step", null, null, "step.ogg");
            receiver.SetEffects(new[] { sound });
            var received = new List<BlockbenchAnimationEvents.Effect>();
            receiver.EffectTriggered += received.Add;

            receiver.OnBlockbenchEffect(0);
            receiver.OnBlockbenchEffect(1);
            receiver.OnBlockbenchEffect(-1);

            Assert.That(received, Has.Count.EqualTo(1));
            Assert.That(received[0].Name, Is.EqualTo("mob.step"));
            Assert.That(received[0].Locator, Is.Empty);
            Assert.That(receiver.Effects.Count, Is.EqualTo(1));
        }

        [Test]
        public void ParserReadsEffectsTextureGroupsAndFlipbookFields()
        {
            BbModel model = BbModelParser.Parse(@"{
              ""meta"":{""format_version"":""5.0"",""model_format"":""free""},
              ""texture_groups"":[{""uuid"":""group"",""name"":""Metal"",""is_material"":true,
                ""material_config"":{""color_value"":[10,20,30,255],""mer_value"":[255,0,128]}}],
              ""textures"":[{""name"":""strip"",""group"":""group"",""pbr_channel"":""mer"",""fps"":12,
                ""frame_order_type"":""custom"",""frame_order"":""0 2 1"",""frame_interpolate"":true}],
              ""animations"":[{""name"":""Idle"",""animators"":{""effects"":{""name"":""Effects"",""type"":""effect"",
                ""keyframes"":[{""channel"":""sound"",""time"":0.5,""data_points"":[{""effect"":""mob.idle"",""file"":""""}]}]}}}]
            }");

            Assert.That(model.warnings, Has.None.Contains("tracks"), "Effect tracks are supported, not unsupported data.");
            BbTextureGroup group = model.texture_groups.Single();
            Assert.That(group.is_material, Is.True);
            Assert.That(group.mer_value, Is.EqualTo(new[] { 255f, 0f, 128f }));
            BbTexture texture = model.textures.Single();
            Assert.That(texture.group, Is.EqualTo("group"));
            Assert.That(texture.pbr_channel, Is.EqualTo("mer"));
            Assert.That(texture.fps, Is.EqualTo(12f));
            Assert.That(texture.frame_order_type, Is.EqualTo("custom"));
            Assert.That(texture.frame_order, Is.EqualTo("0 2 1"));
            Assert.That(texture.frame_interpolate, Is.True);
            Assert.That(model.animations[0].animators["effects"].keyframes[0].data_points[0].effect, Is.EqualTo("mob.idle"));
        }

        [TestCase("loop", false, new[] { 0, 1, 2, 3 })]
        [TestCase("backwards", false, new[] { 3, 2, 1, 0 })]
        [TestCase("back_and_forth", false, new[] { 0, 1, 2, 3, 2, 1 })]
        [TestCase("custom", false, new[] { 0, 2, 1, 3 })]
        [TestCase("custom", true, new[] { 0, 2, 1, 3 })]
        public void FlipbooksFollowBlockbenchFrameCountOrderAndTiming(string order, bool minecraft, int[] frames)
        {
            var texture = new BbTexture
            {
                uv_width = 16,
                uv_height = 16,
                fps = 8f,
                frame_time = 2,
                frame_order_type = order,
                frame_order = "0 2 1:5 7 x"
            };

            Assert.That(MaterialBuilder.TryCreateFlipbook(texture, 16, 64, minecraft, out var flipbook), Is.True);

            Assert.That(flipbook.frameCount, Is.EqualTo(4));
            Assert.That(flipbook.frames, Is.EqualTo(frames));
            float step = minecraft ? 0.1f : 0.125f;
            float[] expected = frames.Select(_ => step).ToArray();
            if (order == "custom" && minecraft)
                expected[2] = 0.25f;
            Assert.That(flipbook.durations, Is.EqualTo(expected).Within(0.0001f));
        }

        [TestCase(16, 16)]
        [TestCase(32, 16)]
        public void TexturesMatchingTheirUvShapeAreNotFlipbooks(int width, int height)
        {
            var texture = new BbTexture { uv_width = width, uv_height = height };
            Assert.That(MaterialBuilder.TryCreateFlipbook(texture, width * 2, height * 2, false, out _), Is.False);
        }

        [Test]
        public void FlipbookPlaybackWrapsAndShowsFramesFromTheTopOfTheStrip()
        {
            var flipbook = new BlockbenchTextureAnimation.Flipbook
            {
                frameCount = 4,
                frames = new[] { 0, 1, 3 },
                durations = new[] { 0.1f, 0.2f, 0.1f }
            };

            Assert.That(BlockbenchTextureAnimation.FrameAt(flipbook, 0f), Is.EqualTo(0));
            Assert.That(BlockbenchTextureAnimation.FrameAt(flipbook, 0.15f), Is.EqualTo(1));
            Assert.That(BlockbenchTextureAnimation.FrameAt(flipbook, 0.35f), Is.EqualTo(3));
            Assert.That(BlockbenchTextureAnimation.FrameAt(flipbook, 0.45f), Is.EqualTo(0), "Playback loops.");
            Assert.That(BlockbenchTextureAnimation.ScaleOffset(0, 4), Is.EqualTo(new Vector4(1f, 0.25f, 0f, 0.75f)));
            Assert.That(BlockbenchTextureAnimation.ScaleOffset(3, 4), Is.EqualTo(new Vector4(1f, 0.25f, 0f, 0f)));
        }

        [Test]
        public void AnimatedTextureMaterialShowsTheFirstFrameAndExposesPlayback()
        {
            var model = new BbModel();
            model.textures.Add(new BbTexture
            {
                name = "Strip",
                uv_width = 4,
                uv_height = 4,
                source = DataUri(Solid(4, 16, Color.white))
            });
            MaterialBuilder builder = Builder(model, new BlockbenchImporterSettings());
            builder.BuildTextures();

            Material material = builder.BuildMaterials()[0];

            Assert.That(builder.TryGetFlipbook(material, out var flipbook), Is.True);
            Assert.That(flipbook.frameCount, Is.EqualTo(4));
            Assert.That(flipbook.scaleOffsetProperty, Does.EndWith("_ST"));
            string property = flipbook.scaleOffsetProperty.Substring(0, flipbook.scaleOffsetProperty.Length - 3);
            Assert.That(material.GetTextureScale(property), Is.EqualTo(new Vector2(1f, 0.25f)));
            Assert.That(material.GetTextureOffset(property), Is.EqualTo(new Vector2(0f, 0.75f)));
        }

        [TestCase("free", "normal")]
        [TestCase("bedrock", "normal")]
        [TestCase("free", "height")]
        public void PbrTextureGroupsCombineChannelsIntoOneLitMaterial(string format, string surface)
        {
            var model = new BbModel { meta = new BbMeta { model_format = format } };
            model.texture_groups.Add(new BbTextureGroup { uuid = "group", name = "Metal", is_material = true });
            model.textures.Add(new BbTexture { name = "Color", group = "group", source = DataUri(Solid(2, 2, new Color32(200, 100, 50, 255))) });
            model.textures.Add(new BbTexture { name = "Surface", group = "group", pbr_channel = surface,
                source = DataUri(Solid(2, 2, new Color32(128, 64, 255, 255))) });
            model.textures.Add(new BbTexture { name = "Mer", group = "group", pbr_channel = "mer",
                source = DataUri(Solid(2, 2, new Color32(255, 128, 64, 255))) });
            MaterialBuilder builder = Builder(model, new BlockbenchImporterSettings { materialMode = BlockbenchMaterialMode.Lit });
            Dictionary<int, Texture2D> textures = builder.BuildTextures();

            Dictionary<int, Material> materials = builder.BuildMaterials();

            Assert.That(materials.Keys, Is.EqualTo(new[] { 0 }), "Channel textures feed the color texture's material.");
            Material material = materials[0];
            Assert.That(material.IsKeywordEnabled("_NORMALMAP"), Is.True);
            var normal = (Texture2D)material.GetTexture("_BumpMap");
            if (format == "free" && surface == "normal")
                Assert.That(normal, Is.SameAs(textures[1]));
            else if (surface == "normal")
                Assert.That(normal.GetPixels32()[0].g, Is.EqualTo(255 - 64), "Bedrock normal maps flip green.");
            else
                Assert.That(normal.GetPixels32()[0], Is.EqualTo(new Color32(128, 128, 255, 255)), "Flat heights face outward.");
            Assert.That(textures[1].isDataSRGB, Is.False, "PBR channel textures are linear data.");

            var metallic = (Texture2D)material.GetTexture("_MetallicGlossMap");
            Assert.That(metallic.GetPixels32()[0], Is.EqualTo(new Color32(255, 0, 0, 255 - 64)));
            var emission = (Texture2D)material.GetTexture("_EmissionMap");
            Assert.That(material.IsKeywordEnabled("_EMISSION"), Is.True);
            Assert.That(emission.GetPixels32()[0], Is.EqualTo(new Color32(200 * 128 / 255, 100 * 128 / 255, 50 * 128 / 255, 255)));
            Assert.That(builder.GetAllTextures().Count(), Is.GreaterThan(textures.Count), "Generated maps are sub-assets too.");
        }

        [Test]
        public void PbrGroupsWithoutMerTexturesUseConstantValuesAndUnlitModeWarns()
        {
            var model = new BbModel();
            model.texture_groups.Add(new BbTextureGroup { uuid = "group", name = "Gem", is_material = true, mer_value = new[] { 255f, 51f, 255f } });
            model.textures.Add(new BbTexture { name = "Color", group = "group", source = DataUri(Solid(1, 1, Color.white)) });
            MaterialBuilder lit = Builder(model, new BlockbenchImporterSettings { materialMode = BlockbenchMaterialMode.Lit });
            lit.BuildTextures();
            Material material = lit.BuildMaterials()[0];
            Assert.That(material.GetFloat("_Metallic"), Is.EqualTo(1f));
            Assert.That(material.GetTexture("_EmissionMap"), Is.SameAs(material.mainTexture));
            Assert.That(material.GetColor("_EmissionColor").r, Is.EqualTo(0.2f).Within(0.001f));

            MaterialBuilder unlit = Builder(model, new BlockbenchImporterSettings());
            unlit.BuildTextures();
            unlit.BuildMaterials();
            Assert.That(model.warnings, Has.Some.Contains("Set Material Mode to Lit"));
        }

        [Test]
        public void ImportAddsEffectAndTextureAnimationComponentsToTheRoot()
        {
            string strip = Convert.ToBase64String(Solid(2, 4, Color.white).EncodeToPNG());
            string json = @"{
              ""meta"":{""format_version"":""5.0"",""model_format"":""free""},
              ""resolution"":{""width"":2,""height"":2},
              ""elements"":[{""type"":""cube"",""uuid"":""cube"",""name"":""cube"",""from"":[0,0,0],""to"":[1,1,1],
                ""faces"":{""north"":{""uv"":[0,0,2,2],""texture"":0}}}],
              ""outliner"":[""cube""],
              ""textures"":[{""name"":""strip"",""uv_width"":2,""uv_height"":2,""source"":""data:image/png;base64," + strip + @"""}],
              ""animations"":[{""name"":""Idle"",""length"":1,""animators"":{""effects"":{""name"":""Effects"",""type"":""effect"",
                ""keyframes"":[{""channel"":""sound"",""time"":0.5,""data_points"":[{""effect"":""mob.idle""}]}]}}}]
            }";
            string folder = AssetDatabase.GenerateUniqueAssetPath("Assets/__BlockbenchEffectsTests");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            try
            {
                string path = folder + "/Effects.bbmodel";
                File.WriteAllText(path, json);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(root, Is.Not.Null);

                var receiver = root.GetComponent<BlockbenchAnimationEvents>();
                Assert.That(receiver, Is.Not.Null);
                Assert.That(receiver.Effects.Single().Name, Is.EqualTo("mob.idle"));
                AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Single();
                Assert.That(AnimationUtility.GetAnimationEvents(clip).Single().functionName, Is.EqualTo("OnBlockbenchEffect"));
                Assert.That(root.GetComponent<Animator>(), Is.Not.Null, "The receiver sits beside the generated Animator.");
                Assert.That(root.GetComponent<BlockbenchTextureAnimation>(), Is.Not.Null);
            }
            finally
            {
                AssetDatabase.DeleteAsset(folder);
            }
        }

        private static BbKeyframe Effect(string channel, float time, BbDataPoint point)
        {
            var keyframe = new BbKeyframe { channel = channel, time = time };
            keyframe.data_points.Add(point);
            return keyframe;
        }

        private static AnimationBuilder CreateAnimationBuilder(BbModel model) =>
            new AnimationBuilder(model, new Dictionary<string, string>(), new Dictionary<string, Transform>(),
                new BlockbenchImporterSettings());

        private MaterialBuilder Builder(BbModel model, BlockbenchImporterSettings settings)
        {
            var builder = new MaterialBuilder(model, "Assets/__BlockbenchEffectsAndTextureTests.bbmodel", settings);
            builders.Add(builder);
            return builder;
        }

        private Texture2D Solid(int width, int height, Color color)
        {
            var texture = Keep(new Texture2D(width, height, TextureFormat.RGBA32, false));
            texture.SetPixels(Enumerable.Repeat(color, width * height).ToArray());
            texture.Apply();
            return texture;
        }

        private static string DataUri(Texture2D texture) =>
            "data:image/png;base64," + Convert.ToBase64String(texture.EncodeToPNG());

        private T Keep<T>(T value) where T : Object
        {
            objects.Add(value);
            return value;
        }
    }
}
