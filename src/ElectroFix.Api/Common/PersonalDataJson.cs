using System.Text.Json.Serialization.Metadata;

using ElectroFix.Shared;

namespace ElectroFix.Api.Common;

// Скрытие ФИО для администратора БД. Тут, а не в панели: тогда и прямой запрос
// админа к Api (Swagger, Postman) не отдаст полное имя. Помеченные [PersonalName]
// поля при отправке ответа превращаются в «И****в А. А.». Сотрудники видят как есть,
// свою учётку админ тоже видит полностью
public static class PersonalDataJson
{
    public static Action<JsonTypeInfo> Modifier(IHttpContextAccessor http) => info =>
    {
        foreach (var property in info.Properties)
        {
            var isName = property.AttributeProvider?.IsDefined(typeof(PersonalNameAttribute), false) == true;
            var isJson = property.AttributeProvider?.IsDefined(typeof(PersonalJsonAttribute), false) == true;
            if ((!isName && !isJson) || property.Get is not { } get)
            {
                continue;
            }

            property.Get = owner =>
            {
                var value = get(owner) as string;
                if (!ShouldMask(http.HttpContext, owner))
                {
                    return value;
                }
                return isName ? PersonalData.MaskName(value) : PersonalData.MaskJson(value);
            };
        }
    };

    private static bool ShouldMask(HttpContext? context, object owner)
    {
        var user = context?.User;
        if (user?.IsInRole(RoleCodes.Admin) != true)
        {
            return false;
        }
        return owner is not UserDto u || u.UserId.ToString() != user.FindFirst("sub")?.Value;
    }
}
