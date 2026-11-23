using ElectroFix.Api.Entities;

using Microsoft.EntityFrameworkCore;

namespace ElectroFix.Api.Data;

// Таблицы создаёт первая миграция из SQL-скриптов (там же процедуры и триггеры),
// а здесь я описываю ту же схему кодом. Имена ключей, индексов и проверок
// совпадают со скриптом, чтобы следующие миграции не пытались их пересоздать.
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<AuditEntry> AuditLog => Set<AuditEntry>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<Part> Parts => Set<Part>();
    public DbSet<Checkin> Checkins => Set<Checkin>();
    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();
    public DbSet<OrderService> OrderServices => Set<OrderService>();
    public DbSet<OrderPart> OrderParts => Set<OrderPart>();
    public DbSet<WarrantyCase> WarrantyCases => Set<WarrantyCase>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<StockMove> StockMoves => Set<StockMove>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    // представления
    public DbSet<WorkOrderView> WorkOrdersView => Set<WorkOrderView>();
    public DbSet<InvoiceView> InvoicesView => Set<InvoiceView>();
    public DbSet<StockBalanceView> StockBalance => Set<StockBalanceView>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<WorkOrderView>().HasNoKey().ToView("v_work_orders");
        b.Entity<InvoiceView>().HasNoKey().ToView("v_invoices");
        b.Entity<StockBalanceView>().HasNoKey().ToView("v_stock_balance");

        // корзина: удалённые записи не видны в обычных запросах.
        // Чтобы их увидеть (корзина, восстановление), пишу IgnoreQueryFilters()
        b.Entity<User>().HasQueryFilter(x => x.DeletedAt == null);
        b.Entity<Client>().HasQueryFilter(x => x.DeletedAt == null);
        b.Entity<Service>().HasQueryFilter(x => x.DeletedAt == null);
        b.Entity<Part>().HasQueryFilter(x => x.DeletedAt == null);

        b.Entity<Role>(e =>
        {
            e.ToTable("roles", t => t.HasCheckConstraint("ck_roles_code", "code ~ '^[a-z_]{3,20}$'"));
            e.HasKey(x => x.RoleId);
            e.Property(x => x.RoleId).UseIdentityAlwaysColumn();
            e.Property(x => x.Code).HasMaxLength(20);
            e.Property(x => x.Name).HasMaxLength(50);
            e.HasIndex(x => x.Code, "uq_roles_code").IsUnique();
            e.HasIndex(x => x.Name, "uq_roles_name").IsUnique();
        });

        b.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasKey(x => x.UserId);
            e.Property(x => x.UserId).UseIdentityAlwaysColumn();
            e.Property(x => x.FullName).HasMaxLength(150);
            e.Property(x => x.Email).HasMaxLength(120);
            e.Property(x => x.Phone).HasMaxLength(20);
            e.Property(x => x.PasswordHash).HasMaxLength(255);
            e.Property(x => x.Settings).HasDefaultValueSql("'{}'");
            e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            e.Property(x => x.BlockReason).HasMaxLength(200);
            e.HasIndex(x => x.Email, "uq_users_email").IsUnique();
            e.HasOne(x => x.Role).WithMany().HasForeignKey(x => x.RoleId)
                .HasConstraintName("fk_users_role").OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<AuditEntry>(e =>
        {
            e.ToTable("audit_log");
            e.HasKey(x => x.AuditId);
            e.Property(x => x.AuditId).UseIdentityAlwaysColumn();
            e.Property(x => x.Action).HasMaxLength(50);
            e.Property(x => x.ObjectTable).HasMaxLength(50);
            e.Property(x => x.IpAddress).HasMaxLength(45);
            e.Property(x => x.UserAgent).HasMaxLength(255);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
                .HasConstraintName("fk_audit_user").OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<Client>(e =>
        {
            e.ToTable("clients");
            e.HasKey(x => x.ClientId);
            e.Property(x => x.ClientId).UseIdentityAlwaysColumn();
            e.Property(x => x.ClientType).HasMaxLength(20);
            e.Property(x => x.DisplayName).HasMaxLength(150);
            e.Property(x => x.Inn).HasMaxLength(12);
            e.Property(x => x.Address).HasMaxLength(255);
            e.Property(x => x.Phone).HasMaxLength(20);
            e.Property(x => x.Email).HasMaxLength(120);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            e.HasIndex(x => x.Inn, "uq_clients_inn").IsUnique();
            e.HasIndex(x => x.UserId, "uq_clients_user").IsUnique();
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId)
                .HasConstraintName("fk_clients_user").OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<Device>(e =>
        {
            e.ToTable("devices", t =>
            {
                t.HasCheckConstraint("ck_devices_year", "manufacture_year IS NULL OR manufacture_year BETWEEN 1950 AND 2100");
                t.HasCheckConstraint("ck_devices_runtime", "runtime_hours >= 0");
            });
            e.HasKey(x => x.DeviceId);
            e.Property(x => x.DeviceId).UseIdentityAlwaysColumn();
            e.Property(x => x.Type).HasMaxLength(50);
            e.Property(x => x.Brand).HasMaxLength(80);
            e.Property(x => x.Model).HasMaxLength(100);
            e.Property(x => x.SerialNumber).HasMaxLength(50);
            e.Property(x => x.InventoryNumber).HasMaxLength(50);
            e.HasIndex(x => x.SerialNumber, "uq_devices_serial").IsUnique();
            e.HasOne(x => x.Client).WithMany(x => x.Devices).HasForeignKey(x => x.ClientId)
                .HasConstraintName("fk_devices_client").OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Service>(e =>
        {
            e.ToTable("services", t =>
            {
                t.HasCheckConstraint("ck_services_hours", "norm_hours > 0 AND norm_hours <= 100");
                t.HasCheckConstraint("ck_services_rate", "rate >= 0");
                t.HasCheckConstraint("ck_services_warranty", "warranty_months BETWEEN 0 AND 36");
            });
            e.HasKey(x => x.ServiceId);
            e.Property(x => x.ServiceId).UseIdentityAlwaysColumn();
            e.Property(x => x.Name).HasMaxLength(150);
            e.Property(x => x.Category).HasMaxLength(50);
            e.Property(x => x.NormHours).HasPrecision(6, 2);
            e.Property(x => x.Rate).HasPrecision(12, 2);
            e.Property(x => x.WarrantyMonths).HasDefaultValue(3);
            e.HasIndex(x => new { x.Name, x.Category }, "uq_services_name").IsUnique();
        });

        b.Entity<Part>(e =>
        {
            e.ToTable("parts", t => t.HasCheckConstraint("ck_parts_min_stock", "min_stock >= 0"));
            e.HasKey(x => x.PartId);
            e.Property(x => x.PartId).UseIdentityAlwaysColumn();
            e.Property(x => x.Article).HasMaxLength(50);
            e.Property(x => x.Name).HasMaxLength(150);
            e.Property(x => x.Category).HasMaxLength(50);
            e.Property(x => x.Unit).HasMaxLength(10).HasDefaultValue("шт");
            e.HasIndex(x => x.Article, "uq_parts_article").IsUnique();
        });

        b.Entity<Checkin>(e =>
        {
            e.ToTable("checkins", t =>
                t.HasCheckConstraint("ck_checkins_runtime", "runtime_at_checkin IS NULL OR runtime_at_checkin >= 0"));
            e.HasKey(x => x.CheckinId);
            e.Property(x => x.CheckinId).UseIdentityAlwaysColumn();
            e.Property(x => x.Branch).HasMaxLength(100);
            e.Property(x => x.ServicesRequested).HasMaxLength(255);
            e.Property(x => x.ConditionState).HasMaxLength(255);
            e.Property(x => x.Priority).HasMaxLength(10).HasDefaultValue("normal");
            e.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("booked");
            e.HasOne(x => x.Device).WithMany().HasForeignKey(x => x.DeviceId)
                .HasConstraintName("fk_checkins_device").OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Receptionist).WithMany().HasForeignKey(x => x.ReceptionistId)
                .HasConstraintName("fk_checkins_receptionist").OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<WorkOrder>(e =>
        {
            e.ToTable("work_orders", t => t.HasCheckConstraint("ck_wo_dates", "closed_at IS NULL OR closed_at >= opened_at"));
            e.HasKey(x => x.WorkOrderId);
            e.Property(x => x.WorkOrderId).UseIdentityAlwaysColumn();
            e.Property(x => x.WorkOrderNumber).HasMaxLength(30);
            e.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("new");
            e.Property(x => x.OpenedAt).HasDefaultValueSql("now()");
            e.HasIndex(x => x.WorkOrderNumber, "uq_wo_number").IsUnique();
            // связь 1:1 с записью на приём
            e.HasOne(x => x.Checkin).WithOne(x => x.WorkOrder).HasForeignKey<WorkOrder>(x => x.CheckinId)
                .HasConstraintName("fk_wo_checkin").OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.CheckinId, "uq_wo_checkin").IsUnique();
            e.HasOne(x => x.Receptionist).WithMany().HasForeignKey(x => x.ReceptionistId)
                .HasConstraintName("fk_wo_receptionist").OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Master).WithMany().HasForeignKey(x => x.MasterId)
                .HasConstraintName("fk_wo_master").OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<OrderService>(e =>
        {
            e.ToTable("order_services", t =>
            {
                t.HasCheckConstraint("ck_os_version", "estimate_version >= 1");
                t.HasCheckConstraint("ck_os_time", "actual_time >= 0 AND actual_time <= 200");
                t.HasCheckConstraint("ck_os_discount", "discount_markup BETWEEN -50 AND 100");
            });
            e.HasKey(x => x.OrderServiceId);
            e.Property(x => x.OrderServiceId).UseIdentityAlwaysColumn();
            e.Property(x => x.EstimateVersion).HasDefaultValue(1);
            e.Property(x => x.ActualTime).HasPrecision(6, 2).HasDefaultValue(0m);
            e.Property(x => x.ApprovalStatus).HasMaxLength(10).HasDefaultValue("draft");
            e.Property(x => x.DiscountMarkup).HasPrecision(6, 2).HasDefaultValue(0m);
            e.Property(x => x.IsEmergency).HasDefaultValue(false);
            e.HasIndex(x => new { x.WorkOrderId, x.ServiceId, x.EstimateVersion }, "uq_os_line").IsUnique();
            e.HasOne(x => x.WorkOrder).WithMany(x => x.Services).HasForeignKey(x => x.WorkOrderId)
                .HasConstraintName("fk_os_work_order").OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Service).WithMany().HasForeignKey(x => x.ServiceId)
                .HasConstraintName("fk_os_service").OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<OrderPart>(e =>
        {
            e.ToTable("order_parts", t =>
            {
                t.HasCheckConstraint("ck_op_quantity", "quantity > 0");
                t.HasCheckConstraint("ck_op_price", "price >= 0");
            });
            e.HasKey(x => x.OrderPartId);
            e.Property(x => x.OrderPartId).UseIdentityAlwaysColumn();
            e.Property(x => x.Quantity).HasPrecision(10, 2);
            e.Property(x => x.Price).HasPrecision(12, 2);
            e.Property(x => x.Batch).HasMaxLength(50);
            e.Property(x => x.ApprovalStatus).HasMaxLength(10).HasDefaultValue("draft");
            e.HasIndex(x => new { x.WorkOrderId, x.PartId, x.Batch }, "uq_op_line").IsUnique();
            e.HasOne(x => x.WorkOrder).WithMany(x => x.Parts).HasForeignKey(x => x.WorkOrderId)
                .HasConstraintName("fk_op_work_order").OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Part).WithMany().HasForeignKey(x => x.PartId)
                .HasConstraintName("fk_op_part").OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<WarrantyCase>(e =>
        {
            e.ToTable("warranty_cases", t => t.HasCheckConstraint("ck_wc_settlement", "settlement_amount >= 0"));
            e.HasKey(x => x.WarrantyCaseId);
            e.Property(x => x.WarrantyCaseId).UseIdentityAlwaysColumn();
            e.Property(x => x.Reason).HasMaxLength(255);
            e.Property(x => x.Decision).HasMaxLength(15).HasDefaultValue("pending");
            e.Property(x => x.SettlementAmount).HasPrecision(12, 2).HasDefaultValue(0m);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            e.HasOne(x => x.WorkOrder).WithMany().HasForeignKey(x => x.WorkOrderId)
                .HasConstraintName("fk_wc_work_order").OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Invoice>(e =>
        {
            e.ToTable("invoices", t => t.HasCheckConstraint("ck_inv_amount", "amount > 0"));
            e.HasKey(x => x.InvoiceId);
            e.Property(x => x.InvoiceId).UseIdentityAlwaysColumn();
            e.Property(x => x.InvoiceNumber).HasMaxLength(30);
            e.Property(x => x.Amount).HasPrecision(12, 2);
            e.Property(x => x.PriceListRef).HasMaxLength(50);
            e.Property(x => x.Status).HasMaxLength(15).HasDefaultValue("unpaid");
            e.Property(x => x.IssuedAt).HasDefaultValueSql("now()");
            e.HasIndex(x => x.InvoiceNumber, "uq_inv_number").IsUnique();
            e.HasOne(x => x.WorkOrder).WithMany().HasForeignKey(x => x.WorkOrderId)
                .HasConstraintName("fk_inv_work_order").OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Payment>(e =>
        {
            e.ToTable("payments", t => t.HasCheckConstraint("ck_pay_amount", "amount > 0"));
            e.HasKey(x => x.PaymentId);
            e.Property(x => x.PaymentId).UseIdentityAlwaysColumn();
            e.Property(x => x.Amount).HasPrecision(12, 2);
            e.Property(x => x.Method).HasMaxLength(15);
            e.Property(x => x.IsRefund).HasDefaultValue(false);
            e.Property(x => x.PaidAt).HasDefaultValueSql("now()");
            e.HasOne(x => x.Invoice).WithMany(x => x.Payments).HasForeignKey(x => x.InvoiceId)
                .HasConstraintName("fk_pay_invoice").OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<RefreshToken>(e =>
        {
            e.ToTable("refresh_tokens");
            e.HasKey(x => x.RefreshTokenId);
            e.Property(x => x.RefreshTokenId).UseIdentityAlwaysColumn();
            e.Property(x => x.TokenHash).HasMaxLength(64);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            e.HasIndex(x => x.TokenHash, "uq_refresh_tokens_hash").IsUnique();
            // удалили пользователя навсегда — его токены уходят вместе с ним
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
                .HasConstraintName("fk_refresh_tokens_user").OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<StockMove>(e =>
        {
            e.ToTable("stock_moves", t => t.HasCheckConstraint("ck_sm_quantity", "quantity > 0"));
            e.HasKey(x => x.StockMoveId);
            e.Property(x => x.StockMoveId).UseIdentityAlwaysColumn();
            e.Property(x => x.MoveType).HasMaxLength(10);
            e.Property(x => x.Quantity).HasPrecision(10, 2);
            e.Property(x => x.Batch).HasMaxLength(50);
            e.Property(x => x.StorageCell).HasMaxLength(20);
            e.Property(x => x.MovedAt).HasDefaultValueSql("now()");
            e.HasOne(x => x.Part).WithMany().HasForeignKey(x => x.PartId)
                .HasConstraintName("fk_sm_part").OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.WorkOrder).WithMany().HasForeignKey(x => x.WorkOrderId)
                .HasConstraintName("fk_sm_work_order").OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Performer).WithMany().HasForeignKey(x => x.PerformedBy)
                .HasConstraintName("fk_sm_performed_by").OnDelete(DeleteBehavior.Restrict);
        });
    }
}
