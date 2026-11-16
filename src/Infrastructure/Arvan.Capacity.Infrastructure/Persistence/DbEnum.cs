using System.Text;

namespace Arvan.Capacity.Infrastructure.Persistence;

/// <summary>
/// Converts between an enum member and the text the database stores: <c>RamMb</c> is
/// <c>RAM_MB</c>, <c>CapacitySoldOut</c> is <c>CAPACITY_SOLD_OUT</c>.
/// </summary>
internal static class DbEnum
{
    /// <summary>
    /// The stored text of an enum member.
    /// </summary>
    /// <param name="value">The member.</param>
    public static string ToDb<T>(T value) where T : struct, Enum
    {
        var name = value.ToString();
        var text = new StringBuilder(name.Length + 4);

        for (var i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]) && char.IsLower(name[i - 1]))
                text.Append('_');

            text.Append(char.ToUpperInvariant(name[i]));
        }

        return text.ToString();
    }

    /// <summary>
    /// The stored text of an optional enum member.
    /// </summary>
    /// <param name="value">The member, or <c>null</c>.</param>
    public static string? ToDb<T>(T? value) where T : struct, Enum => value is null ? null : ToDb(value.Value);

    /// <summary>
    /// The enum member a stored text names.
    /// </summary>
    /// <param name="text">The stored text.</param>
    public static T FromDb<T>(string text) where T : struct, Enum =>
        Enum.Parse<T>(string.Concat(text.Split('_').Select(Capitalize)));

    private static string Capitalize(string part) =>
        part.Length is 0 ? part : char.ToUpperInvariant(part[0]) + part[1..].ToLowerInvariant();
}
