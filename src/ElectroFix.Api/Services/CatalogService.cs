using System.Linq.Expressions;

using ElectroFix.Api.Common;
using ElectroFix.Api.Data;
using ElectroFix.Api.Entities;
using ElectroFix.Shared;

using Microsoft.EntityFrameworkCore;

namespace ElectroFix.Api.Services;

// Справочники: работы и запчасти
public class CatalogService(AppDbContext db)
{
    private static readonly Expression<Func<Service, ServiceDto>> ServiceToDto = s => new ServiceDto(
        s.ServiceId, s.Name, s.Category, s.NormHours, s.Rate, s.WarrantyMonths, s.DeletedAt);

    private static readonly Expression<Func<Part, PartDto>> PartToDto = p => new PartDto(
        p.PartId, p.Article, p.Name, p.Category, p.Unit, p.MinStock, p.DeletedAt);

    private static readonly Dictionary<string, Expression<Func<Service, object?>>> ServiceSort = new()
    {
        ["id"] = s => s.ServiceId,
        ["name"] = s => s.Name,
        ["category"] = s => s.Category,
        ["rate"] = s => s.Rate,
        ["hours"] = s => s.NormHours,
    };

    private static readonly Dictionary<string, Expression<Func<Part, object?>>> PartSort = new()
    {
        ["id"] = p => p.PartId,
        ["article"] = p => p.Article,
        ["name"] = p => p.Name,
        ["category"] = p => p.Category,
        ["minstock"] = p => p.MinStock,
    };

    // ---------- работы ----------

    public Task<PagedResult<ServiceDto>> ListServicesAsync(CatalogListQuery q)
    {
        var query = db.Services.WithTrash(q.Deleted);
        if (q.Search.Like() is { } like)
        {
            query = query.Where(s => EF.Functions.ILike(s.Name, like));
        }
        if (!string.IsNullOrEmpty(q.Category))
        {
            query = query.Where(s => s.Category == q.Category);
        }
        return query.SortBy(q.Sort, ServiceSort, "category").ToPagedAsync(q, ServiceToDto);
    }

    public async Task<ServiceDto> GetServiceAsync(int id) =>
        await db.Services.IgnoreQueryFilters().Where(s => s.ServiceId == id).Select(ServiceToDto).SingleOrDefaultAsync()
        ?? throw ApiException.NotFound("Работа");

    public async Task<ServiceDto> CreateServiceAsync(ServiceSaveRequest r)
    {
        var s = new Service
        {
            Name = r.Name.Trim(),
            Category = r.Category.Trim(),
            NormHours = r.NormHours,
            Rate = r.Rate,
            WarrantyMonths = r.WarrantyMonths,
        };
        db.Services.Add(s);
        await db.SaveChangesAsync();
        return await GetServiceAsync(s.ServiceId);
    }

    // Сумма заказ-наряда считается по текущим норме и ставке (fn_work_order_total),
    // поэтому у существующей работы их менять нельзя: иначе «поедут» старые счета.
    // Новая цена — это новая позиция справочника
    public async Task<ServiceDto> UpdateServiceAsync(int id, ServiceSaveRequest r)
    {
        var s = await db.Services.SingleOrDefaultAsync(x => x.ServiceId == id) ?? throw ApiException.NotFound("Работа");
        if (s.NormHours != r.NormHours)
        {
            throw ApiException.Field(nameof(r.NormHours), "Норму времени у существующей работы менять нельзя: добавьте новую работу");
        }
        if (s.Rate != r.Rate)
        {
            throw ApiException.Field(nameof(r.Rate), "Ставку у существующей работы менять нельзя: добавьте новую работу");
        }
        s.Name = r.Name.Trim();
        s.Category = r.Category.Trim();
        s.WarrantyMonths = r.WarrantyMonths;
        await db.SaveChangesAsync();
        return await GetServiceAsync(id);
    }

    public Task TrashServiceAsync(int id) => db.MoveToTrashAsync<Service>(s => s.ServiceId == id, "Работа");
    public Task RestoreServiceAsync(int id) => db.RestoreAsync<Service>(s => s.ServiceId == id, "Работа");
    public Task DeleteServiceForeverAsync(int id) => db.DeleteForeverAsync<Service>(s => s.ServiceId == id, "Работа");

    // ---------- запчасти ----------

    public Task<PagedResult<PartDto>> ListPartsAsync(CatalogListQuery q)
    {
        var query = db.Parts.WithTrash(q.Deleted);
        if (q.Search.Like() is { } like)
        {
            query = query.Where(p => EF.Functions.ILike(p.Name, like) || EF.Functions.ILike(p.Article, like));
        }
        if (!string.IsNullOrEmpty(q.Category))
        {
            query = query.Where(p => p.Category == q.Category);
        }
        return query.SortBy(q.Sort, PartSort, "name").ToPagedAsync(q, PartToDto);
    }

    public async Task<PartDto> GetPartAsync(int id) =>
        await db.Parts.IgnoreQueryFilters().Where(p => p.PartId == id).Select(PartToDto).SingleOrDefaultAsync()
        ?? throw ApiException.NotFound("Запчасть");

    public async Task<PartDto> CreatePartAsync(PartSaveRequest r)
    {
        var p = new Part();
        Fill(p, r);
        db.Parts.Add(p);
        await db.SaveChangesAsync();
        return await GetPartAsync(p.PartId);
    }

    public async Task<PartDto> UpdatePartAsync(int id, PartSaveRequest r)
    {
        var p = await db.Parts.SingleOrDefaultAsync(x => x.PartId == id) ?? throw ApiException.NotFound("Запчасть");
        // по артикулу деталь ищут на складе, поэтому, если по ней уже были движения, артикул не меняю
        if (p.Article != r.Article && await db.StockMoves.AnyAsync(m => m.PartId == id))
        {
            throw ApiException.Field(nameof(r.Article), "По запчасти уже есть движения на складе, артикул менять нельзя");
        }
        Fill(p, r);
        await db.SaveChangesAsync();
        return await GetPartAsync(id);
    }

    public Task TrashPartAsync(int id) => db.MoveToTrashAsync<Part>(p => p.PartId == id, "Запчасть");
    public Task RestorePartAsync(int id) => db.RestoreAsync<Part>(p => p.PartId == id, "Запчасть");
    public Task DeletePartForeverAsync(int id) => db.DeleteForeverAsync<Part>(p => p.PartId == id, "Запчасть");

    private static void Fill(Part p, PartSaveRequest r)
    {
        p.Article = r.Article.Trim();
        p.Name = r.Name.Trim();
        p.Category = r.Category.Trim();
        p.Unit = r.Unit;
        p.MinStock = r.MinStock;
    }
}
