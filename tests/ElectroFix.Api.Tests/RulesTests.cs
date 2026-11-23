using System.Linq.Expressions;
using System.Text.Json;

using ElectroFix.Api.Common;
using ElectroFix.Api.Services;
using ElectroFix.Shared;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using Npgsql;

namespace ElectroFix.Api.Tests;

// Модульные тесты: бизнес-правила без базы и без Docker, поэтому идут за доли секунды.
// Всё, что зависит от триггеров и процедур, проверяю в интеграционных тестах
public class RulesTests
{
    // ---------- переходы статусов заказ-наряда ----------

    [Fact]
    public void Receptionist_FromNew_CanStartDiagnosticsOrCancel()
    {
        Assert.Equal(["diagnostics", "cancelled"], WorkOrderRules.MovesFor(RoleCodes.Receptionist, "new"));
        // после контроля качества приёмщик либо выдаёт, либо возвращает мастеру
        Assert.Equal(["ready", "in_progress"], WorkOrderRules.MovesFor(RoleCodes.Receptionist, "pdi"));
    }

    [Fact]
    public void Master_OnlyTakesInWorkAndHandsOverToCheck()
    {
        Assert.Equal(["in_progress"], WorkOrderRules.MovesFor(RoleCodes.Master, "assigned"));
        Assert.Equal(["pdi"], WorkOrderRules.MovesFor(RoleCodes.Master, "in_progress"));
        Assert.Empty(WorkOrderRules.MovesFor(RoleCodes.Master, "new"));
        // отменить ремонт мастер не может ни на каком этапе
        Assert.DoesNotContain(WorkOrderRules.MasterMoves.Values, moves => moves.Contains("cancelled"));
    }

    [Theory]
    [InlineData(RoleCodes.Client)]
    [InlineData(RoleCodes.Admin)]
    [InlineData(RoleCodes.Purchaser)]
    public void OtherRoles_CannotMoveStatus(string role)
    {
        foreach (var status in Labels.WorkOrderStatuses.Keys)
        {
            Assert.Empty(WorkOrderRules.MovesFor(role, status));
        }
    }

    [Theory]
    [InlineData("closed")]
    [InlineData("cancelled")]
    public void FinishedOrder_HasNoMoves(string status)
    {
        Assert.Empty(WorkOrderRules.MovesFor(RoleCodes.Receptionist, status));
        Assert.Empty(WorkOrderRules.MovesFor(RoleCodes.Master, status));
    }

    [Theory]
    [InlineData("in_progress", true)]
    [InlineData("pdi", false)]
    [InlineData("ready", false)]
    [InlineData("closed", false)]
    public void Estimate_IsLockedFromQualityCheck(string status, bool editable)
    {
        Assert.Equal(editable, WorkOrderRules.EditableEstimate.Contains(status));
    }

    [Fact]
    public void Rules_UseOnlyRealStatuses()
    {
        // защита от опечатки: в правилах только статусы, которые есть в БД (Labels повторяет CHECK)
        var all = WorkOrderRules.ReceptionistMoves.Concat(WorkOrderRules.MasterMoves)
            .SelectMany(x => x.Value.Append(x.Key))
            .Concat(WorkOrderRules.EditableEstimate);
        Assert.All(all, s => Assert.Contains(s, Labels.WorkOrderStatuses.Keys));
    }

    // ---------- запись на приём ----------

    private static readonly DateTime Now = new(2026, 11, 20, 9, 0, 0, DateTimeKind.Utc); // 12:00 по Москве

    [Fact]
    public void Slot_MoscowTimeIsSavedAsUtc()
    {
        var start = WorkflowService.CheckSlot(new DateTime(2026, 11, 21, 15, 0, 0), Branches.All[0], Now);

        Assert.Equal(new DateTime(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc), start);
    }

    [Fact]
    public void Slot_InThePast_IsFieldError()
    {
        // 11:00 по Москве, а сейчас уже 12:00
        var ex = Assert.Throws<ApiException>(() =>
            WorkflowService.CheckSlot(new DateTime(2026, 11, 20, 11, 0, 0), Branches.All[0], Now));

        Assert.Equal(422, ex.StatusCode);
        Assert.Equal("Это время уже прошло", ex.Errors!["slotStart"]);
    }

    [Fact]
    public void Slot_FurtherThan90Days_IsFieldError()
    {
        var ex = Assert.Throws<ApiException>(() =>
            WorkflowService.CheckSlot(new DateTime(2027, 3, 1, 12, 0, 0), Branches.All[0], Now));

        Assert.True(ex.Errors!.ContainsKey("slotStart"));
    }

    [Fact]
    public void Slot_UnknownBranch_IsFieldError()
    {
        var ex = Assert.Throws<ApiException>(() =>
            WorkflowService.CheckSlot(new DateTime(2026, 11, 21, 15, 0, 0), "Казань, Баумана", Now));

        Assert.True(ex.Errors!.ContainsKey("branch"));
    }

    // ---------- ИНН клиента ----------

    [Theory]
    [InlineData("individual", null)]
    [InlineData("individual", "771234567890")]
    [InlineData("legal_entity", "7701234567")]
    [InlineData("management_company", "7723456781")]
    public void Inn_CorrectForType_Passes(string type, string? inn)
    {
        ClientsService.CheckInn(type, inn);
    }

    [Theory]
    [InlineData("legal_entity", null)]
    [InlineData("legal_entity", "771234567890")]
    [InlineData("individual", "7701234567")]
    public void Inn_WrongForType_IsFieldError(string type, string? inn)
    {
        var ex = Assert.Throws<ApiException>(() => ClientsService.CheckInn(type, inn));

        Assert.Equal(422, ex.StatusCode);
        Assert.True(ex.Errors!.ContainsKey("inn"));
    }

    // ---------- скрытие ФИО для админа ----------

    [Fact]
    public void MaskName_HidesPersonButNotCompany()
    {
        Assert.Equal("И****в А. А.", PersonalData.MaskName("Иванов Алексей Андреевич"));
        Assert.Equal("ООО «ТехноСтрой»", PersonalData.MaskName("ООО «ТехноСтрой»"));
        Assert.Equal("{\"full_name\": \"П*****а М. С.\"}", PersonalData.MaskJson("{\"full_name\": \"Петрова Мария Сергеевна\"}"));
    }

    // ---------- сортировка и поиск в списках ----------

    private record Row(int Id, string Name);

    private static readonly Dictionary<string, Expression<Func<Row, object?>>> SortFields = new()
    {
        ["id"] = r => r.Id,
        ["name"] = r => r.Name,
    };

    private static readonly IQueryable<Row> Rows = new[] { new Row(2, "Б"), new Row(1, "В"), new Row(3, "А") }.AsQueryable();

    [Fact]
    public void SortBy_AscDescAndDefault()
    {
        Assert.Equal([1, 2, 3], Rows.SortBy("id,asc", SortFields, "name").Select(r => r.Id));
        Assert.Equal([3, 2, 1], Rows.SortBy("ID,desc", SortFields, "name").Select(r => r.Id));
        // в адресе сортировки нет — беру по умолчанию
        Assert.Equal(["А", "Б", "В"], Rows.SortBy(null, SortFields, "name").Select(r => r.Name));
    }

    [Fact]
    public void SortBy_UnknownField_Is400WithAllowedList()
    {
        var ex = Assert.Throws<ApiException>(() => Rows.SortBy("password", SortFields, "name"));

        Assert.Equal(400, ex.StatusCode);
        Assert.Contains("id, name", ex.Message);
    }

    [Fact]
    public void Like_TrimsAndSkipsEmpty()
    {
        Assert.Equal("%иванов%", "  иванов ".Like());
        Assert.Null("   ".Like());
        Assert.Null(((string?)null).Like());
    }

    // ---------- единый формат ошибок ----------

    // прогоняю middleware на «пустом» запросе, внутри которого вылетает ошибка
    private static async Task<(int Code, ErrorResponse Body)> RunMiddlewareAsync(Exception error)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new ErrorMiddleware(_ => throw error, NullLogger<ErrorMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        var body = await JsonSerializer.DeserializeAsync<ErrorResponse>(context.Response.Body, JsonSerializerOptions.Web);
        return (context.Response.StatusCode, body!);
    }

    private static PostgresException Pg(string sqlState, string message = "ошибка", string? constraint = null) =>
        new(message, "ERROR", "ERROR", sqlState, constraintName: constraint);

    [Fact]
    public async Task Middleware_UniqueViolationInsideEf_Is409WithClearText()
    {
        // EF заворачивает ошибку PostgreSQL в DbUpdateException — middleware должен найти её внутри
        var error = new DbUpdateException("save failed", Pg(PostgresErrorCodes.UniqueViolation, constraint: "uq_clients_inn"));

        var (code, body) = await RunMiddlewareAsync(error);

        Assert.Equal(409, code);
        Assert.Equal("Клиент с таким ИНН уже есть (возможно, в корзине)", body.Message);
    }

    [Fact]
    public async Task Middleware_RaiseFromProcedure_Is409WithTextFromDb()
    {
        var (code, body) = await RunMiddlewareAsync(Pg(PostgresErrorCodes.RaiseException, "Сумма оплаты больше долга"));

        Assert.Equal(409, code);
        Assert.Equal("Сумма оплаты больше долга", body.Message);
    }

    [Fact]
    public async Task Middleware_CheckViolation_Is422()
    {
        var (code, _) = await RunMiddlewareAsync(Pg(PostgresErrorCodes.CheckViolation, constraint: "ck_parts_price"));

        Assert.Equal(422, code);
    }

    [Fact]
    public async Task Middleware_UnknownError_Is500WithoutDetails()
    {
        var (code, body) = await RunMiddlewareAsync(new InvalidOperationException("строка подключения: Password=secret"));

        Assert.Equal(500, code);
        // наружу не должно утечь ничего из текста исключения
        Assert.DoesNotContain("secret", body.Message);
    }

    [Fact]
    public void ValidationErrors_Are422WithCamelCaseFields_BrokenJsonIs400()
    {
        var state = new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary();
        state.AddModelError("DisplayName", "Укажите имя");
        var result = ValidationResponse.Create(new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor(), state));

        var unprocessable = Assert.IsType<UnprocessableEntityObjectResult>(result);
        Assert.Equal("Укажите имя", ((ErrorResponse)unprocessable.Value!).Errors!["displayName"]);

        var broken = new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary();
        broken.AddModelError("$.phone", "JSON не разобрать");
        Assert.IsType<BadRequestObjectResult>(
            ValidationResponse.Create(new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor(), broken)));
    }
}
