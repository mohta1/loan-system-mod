using System.Data;
using System.Text.Json;
using LoanSystem.Contracts;
using LoanSystem.Modules.LoanProducts.Application;
using LoanSystem.Modules.LoanProducts.Domain;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage;

namespace LoanSystem.Modules.LoanProducts.Infrastructure;

public sealed class LoanProductsDbContext(DbContextOptions<LoanProductsDbContext> options) : DbContext(options), ILoanProductStore
{
    private const string VersionNumberIndex = "IX_versions_product_number";

    public DbSet<LoanProduct> Products => Set<LoanProduct>();
    public DbSet<LoanProductVersion> Versions => Set<LoanProductVersion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new ProductConfiguration());
        modelBuilder.ApplyConfiguration(new VersionConfiguration());
        modelBuilder.ApplyConfiguration(new FinancingTypeConfiguration());
    }

    public Task AddAsync(LoanProduct product, CancellationToken ct) => Products.AddAsync(product, ct).AsTask();

    public Task<LoanProduct?> FindAsync(Guid id, CancellationToken ct) => Products
        .Include(product => product.Versions)
        .ThenInclude(version => version.FinancingTypes)
        .SingleOrDefaultAsync(product => product.Id == id, ct);

    public Task<LoanProductVersion?> FindVersionAsync(Guid productId, Guid versionId, CancellationToken ct) => Versions
        .Include(version => version.FinancingTypes)
        .SingleOrDefaultAsync(version => version.LoanProductId == productId && version.Id == versionId, ct);

    public async Task<IReadOnlyList<ProductListItem>> ListAsync(CancellationToken ct) => await Products
        .AsNoTracking()
        .OrderBy(product => product.Name)
        .Select(product => new ProductListItem(
            product.Id,
            product.Name,
            product.Status == LoanProductStatus.Active ? "Active" : "Inactive",
            product.Versions.Count,
            product.Versions.Select(version => (int?)version.VersionNumber).Max()))
        .ToListAsync(ct);

    public async Task<IReadOnlyList<AvailableProductVersion>> AvailableAsync(DateOnly today, CancellationToken ct) => await Versions
        .AsNoTracking()
        .Where(version => version.Status == LoanProductVersionStatus.Published
            && version.EffectiveFrom <= today
            && (!version.EffectiveTo.HasValue || version.EffectiveTo >= today)
            && Products.Any(product => product.Id == version.LoanProductId && product.Status == LoanProductStatus.Active))
        .OrderBy(version => version.LoanProductId)
        .ThenBy(version => version.VersionNumber)
        .Select(version => new AvailableProductVersion(
            version.LoanProductId,
            version.Id,
            Products.Where(product => product.Id == version.LoanProductId).Select(product => product.Name).Single(),
            version.VersionNumber,
            version.MaximumAmount,
            version.Currency,
            version.DeductionPercentage,
            version.FinancingTypes.Select(type => type.Value).ToArray(),
            version.EffectiveFrom,
            version.EffectiveTo))
        .ToListAsync(ct);

    public async Task<int> NextVersionNumberAsync(Guid productId, CancellationToken ct)
    {
        if (Database.CurrentTransaction is null)
            await Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

        var connection = (SqlConnection)Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = (SqlTransaction)Database.CurrentTransaction!.GetDbTransaction();
        command.CommandText = "SELECT COALESCE(MAX(version_number), 0) FROM loan_products.loan_product_versions WITH (UPDLOCK, HOLDLOCK) WHERE loan_product_id = @productId";
        command.Parameters.Add(new SqlParameter("@productId", SqlDbType.UniqueIdentifier) { Value = productId });
        var current = Convert.ToInt32(await command.ExecuteScalarAsync(ct), System.Globalization.CultureInfo.InvariantCulture);
        return checked(current + 1);
    }

    public async Task LockProductAsync(Guid productId, CancellationToken ct)
    {
        if (Database.CurrentTransaction is null)
            await Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

        var connection = (SqlConnection)Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = (SqlTransaction)Database.CurrentTransaction!.GetDbTransaction();
        command.CommandText = "SELECT loan_product_id FROM loan_products.loan_products WITH (UPDLOCK, HOLDLOCK) WHERE loan_product_id = @productId";
        command.Parameters.Add(new SqlParameter("@productId", SqlDbType.UniqueIdentifier) { Value = productId });
        if (await command.ExecuteScalarAsync(ct) is null)
            throw new InvalidOperationException("The loan product disappeared during publication.");
    }

    public Task<bool> OverlapsAsync(Guid productId, Guid except, DateOnly effectiveFrom, DateOnly? effectiveTo, CancellationToken ct) => Versions.AnyAsync(
        version => version.LoanProductId == productId
            && version.Id != except
            && version.Status == LoanProductVersionStatus.Published
            && (!version.EffectiveTo.HasValue || version.EffectiveTo >= effectiveFrom)
            && (!effectiveTo.HasValue || version.EffectiveFrom <= effectiveTo),
        ct);

    public void Expect(LoanProduct product, byte[] version) => Entry(product).Property(value => value.RowVersion).OriginalValue = version;

    public void Expect(LoanProductVersion version, byte[] expected) => Entry(version).Property(value => value.RowVersion).OriginalValue = expected;

    public async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await SaveChangesAsync(ct);
            if (Database.CurrentTransaction is not null)
                await Database.CommitTransactionAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new LoanProductConcurrencyException();
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 } sqlException
            && sqlException.Message.Contains(VersionNumberIndex, StringComparison.Ordinal))
        {
            throw new LoanProductVersionConflictException();
        }
    }
}

internal sealed class ProductConfiguration : IEntityTypeConfiguration<LoanProduct>
{
    public void Configure(EntityTypeBuilder<LoanProduct> builder)
    {
        builder.ToTable("loan_products", "loan_products");
        builder.HasKey(product => product.Id);
        builder.Property(product => product.Id).HasColumnName("loan_product_id");
        builder.Property(product => product.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(product => product.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
        builder.Property(product => product.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(product => product.UpdatedAtUtc).HasColumnName("updated_at_utc");
        builder.Property(product => product.RowVersion).HasColumnName("row_version").IsRowVersion();
        builder.HasMany(product => product.Versions).WithOne().HasForeignKey(version => version.LoanProductId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class VersionConfiguration : IEntityTypeConfiguration<LoanProductVersion>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<LoanProductVersion> builder)
    {
        builder.ToTable("loan_product_versions", "loan_products");
        builder.HasKey(version => version.Id);
        builder.Property(version => version.Id).HasColumnName("version_id");
        builder.Property(version => version.LoanProductId).HasColumnName("loan_product_id");
        builder.Property(version => version.VersionNumber).HasColumnName("version_number");
        builder.HasIndex(version => new { version.LoanProductId, version.VersionNumber }).IsUnique().HasDatabaseName("IX_versions_product_number");
        builder.Property(version => version.MaximumAmount).HasColumnName("maximum_amount").HasPrecision(19, 4);
        builder.Property(version => version.Currency).HasColumnName("currency").HasColumnType("char(3)");
        builder.Property(version => version.DeductionPercentage).HasColumnName("deduction_percentage").HasPrecision(9, 4);
        builder.Property(version => version.EligibilityConfiguration)
            .HasColumnName("eligibility_configuration")
            .HasColumnType("nvarchar(max)")
            .HasConversion(value => SerializeEligibility(value), value => DeserializeEligibility(value))
            .Metadata.SetValueComparer(new ValueComparer<EligibilityConfiguration>(
                (left, right) => SerializeEligibility(left!) == SerializeEligibility(right!),
                value => SerializeEligibility(value).GetHashCode(StringComparison.Ordinal),
                value => DeserializeEligibility(SerializeEligibility(value))));
        builder.Property(version => version.EffectiveFrom).HasColumnName("effective_from").HasColumnType("date");
        builder.Property(version => version.EffectiveTo).HasColumnName("effective_to").HasColumnType("date");
        builder.Property(version => version.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
        builder.Property(version => version.PublishedAtUtc).HasColumnName("published_at_utc");
        builder.Property(version => version.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(version => version.RowVersion).HasColumnName("row_version").IsRowVersion();
    }

    internal static string SerializeEligibility(EligibilityConfiguration value) => JsonSerializer.Serialize(value, JsonOptions);
    internal static EligibilityConfiguration DeserializeEligibility(string value) => JsonSerializer.Deserialize<EligibilityConfiguration>(value, JsonOptions) ?? throw new InvalidOperationException("Eligibility configuration is invalid.");
}

internal sealed class FinancingTypeConfiguration : IEntityTypeConfiguration<LoanProductFinancingType>
{
    public void Configure(EntityTypeBuilder<LoanProductFinancingType> builder)
    {
        builder.ToTable("loan_product_financing_types", "loan_products");
        builder.HasKey(type => new { type.VersionId, type.Value });
        builder.Property(type => type.VersionId).HasColumnName("version_id");
        builder.Property(type => type.Value).HasColumnName("financing_type").HasMaxLength(100);
        builder.HasOne<LoanProductVersion>().WithMany(version => version.FinancingTypes).HasForeignKey(type => type.VersionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class LoanProductsModule(LoanProductsDbContext database) : ILoanProductsModule
{
    public async Task<LoanProductVersionLookup> GetVersionAsync(Guid versionId, DateOnly businessDate, CancellationToken cancellationToken = default)
    {
        var version = await database.Versions.AsNoTracking().Include(value => value.FinancingTypes).SingleOrDefaultAsync(value => value.Id == versionId, cancellationToken);
        if (version is null)
            return new(LoanProductVersionLookupStatus.NotFound, null);

        var product = await database.Products.AsNoTracking().SingleAsync(value => value.Id == version.LoanProductId, cancellationToken);
        var status = version.Status == LoanProductVersionStatus.Draft
            ? LoanProductVersionLookupStatus.Draft
            : product.Status == LoanProductStatus.Inactive
                ? LoanProductVersionLookupStatus.ProductInactive
                : version.EffectiveFrom > businessDate || (version.EffectiveTo.HasValue && version.EffectiveTo < businessDate)
                    ? LoanProductVersionLookupStatus.OutsideEffectivePeriod
                    : LoanProductVersionLookupStatus.Available;
        var eligibility = version.EligibilityConfiguration;
        var contract = new LoanProductVersionContract(
            product.Id,
            version.Id,
            product.Name,
            version.VersionNumber,
            product.Status.ToString(),
            version.MaximumAmount,
            version.Currency,
            version.DeductionPercentage,
            version.FinancingTypes.Select(type => type.Value).ToArray(),
            new LoanProductEligibility(
                eligibility.RequiredNationality,
                eligibility.MaximumApplicationCount,
                eligibility.RankGradeAmountRules.Select(rule => new LoanProductRankGradeRule(rule.RankGrade, rule.MaximumAmount)).ToArray(),
                eligibility.Term.MaximumTermMonths,
                eligibility.Term.DueDateRule),
            version.EffectiveFrom,
            version.EffectiveTo,
            version.Status.ToString(),
            version.PublishedAtUtc);
        return new(status, contract);
    }
}
