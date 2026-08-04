using System.ComponentModel.DataAnnotations;
using Regira.Entities.Models.Abstractions;

namespace Entities.Testing.Infrastructure.Data;

public class User : IEntity<string>, IHasEncryptedPassword
{
    [MaxLength(32)]
    public string Id { get; set; } = null!;
    [MaxLength(64)]
    public string? Username { get; set; }
    [MaxLength(256)]
    public string? Password { get; set; }
    [MaxLength(512)]
    public string? EncryptedPassword { get; set; }
}
