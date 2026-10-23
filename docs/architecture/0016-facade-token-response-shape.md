# 0016. AuthFacade returns its own token response shape on login

- **Status:** Accepted
- **Date:** 2026-10-24
- **Supersedes:** point 1 of [0013](0013-pass-openemr-tokens-through.md) (login returns OpenEMR's token response unchanged)

## Context

ADR 0013 made `POST /connect/token` return OpenEMR's status and body unchanged. That made clients depend on OpenEMR's response format: a field OpenEMR adds, renames or removes changed AuthFacade's public contract. Clients also received OpenEMR's error bodies, so they saw two error shapes, one from OpenEMR and one from AuthFacade. No clients depend on the passthrough yet, so the shape can change now.

## Decision

1. **A successful login returns 200 with AuthFacade's token response.** The body has four fields, named in snake case:
   - `access_token`, the OpenEMR access token, verified before it is returned;
   - `token_type`, normally `Bearer`;
   - `expires_in`, in seconds, when OpenEMR returns it;
   - `refresh_token`, when OpenEMR issues one.

   `expires_in` and `refresh_token` are omitted when unknown. Other fields in OpenEMR's response are dropped.
2. **A refused login returns 400 `invalid_grant`.** OpenEMR's body is not forwarded. The body is `{"error":"invalid_grant","error_description":"The username or password is incorrect."}`. OpenEMR's 400 for a wrong password is the RFC 6749 status for `invalid_grant`, so no status is mapped.
3. **An OpenEMR failure returns 502 `server_error`.** That covers an OpenEMR 5xx, an OpenEMR 408, a success response without a usable `access_token`, and a token that does not verify. Before this ADR, an OpenEMR 5xx was passed through as the same status. It is now 502, so clients see one code for an upstream fault.
4. **The other outcomes are unchanged.** An unreachable OpenEMR returns 503 `server_error`. Unsupported grant types and invalid fields return 400, as before.
5. **Parsing happens in Infrastructure.** The identity provider reads the four fields into an `OpenEmrToken` value. The application handler sees that value and never the raw body. A missing `token_type` defaults to `Bearer`.

Points 2 to 4 of ADR 0013 still hold: AuthFacade issues no tokens, bearer validation accepts OpenEMR's tokens, and the token exchange grant stays removed.

## Consequences

- AuthFacade's login contract is fixed in its own types. An OpenEMR change that adds or renames fields does not reach clients unless AuthFacade chooses to expose it.
- Clients that read OpenEMR's fields beyond the four above, or that read OpenEMR's error body, must change. No clients depend on the passthrough today.
- Clients see one error shape from the login endpoint, and no upstream error text.
- The mapping of upstream status codes is now a deliberate rule. A new OpenEMR status falls into the 400 `invalid_grant` case if it is a 4xx other than 408, and into 502 if it is a 5xx.
- The circuit breaker on the identity provider does not count a 4xx refusal as a failure. See [0017](0017-circuit-breakers-guard-openemr-calls.md) for what it counts.

## Alternatives considered

- **Keep the passthrough (ADR 0013, point 1).** Rejected. It couples clients to OpenEMR's format and leaks upstream error text.
- **Map OpenEMR's rejections to 401 `invalid_grant`.** Rejected. RFC 6749 uses 400 for `invalid_grant`, and OpenEMR already returns 400 for a wrong password, so the status matches what clients already see.
