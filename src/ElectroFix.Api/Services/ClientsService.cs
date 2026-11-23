using System.Linq.Expressions;

using ElectroFix.Api.Common;
using ElectroFix.Api.Data;
using ElectroFix.Api.Entities;
using ElectroFix.Shared;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace ElectroFix.Api.Services;

// Клиенты. Читаю через EF, а добавляю, меняю и удаляю навсегда процедурами
// sp_client_add / sp_client_update / sp_client_delete из 02_logic.sql
public class ClientsService(AppDbContext db)
{
    private static readonly Expression<Func<Client, ClientDto>> ToDto = c => new ClientDto(
        c.ClientId, c.ClientType, c.DisplayName, c.Inn, c.Address, c.Phone, c.Email,
        c.CreatedAt, c.Devices.Count, c.DeletedAt);

    private static readonly Dictionary<string, Expression<Func<Client, object?>>> SortFields = new()
    {
        ["id"] = c => c.ClientId,
        ["name"] = c => c.DisplayName,
        ["type"] = c => c.ClientType,
        ["created"] = c => c.CreatedAt,
    };

    public Task<PagedResult<ClientDto>> ListAsync(ClientListQuery q)
    {
        var query = db.Clients.WithTrash(q.Deleted);
        if (q.Search.Like() is { } like)
        {
            query = query.Where(c => EF.Functions.ILike(c.DisplayName, like)
                                     || EF.Functions.ILike(c.Phone, like)
                                     || (c.Inn != null && EF.Functions.ILike(c.Inn, like))
                                     || (c.Email != null && EF.Functions.ILike(c.Email, like)));
        }
        if (!string.IsNullOrEmpty(q.ClientType))
        {
            query = query.Where(c => c.ClientType == q.ClientType);
        }
        return query.SortBy(q.Sort, SortFields, "name").ToPagedAsync(q, ToDto);
    }

    public async Task<ClientDto> GetAsync(int id) =>
        await db.Clients.IgnoreQueryFilters().Where(c => c.ClientId == id).Select(ToDto).SingleOrDefaultAsync()
        ?? throw ApiException.NotFound("Клиент");

    public async Task<ClientDto> CreateAsync(ClientSaveRequest r)
    {
        CheckInn(r.ClientType, r.Inn);
        var id = await db.CallAsync<int>(
            "CALL sp_client_add(@type, @name, @phone, @inn, @address, @email, NULL)",
            ("type", r.ClientType), ("name", r.DisplayName), ("phone", r.Phone),
            ("inn", r.Inn), ("address", r.Address), ("email", r.Email));
        return await GetAsync(id);
    }

    // у юрлица и УК ИНН обязателен и из 10 цифр, у физлица — 12 цифр или пусто
    public static void CheckInn(string clientType, string? inn)
    {
        if (clientType != "individual" && inn?.Length != 10)
        {
            throw ApiException.Field(nameof(ClientSaveRequest.Inn), "Для юридического лица и УК нужен ИНН из 10 цифр");
        }
        if (clientType == "individual" && inn != null && inn.Length != 12)
        {
            throw ApiException.Field(nameof(ClientSaveRequest.Inn), "ИНН физического лица — 12 цифр");
        }
    }

    // тип и ИНН после создания не меняются (так устроена процедура sp_client_update)
    public async Task<ClientDto> UpdateAsync(int id, ClientSaveRequest r)
    {
        var realName = await db.Clients.Where(c => c.ClientId == id).Select(c => c.DisplayName).SingleOrDefaultAsync()
                       ?? throw ApiException.NotFound("Клиент");
        // админ видит ФИО клиента скрытым; если он его не менял, отправляю в процедуру настоящее
        var name = r.DisplayName.Trim() == PersonalData.MaskName(realName) ? realName : r.DisplayName;
        await db.CallAsync<object>("CALL sp_client_update(@id, @name, @phone, @address, @email)",
            ("id", id), ("name", name), ("phone", r.Phone), ("address", r.Address), ("email", r.Email));
        return await GetAsync(id);
    }

    public Task TrashAsync(int id) => db.MoveToTrashAsync<Client>(c => c.ClientId == id, "Клиент");

    public Task RestoreAsync(int id) => db.RestoreAsync<Client>(c => c.ClientId == id, "Клиент");

    // процедура сама не даст удалить клиента с устройствами (RAISE EXCEPTION → 409)
    public async Task DeleteForeverAsync(int id)
    {
        if (!await db.Clients.IgnoreQueryFilters().AnyAsync(c => c.ClientId == id))
        {
            throw ApiException.NotFound("Клиент");
        }
        await db.CallAsync<object>("CALL sp_client_delete(@id)", ("id", id));
    }
}
