namespace ElectroFix.Api.Entities;

// Refresh-токен. В базе храню не сам токен, а его SHA-256:
// если таблицу украдут, войти по этим строкам не получится
public class RefreshToken
{
    public int RefreshTokenId { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public string TokenHash { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
