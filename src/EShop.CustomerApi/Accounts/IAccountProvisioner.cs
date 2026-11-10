namespace EShop.CustomerApi.Accounts;

// NeedsPassword: the account has no password yet (created for a guest order and the link not used yet).
public sealed record ProvisionedAccount(string UserId, bool NeedsPassword);

// Accounts for guest orders, in the identity provider.
public interface IAccountProvisioner
{
    // Finds the account of this email or creates it without a password. Idempotent: a retry finds the first one.
    // email: trimmed and in lower case.
    Task<ProvisionedAccount> FindOrCreateAsync(string email, CancellationToken cancellationToken);

    // The identity provider's email with a single-use link to set the password.
    Task SendSetPasswordEmailAsync(string userId, CancellationToken cancellationToken);
}
