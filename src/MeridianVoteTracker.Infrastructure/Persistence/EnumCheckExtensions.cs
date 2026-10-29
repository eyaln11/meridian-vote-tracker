using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MeridianVoteTracker.Infrastructure.Persistence;

public static class EnumCheckExtensions
{
    /// <summary>
    /// Makes the database reject any text that is not a name of the C# enum stored in <paramref name="column"/>.
    /// The list is built from the enum itself, so the code and the constraint cannot drift apart:
    /// adding an enum value changes the constraint in the next migration.
    /// </summary>
    public static TableBuilder<TEntity> HasEnumCheck<TEntity>(
        this TableBuilder<TEntity> table, string constraintName, string column, Type enumType)
        where TEntity : class
    {
        var values = string.Join(", ", Enum.GetNames(enumType).Select(name => $"'{name}'"));
        table.HasCheckConstraint(constraintName, $"{column} IN ({values})");
        return table;
    }
}
