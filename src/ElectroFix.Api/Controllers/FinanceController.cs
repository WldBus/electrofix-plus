using ElectroFix.Api.Common;
using ElectroFix.Api.Services;
using ElectroFix.Shared;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElectroFix.Api.Controllers;

// Счета, платежи, склад и журнал аудита — только чтение
[ApiController]
[Authorize(Roles = Roles.Staff)]
[Route("api")]
public class FinanceController(FinanceService finance) : ControllerBase
{
    [HttpGet("invoices")]
    public Task<PagedResult<InvoiceDto>> Invoices([FromQuery] StatusListQuery q) => finance.ListInvoicesAsync(q);

    [HttpGet("invoices/{id:int}")]
    public Task<InvoiceDto> Invoice(int id) => finance.GetInvoiceAsync(id);

    [HttpGet("payments")]
    public Task<PagedResult<PaymentDto>> Payments([FromQuery] ListQuery q, int? invoiceId) =>
        finance.ListPaymentsAsync(q, invoiceId);

    [HttpGet("stock-moves")]
    public Task<PagedResult<StockMoveDto>> StockMoves([FromQuery] StockMoveListQuery q) => finance.ListStockMovesAsync(q);

    [HttpGet("stock")]
    public Task<PagedResult<StockBalanceDto>> Stock([FromQuery] ListQuery q) => finance.ListStockAsync(q);

    // журнал аудита — только админу
    [Authorize(Roles = Roles.Admin)]
    [HttpGet("audit")]
    public Task<PagedResult<AuditEntryDto>> Audit([FromQuery] AuditListQuery q) => finance.ListAuditAsync(q);
}
