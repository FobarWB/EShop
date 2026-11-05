using EShop.CustomerApi.Dtos;
using EShop.Data;
using EShop.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EShop.CustomerApi.Repositories;

public class CustomerRepository : ICustomerRepository
{
    public const int MaxAddresses = 10;

    private const string ProfilePrimaryKey = "pk_customer_profiles";
    private const string DefaultAddressIndex = "ix_customer_addresses_user_id_default";

    private readonly EShopDbContext _db;

    public CustomerRepository(EShopDbContext db)
    {
        _db = db;
    }

    public Task<string?> RetrievePhoneAsync(string userId, CancellationToken cancellationToken) =>
        _db
            .CustomerProfiles.Where(p => p.UserId == userId)
            .Select(p => p.Phone)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task UpdatePhoneAsync(
        string userId,
        string? phone,
        CancellationToken cancellationToken
    )
    {
        var profile = await EnsureProfileAsync(userId, cancellationToken);
        profile.Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        profile.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    // The default address first, then the newest. Take is a guard: the limit is checked on insert,
    // but two concurrent inserts can pass the check together.
    public Task<List<AddressResponse>> RetrieveAddressesAsync(
        string userId,
        CancellationToken cancellationToken
    ) =>
        _db
            .CustomerAddresses.Where(a => a.UserId == userId)
            .OrderByDescending(a => a.IsDefault)
            .ThenByDescending(a => a.AddressId)
            .Take(MaxAddresses)
            .ToResponse()
            .ToListAsync(cancellationToken);

    public Task<AddressResponse?> RetrieveAddressAsync(
        string userId,
        int addressId,
        CancellationToken cancellationToken
    ) =>
        _db
            .CustomerAddresses.Where(a => a.AddressId == addressId && a.UserId == userId)
            .ToResponse()
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<SaveAddressResult> AddAddressAsync(
        string userId,
        AddressRequest request,
        CancellationToken cancellationToken
    )
    {
        await EnsureProfileAsync(userId, cancellationToken);

        var count = await _db.CustomerAddresses.CountAsync(
            a => a.UserId == userId,
            cancellationToken
        );
        if (count >= MaxAddresses)
        {
            return new SaveAddressResult(SaveAddressStatus.LimitReached);
        }

        var address = new CustomerAddress { UserId = userId, CreatedAt = DateTime.UtcNow };
        request.ApplyTo(address);
        // The first address is the default one, so the checkout always has something to prefill.
        address.IsDefault = request.IsDefault || count == 0;
        _db.CustomerAddresses.Add(address);

        return await SaveAddressAsync(address, cancellationToken);
    }

    public async Task<SaveAddressResult> UpdateAddressAsync(
        string userId,
        int addressId,
        AddressRequest request,
        CancellationToken cancellationToken
    )
    {
        var address = await _db.CustomerAddresses.FirstOrDefaultAsync(
            a => a.AddressId == addressId && a.UserId == userId,
            cancellationToken
        );
        if (address == null)
        {
            return new SaveAddressResult(SaveAddressStatus.NotFound);
        }

        request.ApplyTo(address);
        return await SaveAddressAsync(address, cancellationToken);
    }

    public async Task<bool> DeleteAddressAsync(
        string userId,
        int addressId,
        CancellationToken cancellationToken
    ) =>
        await _db
            .CustomerAddresses.Where(a => a.AddressId == addressId && a.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken) > 0;

    // Saves a tracked address. A new default clears the previous one in the same transaction:
    // the UPDATE runs first, so the partial unique index never sees two defaults of one user.
    // A concurrent request that set another default commits first and this save fails with 23505.
    private async Task<SaveAddressResult> SaveAddressAsync(
        CustomerAddress address,
        CancellationToken cancellationToken
    )
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        if (address.IsDefault)
        {
            // A new address has id 0 here (not yet inserted): it matches none of the existing rows.
            await _db
                .CustomerAddresses.Where(a =>
                    a.UserId == address.UserId && a.IsDefault && a.AddressId != address.AddressId
                )
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.IsDefault, false), cancellationToken);
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex, DefaultAddressIndex))
        {
            // Not committed: disposing the transaction rolls back the UPDATE above.
            return new SaveAddressResult(SaveAddressStatus.DefaultConflict);
        }
        await transaction.CommitAsync(cancellationToken);

        var saved = await RetrieveAddressAsync(
            address.UserId,
            address.AddressId,
            cancellationToken
        );
        return new SaveAddressResult(SaveAddressStatus.Saved, saved);
    }

    // The profile is created on the user's first write (just-in-time). Two first requests at the same time
    // both find none; the second insert fails on the primary key and reads the row the first one committed.
    private async Task<CustomerProfile> EnsureProfileAsync(
        string userId,
        CancellationToken cancellationToken
    )
    {
        var profile = await _db.CustomerProfiles.FirstOrDefaultAsync(
            p => p.UserId == userId,
            cancellationToken
        );
        if (profile != null)
        {
            return profile;
        }

        var now = DateTime.UtcNow;
        profile = new CustomerProfile
        {
            UserId = userId,
            CreatedAt = now,
            UpdatedAt = now,
        };
        _db.CustomerProfiles.Add(profile);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            return profile;
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex, ProfilePrimaryKey))
        {
            // Detach the failed insert, otherwise the next SaveChanges would try it again.
            _db.Entry(profile).State = EntityState.Detached;
            return await _db.CustomerProfiles.SingleAsync(
                p => p.UserId == userId,
                cancellationToken
            );
        }
    }

    private static bool IsUniqueViolation(DbUpdateException ex, string constraintName) =>
        ex.InnerException
            is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: var name,
            }
        && name == constraintName;
}
