using System.Linq.Expressions;

using ElectroFix.Api.Common;
using ElectroFix.Api.Data;
using ElectroFix.Api.Entities;
using ElectroFix.Shared;

using Microsoft.EntityFrameworkCore;

namespace ElectroFix.Api.Services;

// Счета, платежи, склад и журнал аудита — только чтение.
// Платежи и движения склада в базе вообще неизменяемые (триггеры *_immutable)
public class FinanceService(AppDbContext db)
{
    public static readonly Expression<Func<InvoiceView, InvoiceDto>> InvoiceToDto = i => new InvoiceDto(
        i.InvoiceId, i.InvoiceNumber, i.WorkOrderNumber, i.Client, i.Amount, i.Paid, i.Debt, i.Status, i.IssuedAt);

    private static readonly Dictionary<string, Expression<Func<InvoiceView, object?>>> InvoiceSort = new()
    {
        ["id"] = i => i.InvoiceId,
        ["number"] = i => i.InvoiceNumber,
        ["issued"] = i => i.IssuedAt,
        ["amount"] = i => i.Amount,
        ["debt"] = i => i.Debt,
    };

    private static readonly Dictionary<string, Expression<Func<Payment, object?>>> PaymentSort = new()
    {
        ["id"] = p => p.PaymentId,
        ["paid"] = p => p.PaidAt,
        ["amount"] = p => p.Amount,
    };

    private static readonly Dictionary<string, Expression<Func<StockMove, object?>>> MoveSort = new()
    {
        ["id"] = m => m.StockMoveId,
        ["moved"] = m => m.MovedAt,
        ["type"] = m => m.MoveType,
    };

    private static readonly Dictionary<string, Expression<Func<StockBalanceView, object?>>> BalanceSort = new()
    {
        ["article"] = b => b.Article,
        ["name"] = b => b.Name,
        ["available"] = b => b.Available,
    };

    private static readonly Dictionary<string, Expression<Func<AuditEntry, object?>>> AuditSort = new()
    {
        ["id"] = a => a.AuditId,
        ["created"] = a => a.CreatedAt,
        ["action"] = a => a.Action,
        ["table"] = a => a.ObjectTable,
        ["ip"] = a => a.IpAddress,
    };

    public Task<PagedResult<InvoiceDto>> ListInvoicesAsync(StatusListQuery q)
    {
        var query = db.InvoicesView.AsQueryable();
        if (q.Search.Like() is { } like)
        {
            query = query.Where(i => EF.Functions.ILike(i.InvoiceNumber, like) || EF.Functions.ILike(i.Client, like)
                                     || EF.Functions.ILike(i.WorkOrderNumber, like));
        }
        if (!string.IsNullOrEmpty(q.Status))
        {
            query = query.Where(i => i.Status == q.Status);
        }
        return query.SortBy(q.Sort, InvoiceSort, "issued,desc").ToPagedAsync(q, InvoiceToDto);
    }

    public async Task<InvoiceDto> GetInvoiceAsync(int id) =>
        await db.InvoicesView.Where(i => i.InvoiceId == id).Select(InvoiceToDto).SingleOrDefaultAsync()
        ?? throw ApiException.NotFound("Счёт");

    public Task<PagedResult<PaymentDto>> ListPaymentsAsync(ListQuery q, int? invoiceId)
    {
        var query = db.Payments.AsQueryable();
        if (q.Search.Like() is { } like)
        {
            query = query.Where(p => EF.Functions.ILike(p.Invoice!.InvoiceNumber, like));
        }
        if (invoiceId != null)
        {
            query = query.Where(p => p.InvoiceId == invoiceId);
        }
        return query.SortBy(q.Sort, PaymentSort, "paid,desc").ToPagedAsync(q, p => new PaymentDto(
            p.PaymentId, p.InvoiceId, p.Invoice!.InvoiceNumber, p.Amount, p.Method, p.IsRefund, p.PaidAt));
    }

    public Task<PagedResult<StockMoveDto>> ListStockMovesAsync(StockMoveListQuery q)
    {
        var query = db.StockMoves.IgnoreQueryFilters();
        if (q.Search.Like() is { } like)
        {
            query = query.Where(m => EF.Functions.ILike(m.Part!.Name, like) || EF.Functions.ILike(m.Part.Article, like)
                                     || EF.Functions.ILike(m.Batch, like));
        }
        if (!string.IsNullOrEmpty(q.MoveType))
        {
            query = query.Where(m => m.MoveType == q.MoveType);
        }
        if (q.PartId != null)
        {
            query = query.Where(m => m.PartId == q.PartId);
        }
        return query.SortBy(q.Sort, MoveSort, "moved,desc").ToPagedAsync(q, m => new StockMoveDto(
            m.StockMoveId, m.PartId, m.Part!.Article, m.Part.Name, m.WorkOrderId,
            m.WorkOrder != null ? m.WorkOrder.WorkOrderNumber : null,
            m.MoveType, m.Quantity, m.Batch, m.StorageCell, m.Performer!.FullName, m.MovedAt));
    }

    // остатки из представления v_stock_balance
    public Task<PagedResult<StockBalanceDto>> ListStockAsync(ListQuery q)
    {
        var query = db.StockBalance.AsQueryable();
        if (q.Search.Like() is { } like)
        {
            // ищу и по партии с ячейкой: закупщику так проще найти, что куда положил
            query = query.Where(b => EF.Functions.ILike(b.Name, like) || EF.Functions.ILike(b.Article, like)
                                     || EF.Functions.ILike(b.Batch, like) || EF.Functions.ILike(b.StorageCell!, like));
        }
        return query.SortBy(q.Sort, BalanceSort, "name").ToPagedAsync(q, b => new StockBalanceDto(
            b.PartId, b.Article, b.Name, b.Batch, b.StorageCell, b.OnHand, b.Reserved, b.Available));
    }

    // журнал: фильтры по датам, пользователю, действию, таблице и IP
    public Task<PagedResult<AuditEntryDto>> ListAuditAsync(AuditListQuery q)
    {
        var query = db.AuditLog.IgnoreQueryFilters();
        if (q.Search.Like() is { } like)
        {
            query = query.Where(a => (a.BeforeData != null && EF.Functions.ILike(a.BeforeData, like))
                                     || (a.AfterData != null && EF.Functions.ILike(a.AfterData, like)));
        }
        if (q.User.Like() is { } user)
        {
            query = query.Where(a => a.User != null
                                     && (EF.Functions.ILike(a.User.Email, user) || EF.Functions.ILike(a.User.FullName, user)));
        }
        if (!string.IsNullOrEmpty(q.Table))
        {
            query = query.Where(a => a.ObjectTable == q.Table);
        }
        if (!string.IsNullOrEmpty(q.Action))
        {
            query = query.Where(a => a.Action == q.Action);
        }
        if (q.Security)
        {
            var actions = Labels.SecurityActions.ToList();
            query = query.Where(a => actions.Contains(a.Action));
        }
        if (q.Ip.Like() is { } ip)
        {
            query = query.Where(a => a.IpAddress != null && EF.Functions.ILike(a.IpAddress, ip));
        }
        // даты из фильтра — по Москве, в базе время в UTC
        if (q.From is { } from)
        {
            var fromUtc = MoscowToUtc(from);
            query = query.Where(a => a.CreatedAt >= fromUtc);
        }
        if (q.To is { } to)
        {
            var toUtc = MoscowToUtc(to.AddDays(1));
            query = query.Where(a => a.CreatedAt < toUtc);
        }
        return query.SortBy(q.Sort, AuditSort, "id,desc").ToPagedAsync(q, a => new AuditEntryDto(
            a.AuditId, a.UserId, a.User != null ? a.User.FullName : null, a.User != null ? a.User.Email : null,
            a.Action, a.ObjectTable, a.ObjectId, a.BeforeData, a.AfterData, a.IpAddress, a.UserAgent, a.CreatedAt));
    }

    private static DateTime MoscowToUtc(DateOnly date) =>
        DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue).AddHours(-3), DateTimeKind.Utc);
}
