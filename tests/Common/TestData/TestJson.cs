using System.Text.Json;

namespace ACI.Tests.Common.TestData;

public static class TestJson
{
    public static JsonElement Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    public static JsonElement From(object value)
    {
        return JsonSerializer.SerializeToElement(value);
    }
}

