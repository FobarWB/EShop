namespace EShop.CustomerApi.Dtos;

// Email and name come from the token (Keycloak owns them); the phone from customer_profiles.
public sealed record CustomerProfileResponse(
    string? Email,
    string? FirstName,
    string? LastName,
    string? Phone
);
