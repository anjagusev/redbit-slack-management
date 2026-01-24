using System.Text.Json;

namespace RedBit.Slack.Management.Extensions;

/// <summary>
/// Core JSON utility extension methods for JsonElement.
/// </summary>
internal static class JsonElementExtensions
{
    extension(JsonElement element)
    {
        public JsonElement? GetPropertyOrNull(string propertyName)
        {
            if (element.ValueKind != JsonValueKind.Object) return null;
            return element.TryGetProperty(propertyName, out var prop) ? prop : null;
        }

        public string? GetStringOrNull(string propertyName)
        {
            if (element.ValueKind != JsonValueKind.Object) return null;
            if (!element.TryGetProperty(propertyName, out var prop)) return null;
            return prop.ValueKind == JsonValueKind.String ? prop.GetString() : prop.ToString();
        }

        public bool? GetBoolOrNull(string propertyName)
        {
            if (element.ValueKind != JsonValueKind.Object) return null;
            if (!element.TryGetProperty(propertyName, out var prop)) return null;
            return prop.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null
            };
        }

        public long? GetLongOrNull(string propertyName)
        {
            if (element.ValueKind != JsonValueKind.Object) return null;
            if (!element.TryGetProperty(propertyName, out var prop)) return null;
            return prop.ValueKind == JsonValueKind.Number && prop.TryGetInt64(out var value) ? value : null;
        }

        public int? GetIntOrNull(string propertyName)
        {
            if (element.ValueKind != JsonValueKind.Object) return null;
            if (!element.TryGetProperty(propertyName, out var prop)) return null;
            return prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var value) ? value : null;
        }

        public string[] GetStringArrayOrEmpty(string propertyName)
        {
            if (element.ValueKind != JsonValueKind.Object) return [];
            if (!element.TryGetProperty(propertyName, out var prop)) return [];
            if (prop.ValueKind != JsonValueKind.Array) return [];

            var result = new List<string>();
            foreach (var item in prop.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    var str = item.GetString();
                    if (str != null) result.Add(str);
                }
            }
            return result.ToArray();
        }
    }
}
