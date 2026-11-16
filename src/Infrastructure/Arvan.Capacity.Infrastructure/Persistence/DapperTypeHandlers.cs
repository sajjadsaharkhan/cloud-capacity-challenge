using System.Data;
using Arvan.Capacity.Domain.Enums;
using Dapper;

namespace Arvan.Capacity.Infrastructure.Persistence;

/// <summary>
/// Registers the Dapper rules that map the database's text values onto domain types: enums as
/// UPPER_SNAKE text, a product's allowed classes as a text array, and snake_case columns onto
/// PascalCase members.
/// </summary>
internal static class DapperTypeHandlers
{
    private static readonly object Gate = new();
    private static bool _registered;

    /// <summary>
    /// Registers the handlers once for the process.
    /// </summary>
    public static void Register()
    {
        lock (Gate)
        {
            if (_registered)
                return;

            DefaultTypeMap.MatchNamesWithUnderscores = true;

            SqlMapper.AddTypeHandler(new EnumTextHandler<ReservationStatus>());
            SqlMapper.AddTypeHandler(new EnumTextHandler<AllocationState>());
            SqlMapper.AddTypeHandler(new EnumTextHandler<ReasonCode>());
            SqlMapper.AddTypeHandler(new EnumTextHandler<ShortagePolicy>());
            SqlMapper.AddTypeHandler(new EnumTextHandler<ProductStatus>());
            SqlMapper.AddTypeHandler(new EnumTextHandler<ResourceClass>());
            SqlMapper.AddTypeHandler(new ResourceClassListHandler());

            _registered = true;
        }
    }

    private sealed class EnumTextHandler<T> : SqlMapper.TypeHandler<T> where T : struct, Enum
    {
        public override void SetValue(IDbDataParameter parameter, T value)
        {
            parameter.DbType = DbType.String;
            parameter.Value = DbEnum.ToDb(value);
        }

        public override T Parse(object value) => DbEnum.FromDb<T>((string)value);
    }

    private sealed class ResourceClassListHandler : SqlMapper.TypeHandler<IReadOnlyList<ResourceClass>>
    {
        public override void SetValue(IDbDataParameter parameter, IReadOnlyList<ResourceClass>? value) =>
            parameter.Value = value?.Select(c => DbEnum.ToDb(c)).ToArray();

        public override IReadOnlyList<ResourceClass> Parse(object value) =>
            [.. ((string[])value).Select(DbEnum.FromDb<ResourceClass>)];
    }
}
