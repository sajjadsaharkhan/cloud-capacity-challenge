using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace Arvan.Capacity.Framework.Presentation.Json;

/// <summary>
/// The wire format's JSON rules, applied to every serializer an enveloped request or response
/// travels through.
/// </summary>
/// <remarks>
/// Enums cross the API boundary as their member-name strings in both directions: writes emit the
/// member name, reads accept it in any casing and reject the raw number. Registering the converter
/// here rather than on each DTO makes that a property of the boundary, not of whoever remembered
/// an attribute.
/// </remarks>
public static class WireJson
{
    /// <summary>
    /// Applies the wire JSON rules to <paramref name="options"/>.
    /// </summary>
    /// <param name="options">The serializer options to apply the rules to.</param>
    public static void Apply(JsonSerializerOptions options)
    {
        options.Encoder = JavaScriptEncoder.Create(UnicodeRanges.All);

        // No naming policy: values must read and write as the exact member names the manual
        // response mappers already emit and the MySQL enum columns already store.
        options.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
    }
}
