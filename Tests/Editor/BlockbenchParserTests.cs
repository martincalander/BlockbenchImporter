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
            Assert.That(model.animations[0].animators["group-id"].rotation_global, Is.True);
            Assert.That(model.animations[0].animators["group-id"].quaternion_interpolation, Is.True);
            Assert.That(BbModelParser.BuildHierarchy(model)[0].children, Has.Count.EqualTo(2));
        }

        [Test]
        public void Parse_NullFaceTextureDisablesFace()
        {
            string json = MinimalModel.Replace(@"""texture"":0", @"""texture"":null");
            var model = BbModelParser.Parse(json);
            Assert.That(model.elements[0].faces["north"].enabled, Is.False);
        }
    }
}
