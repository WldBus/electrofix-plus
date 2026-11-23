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

    // цветная метка статуса: словарь подписей передаю, потому что «cancelled» есть и у записи, и у заказа
    public static IHtmlContent Status(Dictionary<string, string> labels, string? code)
    {
        if (code == null)
        {
            return new HtmlString("–");
        }
        var css = code switch
        {
            "closed" or "paid" or "approved" or "checked_in" => "bg-success",
            "cancelled" or "rejected" or "no_show" => "bg-secondary",
            "ready" or "partially_paid" or "confirmed" => "bg-info text-dark",
            "unpaid" or "pending" or "estimate" => "bg-warning text-dark",
            "in_progress" or "assigned" or "pdi" => "bg-primary",
            _ => "bg-light text-dark border",
        };
        return new HtmlString($"<span class=\"badge {css}\">{HtmlEncoder.Default.Encode(Labels.Of(labels, code))}</span>");
    }

    // дата для поля datetime-local: по Москве, без секунд
    public static string LocalInput(DateTime moscow) => moscow.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);

    public static DateTime MoscowNow => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Moscow);
}
