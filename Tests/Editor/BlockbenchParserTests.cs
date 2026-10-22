using System;
using System.Collections.Generic;
using MartinCalander.BlockbenchImporter.Model;
using MartinCalander.BlockbenchImporter.Parsing;
using NUnit.Framework;

namespace MartinCalander.BlockbenchImporter.Tests
{
    public sealed class BlockbenchParserTests
    {
        private const string MinimalModel = @"{
          ""meta"":{""format_version"":""5.0"",""model_format"":""free"",""box_uv"":false},
          ""name"":""Parser Fixture"",
          ""visible_box"":[1,2,3],
          ""resolution"":{""width"":32,""height"":16},
          ""elements"":[
            {""name"":""cube"",""type"":""cube"",""uuid"":""cube-id"",""from"":[0,0,0],""to"":[2,4,6],""origin"":[1,2,3],""stretch"":[2,1,0.5],""faces"":{""north"":{""uv"":[0,0,2,4],""texture"":0}}},
            {""name"":""socket"",""type"":""locator"",""uuid"":""locator-id"",""position"":[3,4,5],""rotation"":[10,20,30]}
          ],
          ""groups"":[{""name"":""root"",""uuid"":""group-id"",""origin"":[0,0,0]}],
          ""outliner"":[{""name"":""root"",""uuid"":""group-id"",""children"":[""cube-id"",""locator-id""]}],
          ""textures"":[{""name"":""texture"",""id"":""0"",""uuid"":""texture-id"",""relative_path"":""texture.png"",""width"":32,""height"":16}],
          ""animations"":[{""name"":""move"",""length"":1,""snapping"":20,""animators"":{""group-id"":{""name"":""root"",""type"":""bone"",""rotation_global"":true,""quaternion_interpolation"":true,""keyframes"":[{""channel"":""position"",""time"":0,""interpolation"":""bezier"",""bezier_left_time"":[-0.1,-0.1,-0.1],""bezier_right_time"":[0.1,0.1,0.1],""data_points"":[{""x"":""1"",""y"":""2"",""z"":""3""}]}]}}}]
        }";

        [TestCase(" ")]
        [TestCase("{")]
        [TestCase("[1")]
        [TestCase("{\"name\":\"unfinished}")]
        [TestCase("{\"x\":1,}")]
        [TestCase("[1,]")]
        [TestCase("[1 2]")]
        [TestCase("{\"x\" 1}")]
        [TestCase("{\"x\":}")]
        [TestCase("{} trailing")]
        [TestCase("[01]")]
        [TestCase("[1.]")]
        [TestCase("[1e+]")]
        [TestCase("[1e999]")]
        [TestCase("[truefalse]")]
        [TestCase("\"bad\\q\"")]
        [TestCase("\"bad\\u12\"")]
        [TestCase("\"bad\\uZZZZ\"")]
        [TestCase("\"line\nbreak\"")]
        public void JsonRejectsMalformedOrTruncatedContent(string json)
        {
            Assert.Throws<FormatException>(() => MiniJson.Deserialize(json));
        }

        [Test]
        public void JsonPreservesUnicodeEscapesLiteralsAndNumberBounds()
        {
            var values = (List<object>)MiniJson.Deserialize(
                "[\"å猫\",\"\\uD83D\\uDE00\\n\\t\\\\\\\"\\/\",null,true,false," +
                "-9223372036854775808,9223372036854775807,1.25e-2,-0,1e100]");
            Assert.That(values[0], Is.EqualTo("å猫"));
            Assert.That(values[1], Is.EqualTo("\U0001F600\n\t\\\"/"));
            Assert.That(values[2], Is.Null);
            Assert.That(values[3], Is.EqualTo(true));
            Assert.That(values[4], Is.EqualTo(false));
            Assert.That(values[5], Is.EqualTo(long.MinValue));
            Assert.That(values[6], Is.EqualTo(long.MaxValue));
            Assert.That(values[7], Is.EqualTo(0.0125d));
            Assert.That(values[8], Is.EqualTo(0L));
            Assert.That(values[9], Is.EqualTo(1e100d));
            Assert.That(MiniJson.Deserialize(MiniJson.Serialize(values)), Is.EqualTo(values));
        }

        [Test]
        public void JsonLimitsNestingBeforeTheManagedStackOverflows()
        {
            Assert.That(MiniJson.Deserialize(new string('[', 128) + "0" + new string(']', 128)), Is.Not.Null);
            Assert.Throws<FormatException>(() => MiniJson.Deserialize(
                new string('[', 129) + "0" + new string(']', 129)));
        }

        [TestCase("1e100")]
        [TestCase("\"NaN\"")]
        [TestCase("\"Infinity\"")]
        public void ModelRejectsNonFiniteGeometryValues(string value)
        {
            Assert.Throws<FormatException>(() => BbModelParser.Parse(
                "{\"elements\":[{\"from\":[" + value + ",0,0]}]}"));
        }

        [TestCase("NaN")]
        [TestCase("Infinity")]
        [TestCase("-Infinity")]
        [TestCase("1e100")]
        public void AnimationNonFiniteStringsResolveToZeroWithAWarning(string value)
        {
            var model = BbModelParser.Parse("{\"meta\":{\"format_version\":\"5.0\"}," +
                "\"animations\":[{\"animators\":{\"node\":{\"keyframes\":[{\"channel\":\"position\"," +
                "\"data_points\":[{\"x\":\"" + value + "\"}]}]}}}]}");
            Assert.That(model.animations[0].animators["node"].keyframes[0].data_points[0].ToVector3(),
                Is.EqualTo(UnityEngine.Vector3.zero));
            Assert.That(model.warnings, Has.Some.Contains("resolves to zero"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void HierarchyRejectsSelfReferencesAndIndirectCycles(bool indirect)
        {
            var model = new BbModel();
            model.elements.Add(new BbElement { uuid = "first", children = new List<object> { indirect ? "second" : "first" } });
            if (indirect)
                model.elements.Add(new BbElement { uuid = "second", children = new List<object> { "first" } });
            model.outliner.Add("first");
            model.BuildLookupTables();
            Assert.Throws<FormatException>(() => BbModelParser.BuildHierarchy(model));
        }

        [Test]
        public void HierarchyLimitsDepthOfFlatElementReferences()
        {
            var model = new BbModel();
            for (int index = 0; index < 130; index++)
                model.elements.Add(new BbElement { uuid = index.ToString(), children = new List<object> { (index + 1).ToString() } });
            model.outliner.Add("0");
            model.BuildLookupTables();
            Assert.Throws<FormatException>(() => BbModelParser.BuildHierarchy(model));
        }

        [Test]
        public void HierarchyKeepsAnonymousGroupsAndSkipsDuplicateReferences()
        {
            var model = BbModelParser.Parse(@"{
              ""elements"":[{""uuid"":""node""}],
              ""outliner"":[{""name"":""anonymous"",""children"":[""node"",""node""]},""node""]
            }");
            var nodes = BbModelParser.BuildHierarchy(model);
            Assert.That(nodes, Has.Count.EqualTo(1));
            Assert.That(nodes[0].group.name, Is.EqualTo("anonymous"));
            Assert.That(nodes[0].children, Has.Count.EqualTo(1));
            Assert.That(model.warnings, Has.Some.Contains("Duplicate outliner reference"));
        }

        [Test]
        public void Parse_PreservesCurrentProjectSchemaFields()
        {
            var model = BbModelParser.Parse(MinimalModel);
            Assert.That(model.meta.format_version, Is.EqualTo("5.0"));
            Assert.That(model.visible_box, Is.EqualTo(new[] { 1f, 2f, 3f }));
            Assert.That(model.resolution, Is.EqualTo(new[] { 32, 16 }));
            Assert.That(model.elements, Has.Count.EqualTo(2));
            Assert.That(model.elements[0].stretch, Is.EqualTo(new[] { 2f, 1f, 0.5f }));
            Assert.That(model.elements[1].position, Is.EqualTo(new[] { 3f, 4f, 5f }));
            Assert.That(model.textures[0].relative_path, Is.EqualTo("texture.png"));
            Assert.That(model.textures[0].uv_width, Is.EqualTo(32));
            Assert.That(model.textures[0].uv_height, Is.EqualTo(16));
            Assert.That(model.animations[0].animators["group-id"].rotation_global, Is.True);
            Assert.That(model.animations[0].animators["group-id"].quaternion_interpolation, Is.True);
            Assert.That(BbModelParser.BuildHierarchy(model)[0].children, Has.Count.EqualTo(2));
        }

        [Test]
        public void Parse_MissingScaleAxesAndBezierHandlesUseBlockbenchDefaults()
        {
            var model = BbModelParser.Parse(@"{
              ""meta"":{""format_version"":""5.0""},
              ""animations"":[{""animators"":{""node"":{""keyframes"":[
                {""channel"":""scale"",""interpolation"":""bezier"",""data_points"":[{""x"":2}]}
              ]}}}]
            }");
            var key = model.animations[0].animators["node"].keyframes[0];
            Assert.That(key.data_points[0].ToVector3(), Is.EqualTo(new UnityEngine.Vector3(2, 1, 1)));
            Assert.That(key.bezier_left_time, Is.EqualTo(new[] { -0.1f, -0.1f, -0.1f }));
            Assert.That(key.bezier_right_time, Is.EqualTo(new[] { 0.1f, 0.1f, 0.1f }));
            Assert.That(key.bezier_left_value, Is.EqualTo(new float[3]));
            Assert.That(key.bezier_right_value, Is.EqualTo(new float[3]));
            Assert.That(key.bezier_linked, Is.True);
        }

        [TestCase("bedrock", 0, 0)]
        [TestCase("bedrock_old", 0, 0)]
        [TestCase("bedrock_block", 1, 0)]
        [TestCase("optifine_entity", 1, 0)]
        [TestCase("optifine_part", 1, 0)]
        [TestCase("free", -1, 0)]
        public void Parse_DefaultTexturesPreserveDisabledFacesAndExplicitAssignments(
            string format, int expectedDefault, int expectedAssigned)
        {
            string json = @"{
              ""meta"":{""format_version"":""5.0"",""model_format"":""FORMAT""},
              ""textures"":[{""name"":""first""},{""name"":""default"",""use_as_default"":true}],
              ""elements"":[{""type"":""cube"",""uuid"":""cube"",""faces"":{
                ""north"":{""uv"":[0,0,8,8]},
                ""south"":{""uv"":[0,0,8,8],""texture"":false},
                ""east"":{""uv"":[0,0,8,8],""texture"":null},
                ""west"":{""uv"":[0,0,8,8],""texture"":0}
              }}],""outliner"":[""cube""]
            }".Replace("FORMAT", format);
            var faces = BbModelParser.Parse(json).elements[0].faces;
            Assert.That(faces["north"].texture, Is.EqualTo(expectedDefault));
            Assert.That(faces["south"].texture, Is.EqualTo(expectedDefault));
            Assert.That(faces["east"].enabled, Is.False);
            Assert.That(faces["west"].texture, Is.EqualTo(expectedAssigned));
        }

        [TestCase("4.10", -1f, -1f)]
        [TestCase("5.0", 1f, 1f)]
        public void Parse_AnimationAxesFollowTheSavedFormatVersion(string version, float xSign, float rotationYSign)
        {
            string json = @"{
              ""meta"":{""format_version"":""VERSION""},
              ""animations"":[{""animators"":{""node"":{""keyframes"":[
                {""channel"":""position"",""data_points"":[{""x"":2,""y"":3,""z"":4},{""x"":5,""y"":6,""z"":7}],
                 ""interpolation"":""bezier"",""bezier_left_value"":[1,2,3],""bezier_right_value"":[4,5,6]},
                {""channel"":""rotation"",""data_points"":[{""x"":10,""y"":20,""z"":30}],
                 ""interpolation"":""bezier"",""bezier_left_value"":[1,2,3],""bezier_right_value"":[4,5,6]},
                {""channel"":""scale"",""data_points"":[{""x"":2,""y"":3,""z"":4}]}
              ]}}}]
            }".Replace("VERSION", version);
            var keys = BbModelParser.Parse(json).animations[0].animators["node"].keyframes;
            Assert.That(keys[0].data_points[0].ToVector3(),
                Is.EqualTo(new UnityEngine.Vector3(2 * xSign, 3, 4)));
            Assert.That(keys[0].data_points[1].ToVector3(),
                Is.EqualTo(new UnityEngine.Vector3(5 * xSign, 6, 7)));
            Assert.That(keys[0].bezier_right_value, Is.EqualTo(new[] { 4 * xSign, 5, 6 }));
            Assert.That(keys[1].data_points[0].ToVector3(),
                Is.EqualTo(new UnityEngine.Vector3(10 * xSign, 20 * rotationYSign, 30)));
            Assert.That(keys[1].bezier_left_value, Is.EqualTo(new[] { xSign, 2 * rotationYSign, 3 }));
            Assert.That(keys[2].data_points[0].ToVector3(), Is.EqualTo(new UnityEngine.Vector3(2, 3, 4)));
        }
    }
}
