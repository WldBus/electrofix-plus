using System.Text.RegularExpressions;

namespace ElectroFix.Shared;

// Поле с ФИО человека. Администратору БД Api отдаёт такие поля скрытыми:
// управлять учётками он может и по email, а знать имена людей ему незачем
[AttributeUsage(AttributeTargets.Property)]
public sealed class PersonalNameAttribute : Attribute;

// Поле с JSON строки таблицы (журнал аудита): внутри скрываю full_name и display_name
[AttributeUsage(AttributeTargets.Property)]
public sealed class PersonalJsonAttribute : Attribute;

public static partial class PersonalData
{
    private static readonly string[] OrgPrefixes = ["ООО", "ОАО", "ЗАО", "ПАО", "АО", "ИП", "УК", "НКО", "ТСЖ"];

    // «Иванов Алексей Андреевич» → «И****в А. А.». Названия организаций не трогаю
    public static string? MaskName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || !LooksLikePerson(name))
        {
            return name;
        }

        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var surname = words[0];
        var masked = surname.Length <= 2
            ? surname[0] + "*"
            : surname[0] + new string('*', surname.Length - 2) + surname[^1];
        var initials = words.Skip(1).Select(w => w[0] + ".");
        return string.Join(' ', new[] { masked }.Concat(initials));
    }

    // в JSON из аудита скрываю значения полей full_name и display_name
    public static string? MaskJson(string? json) =>
        json == null
            ? null
            : NameInJson().Replace(json, m => $"{m.Groups[1].Value}\"{MaskName(m.Groups[2].Value)}\"");

    private static bool LooksLikePerson(string name)
    {
        // у организаций кавычки, цифры или ООО/ИП/УК в начале
        if (name.IndexOfAny(['«', '»', '"', '\'']) >= 0 || name.Any(char.IsDigit)
            || OrgPrefixes.Any(p => name.StartsWith(p + " ", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length is >= 1 and <= 4 && words.All(w => char.IsUpper(w[0]));
    }

    [GeneratedRegex("(\"(?:full_name|display_name)\"\\s*:\\s*)\"([^\"]*)\"")]
    private static partial Regex NameInJson();
}
