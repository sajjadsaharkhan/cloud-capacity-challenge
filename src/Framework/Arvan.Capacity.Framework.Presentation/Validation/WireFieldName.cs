namespace Arvan.Capacity.Framework.Presentation.Validation;

/// <summary>
/// Normalises the field names reported in an error's per-field details.
/// </summary>
/// <remarks>
/// Validation failures arrive from two independent sources — FluentValidation reports a CLR
/// property path (<c>Title</c>, <c>Items[0].Name</c>), model binding reports a JSON pointer
/// (<c>$.title</c>) or a parameter name. A client matches an error to its own field by this
/// string, so both have to come out in the casing the client actually sent.
/// </remarks>
internal static class WireFieldName
{
    /// <summary>
    /// Camel-cases each segment of a dotted field path, leaving already-lowercase segments and
    /// JSON-pointer prefixes untouched.
    /// </summary>
    /// <param name="fieldName">The field path as its source reported it.</param>
    public static string From(string fieldName) =>
        string.IsNullOrEmpty(fieldName)
            ? fieldName
            : string.Join('.', fieldName.Split('.').Select(CamelCaseSegment));

    /// <summary>
    /// Lower-cases the leading character of one path segment, leaving any indexer suffix intact.
    /// </summary>
    private static string CamelCaseSegment(string segment) =>
        segment.Length == 0 || !char.IsUpper(segment[0])
            ? segment
            : char.ToLowerInvariant(segment[0]) + segment[1..];
}
