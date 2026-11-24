using System.Globalization;
using System.Text.Encodings.Web;

using ElectroFix.Shared;

using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ElectroFix.Web.Infrastructure;

// Мелкие помощники для вьюх: ссылка сортировки в заголовке таблицы и вывод дат/денег
public static class HtmlHelpers
{
    private static readonly TimeZoneInfo Moscow = TimeZoneInfo.FindSystemTimeZoneById("Europe/Moscow");
    private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

    // клик по заголовку — сортировка по возрастанию, ещё клик — по убыванию
    public static IHtmlContent SortHeader(this IHtmlHelper html, string field, string title)
    {
        var request = html.ViewContext.HttpContext.Request;
        var current = request.Query["sort"].ToString();
        var asc = current == field || current == field + ",asc";
        var desc = current == field + ",desc";
        var url = ListUrl.With(request, ("sort", asc ? field + ",desc" : field), ("page", null));
        var arrow = asc ? " <i class=\"bi bi-caret-up-fill\"></i>" : desc ? " <i class=\"bi bi-caret-down-fill\"></i>" : "";
        return new HtmlString(
            $"<a class=\"sort-link\" href=\"{HtmlEncoder.Default.Encode(url)}\">{HtmlEncoder.Default.Encode(title)}{arrow}</a>");
    }

    // в базе время в UTC, показываю по Москве
    public static string Time(DateTime? utc) =>
        utc == null
            ? "–"
            : TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc), Moscow)
                .ToString("dd.MM.yyyy HH:mm", Ru);

    public static string Day(DateTime? utc) => utc == null ? "–" : Time(utc)[..10];

    public static string Money(decimal value) => value.ToString("N2", Ru) + " ₽";

    public static string Month(DateOnly month) => month.ToString("MMM yy", Ru);

    public static string Num(decimal value) => value.ToString("0.##", Ru);

    // цена в прайсе на сайте: без копеек, «1 200 ₽»
    public static string Price(decimal value) => value.ToString("N0", Ru) + " ₽";

    // норма времени работы: меньше часа — в минутах, чтобы не писать «0,75 ч»
    public static string Duration(decimal hours) =>
        hours < 1 ? $"{hours * 60:0} мин" : $"{Num(hours)} ч";

    // цветная метка статуса: словарь подписей передаю, потому что «cancelled» есть и у записи, и у заказа
    public static IHtmlContent Status(Dictionary<string, string> labels, string? code)
    {
        if (code == null)
        {
            return new HtmlString("–");
        }
        // спокойные метки: светлый фон и текст того же тона, в тёмной теме Bootstrap их сам приглушает
        var tone = code switch
        {
            "closed" or "paid" or "approved" or "checked_in" or "done" => "success",
            "cancelled" or "rejected" or "no_show" => "secondary",
            "ready" or "partially_paid" or "confirmed" => "info",
            "unpaid" or "pending" or "estimate" or "new" => "warning",
            "in_progress" or "assigned" or "pdi" or "processing" => "primary",
            _ => "secondary",
        };
        var css = $"bg-{tone}-subtle text-{tone}-emphasis border border-{tone}-subtle";
        return new HtmlString($"<span class=\"badge {css}\">{HtmlEncoder.Default.Encode(Labels.Of(labels, code))}</span>");
    }

    // дата для поля datetime-local: по Москве, без секунд
    public static string LocalInput(DateTime moscow) => moscow.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);

    public static DateTime MoscowNow => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Moscow);
}
