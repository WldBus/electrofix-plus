using ElectroFix.Admin.Infrastructure;
using ElectroFix.Shared;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ElectroFix.Admin.Controllers;

// Конкретные CRUD-разделы панели: только адрес в Api и как запись превращается в форму

public class ClientsController(ApiClient api) : CrudController<ClientDto, ClientSaveRequest, ClientListQuery>(api)
{
    protected override string ApiPath => "/api/clients";

    protected override ClientSaveRequest ToForm(ClientDto c) => new()
    {
        ClientType = c.ClientType,
        DisplayName = c.DisplayName,
        Inn = c.Inn,
        Address = c.Address,
        Phone = c.Phone,
        Email = c.Email,
    };
}

public class ServicesController(ApiClient api) : CrudController<ServiceDto, ServiceSaveRequest, CatalogListQuery>(api)
{
    protected override string ApiPath => "/api/services";

    protected override ServiceSaveRequest ToForm(ServiceDto s) => new()
    {
        Name = s.Name,
        Category = s.Category,
        NormHours = s.NormHours,
        Rate = s.Rate,
        WarrantyMonths = s.WarrantyMonths,
    };
}

public class PartsController(ApiClient api) : CrudController<PartDto, PartSaveRequest, CatalogListQuery>(api)
{
    protected override string ApiPath => "/api/parts";

    protected override PartSaveRequest ToForm(PartDto p) => new()
    {
        Article = p.Article,
        Name = p.Name,
        Category = p.Category,
        Unit = p.Unit,
        MinStock = p.MinStock,
    };
}

public class UsersController(ApiClient api, StatsClient stats) : CrudController<UserDto, UserSaveRequest, UserListQuery>(api)
{
    protected override string ApiPath => "/api/users";

    protected override UserSaveRequest ToForm(UserDto u) => new()
    {
        RoleId = u.RoleId,
        FullName = u.FullName,
        Email = u.Email,
        Phone = u.Phone,
    };

    // роли нужны и в фильтре списка, и в форме
    protected override async Task PrepareFormAsync()
    {
        var roles = await Api.GetAsync<List<RoleDto>>("/api/roles");
        ViewBag.RoleStats = roles;
        ViewBag.Roles = new SelectList(roles, nameof(RoleDto.RoleId), nameof(RoleDto.Name));
    }

    // над списком: всего / активных / заблокированных, по ролям и регистрации по месяцам
    protected override async Task PrepareListAsync()
    {
        await PrepareFormAsync();
        ViewBag.Stats = await stats.DashboardAsync(12);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Block(int id, string reason, string? returnUrl)
    {
        await RunAndNotifyAsync(() => Api.PostAsync($"/api/users/{id}/block", new BlockRequest { Reason = reason ?? "" }),
            "Пользователь заблокирован, его сессии завершены");
        return BackTo(returnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unblock(int id, string? returnUrl)
    {
        await RunAndNotifyAsync(() => Api.PostAsync($"/api/users/{id}/unblock"), "Пользователь разблокирован");
        return BackTo(returnUrl);
    }
}
