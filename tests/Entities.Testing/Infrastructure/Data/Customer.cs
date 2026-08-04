using System.ComponentModel.DataAnnotations;
using Regira.Entities.Models.Abstractions;

namespace Entities.Testing.Infrastructure.Data;

public class Customer : IEntityWithSerial
{
    public int Id { get; set; }
    [MaxLength(64)]
    public string? Name { get; set; }
}
