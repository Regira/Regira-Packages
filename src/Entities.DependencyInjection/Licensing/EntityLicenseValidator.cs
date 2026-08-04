using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.Attachments.Abstractions;
using Regira.Licensing.Services;
using System.Runtime.CompilerServices;
using Regira.Licensing.Models;
using Regira.Licensing.Utilities;

namespace Regira.Entities.DependencyInjection.Licensing;

internal class EntityLicenseValidator
{
    // One validator per IServiceCollection instance; weak-keyed so it doesn't prevent GC.
    private static readonly ConditionalWeakTable<IServiceCollection, EntityLicenseValidator> Validators = new();

    internal static EntityLicenseValidator For(IServiceCollection services)
        => Validators.GetOrCreateValue(services);

    // DI registration is startup single-threaded; no locking needed.
    // A slot is consumed by a distinct entity *type*, not by a registration call: the same type may be
    // registered more than once internally (e.g. an attachment entity is registered both as a plain service
    // and with a search object) and still counts once. The shared Attachment base table is framework
    // infrastructure (registered once via WithAttachments and reused by every owner) and never counts.
    private readonly List<Type> _simpleEntities = [];
    private readonly List<Type> _complexEntities = [];
    private bool _validated;
    private string? _tier;
    private int _simpleLimit;
    private int _complexLimit;

    internal int SimpleCount => _simpleEntities.Count;
    internal int ComplexCount => _complexEntities.Count;
    internal string SimpleNames => FormatNames(_simpleEntities);
    internal string ComplexNames => FormatNames(_complexEntities);
    // The license's own tier string ("free", "trial", "paid", "pro", …). Stays "free" until a valid signed
    // key is resolved; a validated key with no explicit tier value reports "paid".
    internal string Tier => _tier ?? "free";

    internal void TrackSimple(IServiceCollection services, Type entityType)
    {
        if (IsSharedAttachmentBase(entityType))
        {
            return;
        }
        if (!_simpleEntities.Contains(entityType))
        {
            _simpleEntities.Add(entityType);
        }
        ValidateIfNeeded(services);
    }

    internal void TrackComplex(IServiceCollection services, Type entityType)
    {
        if (IsSharedAttachmentBase(entityType))
        {
            return;
        }
        if (!_complexEntities.Contains(entityType))
        {
            _complexEntities.Add(entityType);
        }
        ValidateIfNeeded(services);
    }

    // The shared attachment base implements IAttachment (it carries the file). The per-owner join entity
    // implements IEntityAttachment only, so it still consumes a slot; owners implement neither.
    private static bool IsSharedAttachmentBase(Type entityType)
        => typeof(IAttachment).IsAssignableFrom(entityType);

    private void ValidateIfNeeded(IServiceCollection services)
    {
        if (!_validated)
        {
            var allLicenses = services
                .Where(d => d.ServiceType == typeof(License) && d.ImplementationInstance != null)
                .Select(d => (License)d.ImplementationInstance!)
                .ToList();

            var best = LicenseUtility.Resolve(allLicenses, LicenseDefaults.Products.Entities);

            if (best == null || string.IsNullOrWhiteSpace(best.RawKey))
            {
                _tier = "free";
                var freeLimits = best?.Limits ?? LicenseDefaults.EntityFreeLimits;
                _simpleLimit = freeLimits.TryGetValue("entities.simple", out var sl) ? sl : 0;
                _complexLimit = freeLimits.TryGetValue("entities.complex", out var cl) ? cl : 0;
            }
            else
            {
                var license = LicenseValidator.Validate(best, LicenseDefaults.Products.Entities);
                _tier = string.IsNullOrWhiteSpace(license.Tier) ? "paid" : license.Tier;
                if (license.Limits == null)
                {
                    _simpleLimit = int.MaxValue;
                    _complexLimit = int.MaxValue;
                }
                else
                {
                    _simpleLimit = license.Limits.TryGetValue("entities.simple", out var sl) ? sl : int.MaxValue;
                    _complexLimit = license.Limits.TryGetValue("entities.complex", out var cl) ? cl : int.MaxValue;
                }
            }
            _validated = true;
        }

        if (_simpleEntities.Count > _simpleLimit || _complexEntities.Count > _complexLimit)
        {
            throw new LicenseException(
                $"Your Regira license allows {_simpleLimit} simple and {_complexLimit} complex entity registrations, " +
                $"but this application registers {_simpleEntities.Count} simple and {_complexEntities.Count} complex.\n" +
                $"- Simple ({_simpleEntities.Count}): {FormatNames(_simpleEntities)}\n" +
                $"- Complex ({_complexEntities.Count}): {FormatNames(_complexEntities)}\n" +
                "A registration is 'complex' when the For<>() overload includes TSortBy and TIncludes type parameters " +
                "(For<TEntity, TSearchObject, TSortBy, TIncludes>() or For<TEntity, TKey, TSearchObject, TSortBy, TIncludes>()); " +
                "all other For<>() overloads count as 'simple'.\n" +
                "Upgrade your license at https://regira.com/licensing");
        }
    }

    private static string FormatNames(List<Type> names)
        => names.Count == 0 ? "none" : string.Join(", ", names.Select(t => t.Name));
}
