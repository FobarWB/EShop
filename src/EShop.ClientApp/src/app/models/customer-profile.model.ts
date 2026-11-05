// Email and name come from the token and are changed in the Keycloak account console; only the phone is ours.
export interface ICustomerProfile
{
    email: string | null;
    firstName: string | null;
    lastName: string | null;
    phone: string | null;
}
