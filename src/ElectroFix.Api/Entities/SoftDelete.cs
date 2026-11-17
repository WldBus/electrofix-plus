namespace ElectroFix.Api.Entities;

// «корзина»: запись не удаляется, а получает дату удаления.
// Глобальный фильтр в AppDbContext прячет такие записи из обычных запросов
public interface ISoftDeletable
{
    DateTime? DeletedAt { get; set; }
}
