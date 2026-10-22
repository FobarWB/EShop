---
type: decision
status: accepted
verified: 2026-10-22
issue: 48
---

# Relative API URLs behind a proxy

## Context

The Angular services called `https://localhost:500x/api/...`: the ports were duplicated in the client code, the browser
called three origins (CORS), and in containers the APIs have other addresses. Alternative: a `config.json` with the
API URLs, loaded by the client at start; it keeps the ports, CORS and direct browser calls to every API.

## Decision

The client calls relative URLs (`/api/products`); the server that delivered the page forwards them by path prefix:
the Angular dev server (`proxy.conf.json`) in development, nginx in the client container. nginx is the only service
published to the host: the single entry point of the shop.

## Consequences

- API ports live only in `launchSettings.json` and `proxy.conf.json`; the client code has no hosts or ports.
- The browser sees one origin: the client needs no CORS (the policy stays for other clients).
- A new route prefix needs an entry in `proxy.conf.json` and in `nginx.conf`.
- TLS terminates at the entry point; the APIs listen on HTTP inside the network, so `UseHttpsRedirection()` was removed.
  nginx sends `X-Forwarded-For`/`-Proto`; the APIs do not read them yet (`UseForwardedHeaders`, with HTTPS in #36).
- nginx works as a simple API gateway; a .NET gateway (YARP) from the roadmap can replace it.
