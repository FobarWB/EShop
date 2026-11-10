namespace EShop.ServiceDefaults.Keycloak;

// Keycloak did not answer or refused the service account: not a decision about the user, so the client gets 503.
public sealed class IdentityProviderUnavailableException(string message, Exception innerException)
    : Exception(message, innerException);
