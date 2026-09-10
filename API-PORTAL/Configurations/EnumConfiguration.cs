using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace API_PORTAL.Configurations;

public static class EnumConfiguration
{
    public static PropertyBuilder<TEnum> HasVarcharEnum<TEnum>(
        this PropertyBuilder<TEnum> property)
        where TEnum : struct, Enum
    {
        return property
            .HasConversion<string>()
            .HasColumnType("varchar");
    }

    public static TableBuilder HasEnumCheck<TEnum>(
        this TableBuilder table,
        string constraintName,
        string columnName)
        where TEnum : struct, Enum
    {
        var values = string.Join(
            ", ",
            Enum.GetNames<TEnum>().Select(value => $"'{value}'"));

        table.HasCheckConstraint(
            constraintName,
            $"{columnName} IN ({values})");

        return table;
    }
}
