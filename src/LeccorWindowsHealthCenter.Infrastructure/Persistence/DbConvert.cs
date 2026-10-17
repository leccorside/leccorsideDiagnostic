using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;

namespace LeccorWindowsHealthCenter.Infrastructure.Persistence;

internal static class DbConvert
{
    public static readonly JsonSerializerOptions Json = new()
    {
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = false,
    };

    public static string Date(DateTimeOffset value) => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    public static object DateOrNull(DateTimeOffset? value) => value is null ? DBNull.Value : Date(value.Value);

    public static DateTimeOffset ParseDate(string value) => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    public static object OrNull(object? value) => value ?? DBNull.Value;

    public static string ToJson<T>(T value) => JsonSerializer.Serialize(value, Json);

    public static T? FromJson<T>(string? json) => string.IsNullOrEmpty(json) ? default : JsonSerializer.Deserialize<T>(json, Json);

    public static SqliteParameter Add(this SqliteCommand command, string name, object? value) =>
        command.Parameters.AddWithValue(name, value ?? DBNull.Value);

    public static string? GetNullableString(this SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
}
