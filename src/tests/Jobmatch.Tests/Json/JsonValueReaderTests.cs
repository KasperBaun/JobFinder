using System.Text.Json;
using Jobmatch.Json;

namespace Jobmatch.Tests.Json;

public sealed class JsonValueReaderTests
{
    [Fact]
    public void Walk_Dotted_Object_Path_Resolves()
    {
        using var doc = JsonDocument.Parse("""{ "a": { "b": { "c": 42 } } }""");
        var el = JsonValueReader.Walk(doc.RootElement, "a.b.c");
        Assert.Equal(42, el.GetInt32());
    }

    [Fact]
    public void Walk_Numeric_Segment_Indexes_Into_Array()
    {
        // Oracle Recruiting nests the requisitions at items[0].requisitionList —
        // numeric path segments index into arrays.
        using var doc = JsonDocument.Parse("""
            { "items": [ { "requisitionList": [ { "Title": "Senior Engineer" } ] } ] }
            """);
        var el = JsonValueReader.Walk(doc.RootElement, "items.0.requisitionList");
        Assert.Equal(JsonValueKind.Array, el.ValueKind);
        Assert.Equal("Senior Engineer", el[0].GetProperty("Title").GetString());
    }

    [Fact]
    public void Walk_Numeric_Segment_Out_Of_Bounds_Returns_Default()
    {
        using var doc = JsonDocument.Parse("""{ "items": [ ] }""");
        var el = JsonValueReader.Walk(doc.RootElement, "items.0.requisitionList");
        Assert.Equal(JsonValueKind.Undefined, el.ValueKind);
    }

    [Fact]
    public void Walk_Numeric_Key_On_Object_Still_Resolves_As_Property()
    {
        // A property literally named "0" must keep working — array indexing only
        // applies when the current element IS an array.
        using var doc = JsonDocument.Parse("""{ "0": { "x": 1 } }""");
        var el = JsonValueReader.Walk(doc.RootElement, "0.x");
        Assert.Equal(1, el.GetInt32());
    }
}
