using Erp.Application.MasterData;
using Erp.Domain.Common;
using Erp.Domain.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Persistence;

public sealed class EfMasterDataService(ErpDbContext dbContext) : IMasterDataService
{
    public Task<PagedResult<BusinessPartnerDto>> GetCustomersAsync(Guid companyId, MasterDataQuery query, CancellationToken cancellationToken = default) =>
        GetPartnersAsync(companyId, query, true, cancellationToken);

    public Task<BusinessPartnerDto?> GetCustomerAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default) =>
        GetPartnerAsync(companyId, id, true, cancellationToken);

    public Task<BusinessPartnerDto> CreateCustomerAsync(Guid companyId, BusinessPartnerInput input, CancellationToken cancellationToken = default) =>
        CreatePartnerAsync(companyId, input, true, cancellationToken);

    public Task<BusinessPartnerDto?> UpdateCustomerAsync(Guid companyId, Guid id, BusinessPartnerInput input, CancellationToken cancellationToken = default) =>
        UpdatePartnerAsync(companyId, id, input, true, cancellationToken);

    public Task<bool> SetCustomerActiveAsync(Guid companyId, Guid id, bool isActive, CancellationToken cancellationToken = default) =>
        SetPartnerActiveAsync(companyId, id, true, isActive, cancellationToken);

    public Task<PagedResult<BusinessPartnerDto>> GetSuppliersAsync(Guid companyId, MasterDataQuery query, CancellationToken cancellationToken = default) =>
        GetPartnersAsync(companyId, query, false, cancellationToken);

    public Task<BusinessPartnerDto?> GetSupplierAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default) =>
        GetPartnerAsync(companyId, id, false, cancellationToken);

    public Task<BusinessPartnerDto> CreateSupplierAsync(Guid companyId, BusinessPartnerInput input, CancellationToken cancellationToken = default) =>
        CreatePartnerAsync(companyId, input, false, cancellationToken);

    public Task<BusinessPartnerDto?> UpdateSupplierAsync(Guid companyId, Guid id, BusinessPartnerInput input, CancellationToken cancellationToken = default) =>
        UpdatePartnerAsync(companyId, id, input, false, cancellationToken);

    public Task<bool> SetSupplierActiveAsync(Guid companyId, Guid id, bool isActive, CancellationToken cancellationToken = default) =>
        SetPartnerActiveAsync(companyId, id, false, isActive, cancellationToken);

    public async Task<bool> DeleteSupplierAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default)
    {
        var partner = await dbContext.BusinessPartners
            .Include(item => item.CustomerProfile)
            .Include(item => item.SupplierProfile)
            .SingleOrDefaultAsync(item => item.CompanyId == companyId && item.Id == id, cancellationToken);

        if (partner is null || partner.SupplierProfile is null) return false;

        if (partner.CustomerProfile is not null)
        {
            throw new MasterDataConflictException("This supplier is also a customer and cannot be deleted. Deactivate it instead.");
        }

        if (await HasSupplierDependenciesAsync(companyId, id, cancellationToken))
        {
            throw new MasterDataConflictException("This supplier has commercial or financial history and cannot be deleted. Deactivate it instead.");
        }

        dbContext.SupplierProfiles.Remove(partner.SupplierProfile);
        dbContext.BusinessPartners.Remove(partner);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<PagedResult<ProductCategoryDto>> GetCategoriesAsync(Guid companyId, MasterDataQuery query, CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(query);
        var categories = dbContext.ProductCategories.AsNoTracking().Where(category => category.CompanyId == companyId);
        categories = Filter(categories, normalized, item => item.Code, item => item.Name, item => item.IsActive);
        var totalCount = await categories.CountAsync(cancellationToken);
        var categoryOrder = normalized.SortBy?.ToLowerInvariant() == "name"
            ? (normalized.SortDescending ? categories.OrderByDescending(item => item.Name) : categories.OrderBy(item => item.Name))
            : (normalized.SortDescending ? categories.OrderByDescending(item => item.Code) : categories.OrderBy(item => item.Code));
        var entities = await categoryOrder
            .Skip((normalized.PageNumber - 1) * normalized.PageSize)
            .Take(normalized.PageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<ProductCategoryDto>(await ToCategoryDtosAsync(entities, cancellationToken), normalized.PageNumber, normalized.PageSize, totalCount);
    }

    public async Task<ProductCategoryDto?> GetCategoryAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default)
    {
        var category = await dbContext.ProductCategories.AsNoTracking().SingleOrDefaultAsync(item => item.CompanyId == companyId && item.Id == id, cancellationToken);
        return category is null ? null : (await ToCategoryDtosAsync([category], cancellationToken)).Single();
    }

    public async Task<ProductCategoryDto> CreateCategoryAsync(Guid companyId, ProductCategoryInput input, CancellationToken cancellationToken = default)
    {
        await EnsureCompanyAsync(companyId, cancellationToken);
        await EnsureUniqueCategoryCodeAsync(companyId, input.Code, null, cancellationToken);
        await EnsureValidParentAsync(companyId, Guid.Empty, input.ParentCategoryId, cancellationToken);
        var category = new ProductCategory(companyId, input.Code, input.Name, input.ParentCategoryId);
        dbContext.ProductCategories.Add(category);
        await dbContext.SaveChangesAsync(cancellationToken);
        return (await GetCategoryAsync(companyId, category.Id, cancellationToken))!;
    }

    public async Task<ProductCategoryDto?> UpdateCategoryAsync(Guid companyId, Guid id, ProductCategoryInput input, CancellationToken cancellationToken = default)
    {
        var category = await dbContext.ProductCategories.SingleOrDefaultAsync(item => item.CompanyId == companyId && item.Id == id, cancellationToken);
        if (category is null) return null;
        await EnsureUniqueCategoryCodeAsync(companyId, input.Code, id, cancellationToken);
        await EnsureValidParentAsync(companyId, id, input.ParentCategoryId, cancellationToken);
        category.Update(input.Code, input.Name, input.ParentCategoryId);
        await dbContext.SaveChangesAsync(cancellationToken);
        return (await GetCategoryAsync(companyId, id, cancellationToken))!;
    }

    public async Task<bool> SetCategoryActiveAsync(Guid companyId, Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        var category = await dbContext.ProductCategories.SingleOrDefaultAsync(item => item.CompanyId == companyId && item.Id == id, cancellationToken);
        if (category is null) return false;
        category.SetActive(isActive);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<PagedResult<UnitOfMeasureDto>> GetUnitsAsync(Guid companyId, MasterDataQuery query, CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(query) with { IsActive = null };
        var units = dbContext.UnitsOfMeasure.AsNoTracking().Where(unit => unit.CompanyId == companyId);
        if (!string.IsNullOrWhiteSpace(normalized.Search))
        {
            var term = normalized.Search.Trim().ToLower();
            units = units.Where(unit => unit.Code.ToLower().Contains(term) || unit.Name.ToLower().Contains(term));
        }
        var totalCount = await units.CountAsync(cancellationToken);
        var unitOrder = normalized.SortBy?.ToLowerInvariant() == "name"
            ? (normalized.SortDescending ? units.OrderByDescending(item => item.Name) : units.OrderBy(item => item.Name))
            : (normalized.SortDescending ? units.OrderByDescending(item => item.Code) : units.OrderBy(item => item.Code));
        var items = await unitOrder.Skip((normalized.PageNumber - 1) * normalized.PageSize).Take(normalized.PageSize).ToListAsync(cancellationToken);
        return new PagedResult<UnitOfMeasureDto>(items.Select(unit => new UnitOfMeasureDto(unit.Id, unit.Code, unit.Name, unit.Dimension, unit.ConversionFactorToBase, unit.DecimalPlaces)).ToArray(), normalized.PageNumber, normalized.PageSize, totalCount);
    }

    public async Task<UnitOfMeasureDto?> GetUnitAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default)
    {
        var unit = await dbContext.UnitsOfMeasure.AsNoTracking().SingleOrDefaultAsync(item => item.CompanyId == companyId && item.Id == id, cancellationToken);
        return unit is null ? null : new UnitOfMeasureDto(unit.Id, unit.Code, unit.Name, unit.Dimension, unit.ConversionFactorToBase, unit.DecimalPlaces);
    }

    public async Task<UnitOfMeasureDto> CreateUnitAsync(Guid companyId, UnitOfMeasureInput input, CancellationToken cancellationToken = default)
    {
        await EnsureCompanyAsync(companyId, cancellationToken);
        await EnsureUniqueUnitCodeAsync(companyId, input.Code, null, cancellationToken);
        var unit = new UnitOfMeasure(input.Code, input.Name, input.Dimension, input.ConversionFactorToBase, input.DecimalPlaces, companyId);
        dbContext.UnitsOfMeasure.Add(unit);
        await dbContext.SaveChangesAsync(cancellationToken);
        return (await GetUnitAsync(companyId, unit.Id, cancellationToken))!;
    }

    public async Task<UnitOfMeasureDto?> UpdateUnitAsync(Guid companyId, Guid id, UnitOfMeasureInput input, CancellationToken cancellationToken = default)
    {
        var unit = await dbContext.UnitsOfMeasure.SingleOrDefaultAsync(item => item.CompanyId == companyId && item.Id == id, cancellationToken);
        if (unit is null) return null;
        await EnsureUniqueUnitCodeAsync(companyId, input.Code, id, cancellationToken);
        unit.Update(input.Code, input.Name, input.Dimension, input.ConversionFactorToBase, input.DecimalPlaces);
        await dbContext.SaveChangesAsync(cancellationToken);
        return (await GetUnitAsync(companyId, id, cancellationToken))!;
    }

    public async Task<PagedResult<ProductDto>> GetProductsAsync(Guid companyId, MasterDataQuery query, CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(query);
        var products = dbContext.Products.AsNoTracking().Where(product => product.CompanyId == companyId);
        products = Filter(products, normalized, item => item.Sku, item => item.Name, item => item.IsActive);
        if (normalized.ProductCategoryId.HasValue) products = products.Where(product => product.ProductCategoryId == normalized.ProductCategoryId);
        if (normalized.UnitOfMeasureId.HasValue) products = products.Where(product => product.StockUnitOfMeasureId == normalized.UnitOfMeasureId);
        var totalCount = await products.CountAsync(cancellationToken);
        var productOrder = normalized.SortBy?.ToLowerInvariant() == "name"
            ? (normalized.SortDescending ? products.OrderByDescending(item => item.Name) : products.OrderBy(item => item.Name))
            : (normalized.SortDescending ? products.OrderByDescending(item => item.Sku) : products.OrderBy(item => item.Sku));
        var entities = await productOrder.Skip((normalized.PageNumber - 1) * normalized.PageSize).Take(normalized.PageSize).ToListAsync(cancellationToken);
        return new PagedResult<ProductDto>(await ToProductDtosAsync(entities, cancellationToken), normalized.PageNumber, normalized.PageSize, totalCount);
    }

    public async Task<ProductDto?> GetProductAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default)
    {
        var product = await dbContext.Products.AsNoTracking().SingleOrDefaultAsync(item => item.CompanyId == companyId && item.Id == id, cancellationToken);
        return product is null ? null : (await ToProductDtosAsync([product], cancellationToken)).Single();
    }

    public async Task<ProductDto> CreateProductAsync(Guid companyId, ProductInput input, CancellationToken cancellationToken = default)
    {
        await EnsureCompanyAsync(companyId, cancellationToken);
        await EnsureProductReferencesAsync(companyId, input.StockUnitOfMeasureId, input.ProductCategoryId, cancellationToken);
        var product = new Product(companyId, await NextCodeAsync("product_sku_sequence", "PRD", cancellationToken), input.Name, input.ProductType, input.StockUnitOfMeasureId, input.ProductCategoryId, inventoryPurpose: input.InventoryPurpose);
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync(cancellationToken);
        return (await GetProductAsync(companyId, product.Id, cancellationToken))!;
    }

    public async Task<ProductDto?> UpdateProductAsync(Guid companyId, Guid id, ProductInput input, CancellationToken cancellationToken = default)
    {
        var product = await dbContext.Products.SingleOrDefaultAsync(item => item.CompanyId == companyId && item.Id == id, cancellationToken);
        if (product is null) return null;
        await EnsureProductReferencesAsync(companyId, input.StockUnitOfMeasureId, input.ProductCategoryId, cancellationToken);
        product.Update(product.Sku, input.Name, input.ProductType, input.StockUnitOfMeasureId, input.ProductCategoryId, inventoryPurpose: input.InventoryPurpose);
        await dbContext.SaveChangesAsync(cancellationToken);
        return (await GetProductAsync(companyId, id, cancellationToken))!;
    }

    public async Task<bool> SetProductActiveAsync(Guid companyId, Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        var product = await dbContext.Products.SingleOrDefaultAsync(item => item.CompanyId == companyId && item.Id == id, cancellationToken);
        if (product is null) return false;
        product.SetActive(isActive);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<PagedResult<BusinessPartnerDto>> GetPartnersAsync(Guid companyId, MasterDataQuery query, bool customer, CancellationToken cancellationToken)
    {
        var normalized = Normalize(query);
        var partners = dbContext.BusinessPartners.AsNoTracking().Where(partner => partner.CompanyId == companyId && (customer ? partner.CustomerProfile != null : partner.SupplierProfile != null));
        partners = Filter(partners, normalized, item => item.Code, item => item.LegalName, item => item.IsActive);
        var totalCount = await partners.CountAsync(cancellationToken);
        var partnerOrder = normalized.SortBy?.ToLowerInvariant() == "name"
            ? (normalized.SortDescending ? partners.OrderByDescending(item => item.LegalName) : partners.OrderBy(item => item.LegalName))
            : (normalized.SortDescending ? partners.OrderByDescending(item => item.Code) : partners.OrderBy(item => item.Code));
        var entities = await partnerOrder.Skip((normalized.PageNumber - 1) * normalized.PageSize).Take(normalized.PageSize).ToListAsync(cancellationToken);
        return new PagedResult<BusinessPartnerDto>(entities.Select(ToDto).ToArray(), normalized.PageNumber, normalized.PageSize, totalCount);
    }

    private async Task<BusinessPartnerDto?> GetPartnerAsync(Guid companyId, Guid id, bool customer, CancellationToken cancellationToken)
    {
        var partner = await dbContext.BusinessPartners.AsNoTracking().SingleOrDefaultAsync(item => item.CompanyId == companyId && item.Id == id && (customer ? item.CustomerProfile != null : item.SupplierProfile != null), cancellationToken);
        return partner is null ? null : ToDto(partner);
    }

    private async Task<BusinessPartnerDto> CreatePartnerAsync(Guid companyId, BusinessPartnerInput input, bool customer, CancellationToken cancellationToken)
    {
        await EnsureCompanyAsync(companyId, cancellationToken);
        var partner = CreatePartner(companyId, input, await NextCodeAsync(customer ? "customer_code_sequence" : "supplier_code_sequence", customer ? "CUS" : "SUP", cancellationToken));
        if (customer) partner.EnableCustomer(); else partner.EnableSupplier();
        dbContext.BusinessPartners.Add(partner);
        await dbContext.SaveChangesAsync(cancellationToken);
        return (await GetPartnerAsync(companyId, partner.Id, customer, cancellationToken))!;
    }

    private async Task<BusinessPartnerDto?> UpdatePartnerAsync(Guid companyId, Guid id, BusinessPartnerInput input, bool customer, CancellationToken cancellationToken)
    {
        var partner = await dbContext.BusinessPartners
            .Include(item => item.CustomerProfile)
            .Include(item => item.SupplierProfile)
            .SingleOrDefaultAsync(item => item.CompanyId == companyId && item.Id == id, cancellationToken);
        if (partner is null || (customer ? partner.CustomerProfile is null : partner.SupplierProfile is null)) return null;
        partner.Update(partner.Code, input.LegalName, CreateAddress(input), CreateContact(input), input.TaxIdentifier, input.PaymentTermsDays);
        await dbContext.SaveChangesAsync(cancellationToken);
        return (await GetPartnerAsync(companyId, id, customer, cancellationToken))!;
    }

    private async Task<bool> SetPartnerActiveAsync(Guid companyId, Guid id, bool customer, bool isActive, CancellationToken cancellationToken)
    {
        var partner = await dbContext.BusinessPartners
            .Include(item => item.CustomerProfile)
            .Include(item => item.SupplierProfile)
            .SingleOrDefaultAsync(item => item.CompanyId == companyId && item.Id == id, cancellationToken);
        if (partner is null || (customer ? partner.CustomerProfile is null : partner.SupplierProfile is null)) return false;
        partner.SetActive(isActive);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<bool> HasSupplierDependenciesAsync(Guid companyId, Guid supplierId, CancellationToken cancellationToken)
    {
        return await dbContext.PurchaseInvoices.AnyAsync(invoice => invoice.CompanyId == companyId && invoice.SupplierId == supplierId, cancellationToken)
            || await dbContext.Payments.AnyAsync(payment => payment.CompanyId == companyId && payment.BusinessPartnerId == supplierId, cancellationToken)
            || await dbContext.OpenItems.AnyAsync(item => item.CompanyId == companyId && item.BusinessPartnerId == supplierId, cancellationToken)
            || await dbContext.JournalEntryLines.AnyAsync(line => line.BusinessPartnerId == supplierId, cancellationToken);
    }

    private static BusinessPartner CreatePartner(Guid companyId, BusinessPartnerInput input, string code) => new(companyId, code, input.LegalName, input.TaxIdentifier, input.PaymentTermsDays, CreateAddress(input), CreateContact(input));
    private static Address? CreateAddress(BusinessPartnerInput input) => string.IsNullOrWhiteSpace(input.AddressLine1) ? null : new Address(input.AddressLine1, input.CountryCode!, input.AddressLine2, input.City, input.PostalCode);
    private static ContactDetails? CreateContact(BusinessPartnerInput input) => string.IsNullOrWhiteSpace(input.Email) && string.IsNullOrWhiteSpace(input.Phone) ? null : new ContactDetails(input.Email, input.Phone);

    private async Task<IReadOnlyCollection<ProductCategoryDto>> ToCategoryDtosAsync(IReadOnlyCollection<ProductCategory> categories, CancellationToken cancellationToken)
    {
        var parentIds = categories.Where(category => category.ParentCategoryId.HasValue).Select(category => category.ParentCategoryId!.Value).Distinct().ToArray();
        var parentNames = parentIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.ProductCategories.AsNoTracking().Where(category => parentIds.Contains(category.Id)).ToDictionaryAsync(category => category.Id, category => category.Name, cancellationToken);
        return categories.Select(category => new ProductCategoryDto(category.Id, category.Code, category.Name, category.ParentCategoryId, category.ParentCategoryId.HasValue && parentNames.TryGetValue(category.ParentCategoryId.Value, out var parentName) ? parentName : null, category.IsActive)).ToArray();
    }

    private async Task<IReadOnlyCollection<ProductDto>> ToProductDtosAsync(IReadOnlyCollection<Product> products, CancellationToken cancellationToken)
    {
        var unitIds = products.Where(product => product.StockUnitOfMeasureId.HasValue).Select(product => product.StockUnitOfMeasureId!.Value).Distinct().ToArray();
        var categoryIds = products.Where(product => product.ProductCategoryId.HasValue).Select(product => product.ProductCategoryId!.Value).Distinct().ToArray();
        var unitNames = unitIds.Length == 0 ? new Dictionary<Guid, string>() : await dbContext.UnitsOfMeasure.AsNoTracking().Where(unit => unitIds.Contains(unit.Id)).ToDictionaryAsync(unit => unit.Id, unit => unit.Name, cancellationToken);
        var categoryNames = categoryIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.ProductCategories.AsNoTracking().Where(category => categoryIds.Contains(category.Id)).ToDictionaryAsync(category => category.Id, category => category.Name, cancellationToken);
        return products.Select(product => new ProductDto(product.Id, product.Sku, product.Name, product.ProductType, product.StockUnitOfMeasureId, product.StockUnitOfMeasureId.HasValue && unitNames.TryGetValue(product.StockUnitOfMeasureId.Value, out var unitName) ? unitName : null, product.ProductCategoryId, product.ProductCategoryId.HasValue && categoryNames.TryGetValue(product.ProductCategoryId.Value, out var categoryName) ? categoryName : null, product.InventoryPurpose, product.IsActive)).ToArray();
    }

    private static BusinessPartnerDto ToDto(BusinessPartner partner) => new(partner.Id, partner.Code, partner.LegalName, partner.TaxIdentifier, partner.Address?.Line1, partner.Address?.Line2, partner.Address?.City, partner.Address?.PostalCode, partner.Address?.CountryCode, partner.ContactDetails?.Email, partner.ContactDetails?.Phone, partner.PaymentTermsDays, partner.IsActive);

    private async Task EnsureCompanyAsync(Guid companyId, CancellationToken cancellationToken)
    {
        if (!await dbContext.Companies.AnyAsync(company => company.Id == companyId && company.IsActive, cancellationToken)) throw new MasterDataValidationException("The selected company is unavailable.");
    }

    private async Task EnsureUniquePartnerCodeAsync(Guid companyId, string code, Guid? excludedId, CancellationToken cancellationToken) =>
        await EnsureUniqueAsync(dbContext.BusinessPartners.Where(item => item.CompanyId == companyId), code, excludedId, item => item.Code, "business partner", cancellationToken);
    private async Task EnsureUniqueCategoryCodeAsync(Guid companyId, string code, Guid? excludedId, CancellationToken cancellationToken) =>
        await EnsureUniqueAsync(dbContext.ProductCategories.Where(item => item.CompanyId == companyId), code, excludedId, item => item.Code, "product category", cancellationToken);
    private async Task EnsureUniqueUnitCodeAsync(Guid companyId, string code, Guid? excludedId, CancellationToken cancellationToken) =>
        await EnsureUniqueAsync(dbContext.UnitsOfMeasure.Where(item => item.CompanyId == companyId), code, excludedId, item => item.Code, "unit of measure", cancellationToken);
    private async Task EnsureUniqueProductSkuAsync(Guid companyId, string sku, Guid? excludedId, CancellationToken cancellationToken) =>
        await EnsureUniqueAsync(dbContext.Products.Where(item => item.CompanyId == companyId), sku, excludedId, item => item.Sku, "product", cancellationToken);

    private static async Task EnsureUniqueAsync<TEntity>(IQueryable<TEntity> source, string code, Guid? excludedId, System.Linq.Expressions.Expression<Func<TEntity, string>> codeSelector, string recordName, CancellationToken cancellationToken) where TEntity : Entity
    {
        var normalized = code.Trim().ToUpperInvariant();
        var parameter = System.Linq.Expressions.Expression.Parameter(typeof(TEntity), "item");
        var codeValue = ReplaceParameter(codeSelector, parameter);
        var body = System.Linq.Expressions.Expression.Equal(codeValue, System.Linq.Expressions.Expression.Constant(normalized));
        if (excludedId.HasValue)
        {
            var id = System.Linq.Expressions.Expression.Property(parameter, nameof(Entity.Id));
            body = System.Linq.Expressions.Expression.AndAlso(body, System.Linq.Expressions.Expression.NotEqual(id, System.Linq.Expressions.Expression.Constant(excludedId.Value)));
        }
        var duplicate = await source.AnyAsync(System.Linq.Expressions.Expression.Lambda<Func<TEntity, bool>>(body, parameter), cancellationToken);
        if (duplicate) throw new MasterDataConflictException($"A {recordName} with this code already exists.");
    }

    private async Task EnsureValidParentAsync(Guid companyId, Guid categoryId, Guid? parentCategoryId, CancellationToken cancellationToken)
    {
        if (!parentCategoryId.HasValue) return;
        if (parentCategoryId == categoryId) throw new MasterDataValidationException("A category cannot be its own parent.");
        var parent = await dbContext.ProductCategories.AsNoTracking().SingleOrDefaultAsync(item => item.CompanyId == companyId && item.Id == parentCategoryId, cancellationToken);
        if (parent is null) throw new MasterDataValidationException("The selected parent category was not found.");
        if (!parent.IsActive) throw new MasterDataValidationException("An inactive category cannot be used as a parent.");
    }

    private async Task EnsureProductReferencesAsync(Guid companyId, Guid? unitId, Guid? categoryId, CancellationToken cancellationToken)
    {
        if (unitId.HasValue && !await dbContext.UnitsOfMeasure.AnyAsync(item => item.Id == unitId && (item.CompanyId == null || item.CompanyId == companyId), cancellationToken)) throw new MasterDataValidationException("The selected unit of measure was not found.");
        if (categoryId.HasValue && !await dbContext.ProductCategories.AnyAsync(item => item.Id == categoryId && item.CompanyId == companyId && item.IsActive, cancellationToken)) throw new MasterDataValidationException("The selected active product category was not found.");
    }

    private async Task<string> NextCodeAsync(string sequenceName, string prefix, CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsRelational())
        {
            var nextValue = sequenceName switch
            {
                "customer_code_sequence" => await dbContext.BusinessPartners.CountAsync(partner => partner.Code.StartsWith("CUS-"), cancellationToken) + 1,
                "supplier_code_sequence" => await dbContext.BusinessPartners.CountAsync(partner => partner.Code.StartsWith("SUP-"), cancellationToken) + 1,
                "product_sku_sequence" => await dbContext.Products.CountAsync(product => product.Sku.StartsWith("PRD-"), cancellationToken) + 1,
                _ => throw new ArgumentOutOfRangeException(nameof(sequenceName), sequenceName, "Unsupported master-data numbering sequence.")
            };

            return $"{prefix}-{nextValue:D6}";
        }

        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = dbContext.Database.GetDbConnection().CreateCommand();
            command.CommandText = $"SELECT nextval('erp.{sequenceName}')";
            var value = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
            return $"{prefix}-{value:D6}";
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }

    private static MasterDataQuery Normalize(MasterDataQuery query) => query with { PageNumber = Math.Max(query.PageNumber, 1), PageSize = Math.Clamp(query.PageSize, 1, 100) };
    private static IQueryable<T> Filter<T>(IQueryable<T> source, MasterDataQuery query, System.Linq.Expressions.Expression<Func<T, string>> code, System.Linq.Expressions.Expression<Func<T, string>> name, System.Linq.Expressions.Expression<Func<T, bool>> isActive)
        where T : class
    {
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            var parameter = System.Linq.Expressions.Expression.Parameter(typeof(T), "item");
            var codeValue = ReplaceParameter(code, parameter);
            var nameValue = ReplaceParameter(name, parameter);
            var toLower = typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!;
            var contains = typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;
            var termExpression = System.Linq.Expressions.Expression.Constant(term);
            var body = System.Linq.Expressions.Expression.OrElse(
                System.Linq.Expressions.Expression.Call(System.Linq.Expressions.Expression.Call(codeValue, toLower), contains, termExpression),
                System.Linq.Expressions.Expression.Call(System.Linq.Expressions.Expression.Call(nameValue, toLower), contains, termExpression));
            source = source.Where(System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(body, parameter));
        }
        if (query.IsActive.HasValue)
        {
            var parameter = System.Linq.Expressions.Expression.Parameter(typeof(T), "item");
            var activeValue = ReplaceParameter(isActive, parameter);
            var body = System.Linq.Expressions.Expression.Equal(activeValue, System.Linq.Expressions.Expression.Constant(query.IsActive.Value));
            source = source.Where(System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(body, parameter));
        }

        return source;
    }
    private static System.Linq.Expressions.Expression ReplaceParameter<T, TValue>(System.Linq.Expressions.Expression<Func<T, TValue>> expression, System.Linq.Expressions.ParameterExpression parameter) =>
        new ParameterReplacementVisitor(expression.Parameters[0], parameter).Visit(expression.Body)!;
    private sealed class ParameterReplacementVisitor(System.Linq.Expressions.ParameterExpression source, System.Linq.Expressions.ParameterExpression target) : System.Linq.Expressions.ExpressionVisitor
    {
        protected override System.Linq.Expressions.Expression VisitParameter(System.Linq.Expressions.ParameterExpression node) => node == source ? target : base.VisitParameter(node);
    }
}
