using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RogueTrader.Core.Data;

/// <summary>
/// Мягкое чтение текстовых полей дата-пака.
///
/// В книге одни и те же величины записываются по-разному: бронебойность — это
/// «0», а урон — «1к10+3 Э»; ёмкость магазина — «10», а перезарядка — «1 ПД».
/// Пока модель ждала только строку, System.Text.Json спотыкался на первом числе
/// и отбрасывал СЕГОЛУ 05-items.json вместе со всем арсеналом: предметы просто
/// исчезали из программы без единого предупреждения.
///
/// Конвертер принимает и строку, и число, и «да/нет» — и приводит к строке.
/// </summary>
public sealed class LenientStringConverter : JsonConverter<string>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return reader.GetString();

            case JsonTokenType.Number:
                if (reader.TryGetInt64(out var i)) return i.ToString(CultureInfo.InvariantCulture);
                return reader.GetDouble().ToString("0.###", CultureInfo.InvariantCulture);

            case JsonTokenType.True:
                return "да";
            case JsonTokenType.False:
                return "нет";

            case JsonTokenType.Null:
                return null;

            default:
                reader.Skip();
                return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value);
}

/// <summary>Такой же, но для списков строк: элементы тоже могут прийти числами.</summary>
public sealed class LenientStringListConverter : JsonConverter<List<string>>
{
    public override List<string>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        if (reader.TokenType != JsonTokenType.StartArray) { reader.Skip(); return []; }

        var result = new List<string>();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray) break;
            result.Add(new LenientStringConverter().Read(ref reader, typeof(string), options) ?? "");
        }
        return result;
    }

    public override void Write(Utf8JsonWriter writer, List<string> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var s in value) writer.WriteStringValue(s);
        writer.WriteEndArray();
    }
}
