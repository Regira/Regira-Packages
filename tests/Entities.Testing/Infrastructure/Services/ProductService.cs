using Entities.Testing.Infrastructure.Data;
using Regira.Entities.EFcore.QueryBuilders;
using Regira.Entities.QueryBuilders.Abstractions;
using Regira.Entities.EFcore.Services;
using Regira.Entities.Models;
using Regira.Entities.Services.Abstractions;

namespace Entities.Testing.Infrastructure.Services;

public class ProductService(IEntityReadService<Product, int, SearchObject<int>> readService, IEntityWriteService<Product, int> writeService)
    : EntityRepository<Product>(readService, writeService)
{
}
public class ProductQueryBuilder(IEnumerable<IGlobalFilteredQueryBuilder> globalFilters,
    IEnumerable<IFilteredQueryBuilder<Product, int, SearchObject<int>>>? filters = null)
    : QueryBuilder<Product>(globalFilters, filters);

// Wrapping service whose Add override sets a sentinel — used to verify Save() routes through Add().
public class CustomerAddOverrideService(IEntityRepository<Customer, int, SearchObject<int>> service)
    : EntityWrappingServiceBase<Customer>(service)
{
    public const string GeneratedName = "ADD-OVERRIDE";
    public override Task Add(Customer item, CancellationToken token = default)
    {
        item.Name = GeneratedName;
        return base.Add(item, token);
    }
}