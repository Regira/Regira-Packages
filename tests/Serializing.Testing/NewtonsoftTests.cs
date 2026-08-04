using JsonSerializer = Regira.Serializing.Newtonsoft.Json.JsonSerializer;

namespace Serializing.Testing;

public enum TestStatus
{
    Simple,
    Hard
}
public struct Test
{
    public Guid? Guid { get; set; }
    public TestStatus? Status { get; set; }
    public bool? IsActive { get; set; }
    public bool IsArchived { get; set; }
}

[TestFixture]
public class NewtonsoftTests
{
    [Test]
    public void Test_Guid()
    {
        var input = new Test { Guid = Guid.NewGuid() };

        var serializer = new JsonSerializer();
        var json = serializer.Serialize(input);
        var output = serializer.Deserialize<Test>(json);

        Assert.That(output, Is.EqualTo(input));
        Assert.That(output.Guid, Is.EqualTo(input.Guid));
        Assert.That(output.Guid?.ToString("N"), Is.EqualTo(input.Guid?.ToString("N")));
    }

    [Test]
    public void Test_Read_Bool_From_Number()
    {
        var jsonInput0 = "{ isActive: 0 }";
        var jsonInput1 = "{ isActive: 1 }";
        var jsonInputFalse = "{ isActive: false }";
        var jsonInputTrue = "{ isActive: true }";
        var jsonInputNull = "{ isActive: null }";
        var jsonInputEmpty = "{ }";

        var serializer = new JsonSerializer();

        var output0 = serializer.Deserialize<Test>(jsonInput0);
        Assert.That(output0.IsActive, Is.False);

        var output1 = serializer.Deserialize<Test>(jsonInput1);
        Assert.That(output1.IsActive, Is.True);

        var outputFalse = serializer.Deserialize<Test>(jsonInputFalse);
        Assert.That(outputFalse.IsActive, Is.False);

        var outputTrue = serializer.Deserialize<Test>(jsonInputTrue);
        Assert.That(outputTrue.IsActive, Is.True);

        var outputNull = serializer.Deserialize<Test>(jsonInputNull);
        Assert.That(outputNull.IsActive, Is.Null);

        var outputEmpty = serializer.Deserialize<Test>(jsonInputEmpty);
        Assert.That(outputEmpty.IsActive, Is.Null);
    }

    [Test]
    public void Test_Write_Bool_As_Number()
    {
        var serializer = new JsonSerializer(new JsonSerializer.Options { BoolAsNumber = true });

        var input1 = new Test { IsActive = false, IsArchived = true };
        var json1 = serializer.Serialize(input1);
        var expected1 = @"{""isActive"":0,""isArchived"":1}";
        Assert.That(json1, Is.EqualTo(expected1));
        var output1 = serializer.Deserialize<Test>(json1);
        Assert.That(output1.IsActive, Is.EqualTo(input1.IsActive));
        Assert.That(output1.IsArchived, Is.EqualTo(input1.IsArchived));

        var input2 = new Test { IsArchived = true };
        var json2 = serializer.Serialize(input2);
        var expected2 = @"{""isArchived"":1}";
        Assert.That(json2, Is.EqualTo(expected2));
        var output2 = serializer.Deserialize<Test>(json2);
        Assert.That(output2.IsActive, Is.EqualTo(input2.IsActive));
        Assert.That(output2.IsActive, Is.Null);
        Assert.That(output2.IsArchived, Is.EqualTo(input2.IsArchived));
    }

    [Test]
    public void Test_EnumString()
    {
        var input = new Test { Status = TestStatus.Hard };

        var serializer = new JsonSerializer(new JsonSerializer.Options { EnumAsString = true });
        var json = serializer.Serialize(input);
        var output = serializer.Deserialize<Test>(json);

        var expected = $@"{{""status"":""{TestStatus.Hard}"",""isArchived"":0}}";
        Assert.That(json, Is.EqualTo(expected));
        Assert.That(json.Contains(TestStatus.Hard.ToString()), Is.True);
        Assert.That(output.Status, Is.EqualTo(input.Status));
    }
}