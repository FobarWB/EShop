namespace EShop.OrderApi.Accounts;

// Whether an email already belongs to an account: the guest checkout asks the visitor to sign in instead.
public interface IAccountDirectory
{
    // email: trimmed and in lower case (Keycloak stores emails in lower case).
    Task<bool> AccountExistsAsync(string email, CancellationToken cancellationToken);
}
