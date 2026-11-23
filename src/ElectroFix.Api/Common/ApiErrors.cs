using System.Text.Json;

using ElectroFix.Shared;

using Microsoft.AspNetCore.Mvc;

using Npgsql;

namespace ElectroFix.Api.Common;

// Свои исключения: сервис бросает, а middleware превращает в нужный код ответа
public class ApiException(int statusCode, string message, Dictionary<string, string>? errors = null)
    : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public Dictionary<string, string>? Errors { get; } = errors;

    public static ApiException NotFound(string what) => new(404, $"Не найдено: {what.ToLowerInvariant()}");
    public static ApiException Conflict(string message) => new(409, message);
    public static ApiException BadRequest(string message) => new(400, message);

    // ошибка конкретного поля → 422
    public static ApiException Field(string field, string message) =>
        new(422, "Проверьте поля формы", new() { [JsonNamingPolicy.CamelCase.ConvertName(field)] = message });
}

public class ErrorMiddleware(RequestDelegate next, ILogger<ErrorMiddleware> logger)
{
    // понятные тексты для уникальных ограничений из 01_tables.sql
    private static readonly Dictionary<string, string> UniqueMessages = new()
    {
        ["uq_clients_inn"] = "Клиент с таким ИНН уже есть (возможно, в корзине)",
        ["uq_users_email"] = "Пользователь с таким email уже есть (возможно, в корзине)",
        ["uq_parts_article"] = "Запчасть с таким артикулом уже есть (возможно, в корзине)",
        ["uq_services_name"] = "Такая работа в этой категории уже есть (возможно, в корзине)",
        ["uq_devices_serial"] = "Устройство с таким серийным номером уже есть",
        ["uq_os_line"] = "Эта работа уже есть в смете",
        ["uq_op_line"] = "Эта запчасть с этой партией уже есть в смете",
        ["ix_clients_user_id"] = "Эта учётная запись уже привязана к другому клиенту",
    };

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ApiException ex)
        {
            await WriteAsync(context, ex.StatusCode, new ErrorResponse(ex.Message, ex.Errors));
        }
        catch (Exception ex) when (FindPostgres(ex) is { } pg)
        {
            var (code, message) = Describe(pg);
            if (code == 500)
            {
                logger.LogError(ex, "Ошибка базы данных");
            }
            await WriteAsync(context, code, new ErrorResponse(message));
        }
        catch (Exception ex)
        {
            // стектрейс только в лог, наружу общий текст
            logger.LogError(ex, "Необработанная ошибка");
            await WriteAsync(context, 500, new ErrorResponse("Внутренняя ошибка сервера. Попробуйте позже"));
        }
    }

    private static (int Code, string Message) Describe(PostgresException pg) => pg.SqlState switch
    {
        // RAISE EXCEPTION из моих процедур и триггеров — это нарушение бизнес-правила, текст уже по-русски
        PostgresErrorCodes.RaiseException => (409, pg.MessageText),
        PostgresErrorCodes.UniqueViolation => (409,
            UniqueMessages.GetValueOrDefault(pg.ConstraintName ?? "", "Такая запись уже есть")),
        PostgresErrorCodes.ForeignKeyViolation => (409, "На запись ссылаются другие данные, удалить её нельзя"),
        PostgresErrorCodes.CheckViolation => (422, $"Значение не прошло проверку в базе ({pg.ConstraintName})"),
        _ => (500, "Ошибка базы данных"),
    };

    private static PostgresException? FindPostgres(Exception ex)
    {
        // EF заворачивает ошибку PostgreSQL в DbUpdateException, поэтому ищу вглубь
        for (var e = ex; e != null; e = e.InnerException)
        {
            if (e is PostgresException pg)
            {
                return pg;
            }
        }
        return null;
    }

    private static async Task WriteAsync(HttpContext context, int code, ErrorResponse body)
    {
        context.Response.Clear();
        context.Response.StatusCode = code;
        await context.Response.WriteAsJsonAsync(body);
    }
}

public static class ValidationResponse
{
    // ошибки DataAnnotations → 422 в моём формате; битый JSON → 400
    public static IActionResult Create(ActionContext context)
    {
        var state = context.ModelState.Where(x => x.Value?.Errors.Count > 0).ToList();
        if (state.Any(x => x.Key.StartsWith('$') || x.Key == "request"))
        {
            return new BadRequestObjectResult(new ErrorResponse("Некорректный запрос: проверьте JSON"));
        }

        var errors = state.ToDictionary(
            x => JsonNamingPolicy.CamelCase.ConvertName(x.Key),
            x => x.Value!.Errors[0].ErrorMessage);
        return new UnprocessableEntityObjectResult(new ErrorResponse("Проверьте поля формы", errors));
    }
}
