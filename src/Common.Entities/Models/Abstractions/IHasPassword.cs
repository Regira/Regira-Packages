namespace Regira.Entities.Models.Abstractions;

public interface IHasPassword
{
    string? Password { get; }
}
public interface IHasEncryptedPassword : IHasPassword
{
    string? EncryptedPassword { get; set; }
}