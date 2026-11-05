---
type: decision
status: accepted
verified: 2026-11-03
issue: 44
---

# Account lifecycle in Keycloak

## Context

Issue #44 asks for sign-up, email confirmation, resend, password reset, a password policy, case-insensitive unique
emails, rate limiting and no account enumeration. [UseKeycloakForAuthentication](UseKeycloakForAuthentication.md)
already made Keycloak the source of truth for credentials and email. Writing these flows in our APIs would mean
security-critical code (tokens in emails, expiry, single use) next to a provider that has them.

## Decision

The whole account lifecycle is configured in the realm ([eshop-realm.json](../../keycloak/realms/eshop-realm.json)),
not coded:

| Need | Realm setting |
| --- | --- |
| Sign-up on the Keycloak page | `registrationAllowed`; the email is the user name (`registrationEmailAsUsername`, `loginWithEmailAllowed`) |
| Unique email, case-insensitive | `duplicateEmailsAllowed: false`; Keycloak stores emails in lower case |
| Email confirmation before the first sign-in | `verifyEmail: true`; resend from the "Verify your email" page |
| Password reset | `resetPasswordAllowed`; the same single-use action tokens |
| Link expiry | `actionTokenGeneratedByUserLifespan: 1800` (30 min, default 5) |
| Password policy | `length(8) and notUsername and notEmail` |
| Sign-in brute force | `bruteForceProtected`, 5 failures, wait from 60 s up to 15 min |
| Email delivery (development) | `smtpServer` → Mailpit (`mailpit:1025`, inbox http://localhost:8025) |

The password is not on the registration form: with `verifyEmail` Keycloak 26 asks for it after the link in the email
is opened (`UPDATE_PASSWORD`; the option "Always set password on register form" of the "Password Validation" step
is off and deprecated). Only someone who reads the mailbox can set the password. Guest checkout (#45) reuses this:
a user created without a password sets it through the link.

Unconfirmed users cannot sign in at all (the issue asked for "sign in, but no orders"). Every token therefore carries
`email_verified: true`, so the APIs do not check it.

## Consequences

- No account code of our own; email and name are read from the token (`email`, `given_name`, `family_name`).
- Accepted limitation: the registration form answers "Email already exists", so a registered email can be detected
  through sign-up. Hiding it needs a custom Keycloak extension (Java SPI) or our own sign-up form through the Admin API;
  neither is worth it here. Password reset does not reveal whether an account exists.
- Accepted limitation: Keycloak limits failed sign-ins, not sign-up or reset requests. `AddRateLimiter` protects only
  our APIs; limits for Keycloak's pages belong to the reverse proxy in front of it (#59).
- Development users need passwords that pass the policy: the realm import fails on a weaker one
  (`invalidPasswordMinLengthMessage`, Keycloak does not start): `admin`/`admin1234`, `customer`/`customer1234`.
- The realm file is imported only when the realm does not exist: changing these settings locally needs the realm
  deleted (`kcadm.sh delete realms/eshop`, see [Authentication](../Learning/Authentication.md)); users get new `sub` values.
- Open (#59): the server realm without development users, a real SMTP server with credentials outside Git, and how
  realm changes reach an existing realm (a second file or a tool such as `keycloak-config-cli`).
