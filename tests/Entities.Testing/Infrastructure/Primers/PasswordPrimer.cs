using Microsoft.EntityFrameworkCore.ChangeTracking;
using Regira.Entities.EFcore.Primers.Abstractions;
using Regira.Entities.Models.Abstractions;
using Regira.Utilities;

namespace Entities.Testing.Infrastructure.Primers;

public class PasswordPrimer : EntityPrimerBase<IHasEncryptedPassword>
{
    public override Task PrepareAsync(IHasEncryptedPassword entity, EntityEntry entry, CancellationToken token = default)
    {
        if (!string.IsNullOrWhiteSpace(entity.Password))
        {
            entity.EncryptedPassword = Encrypt(entity.Password);
        }

        return Task.CompletedTask;
    }

    public string Encrypt(string input) => input.Base64Encode();
}
