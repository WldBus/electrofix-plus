using System.Globalization;

using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ElectroFix.Web.Infrastructure;

// Поле <input type="number"> браузер всегда отправляет с точкой (0.5), а с русской культурой
// MVC ждёт запятую и не может разобрать число. Читаю форму без культуры, как и адресную строку
public class InvariantFormValueProviderFactory : IValueProviderFactory
{
    public async Task CreateValueProviderAsync(ValueProviderFactoryContext context)
    {
        var request = context.ActionContext.HttpContext.Request;
        if (request.HasFormContentType)
        {
            var form = await request.ReadFormAsync();
            context.ValueProviders.Add(new FormValueProvider(BindingSource.Form, form, CultureInfo.InvariantCulture));
        }
    }
}
