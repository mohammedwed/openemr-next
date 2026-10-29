# Consuming AuthFacade from a new host

Every host (an HTTP API, a CLI, a worker) composes AuthFacade the same way. See
[ADR 0011](../../../../docs/architecture/0011-host-neutral-composition-root.md) for why.

## 1. Reference Composition, not the API

Reference `OpenEmrNext.AuthFacade.Composition` from your host project. Do not reference
`OpenEmrNext.AuthFacade.Api`: it is an ASP.NET host, and referencing it pulls in the web
pipeline.

## 2. Register the services

```csharp
using OpenEmrNext.AuthFacade.Composition;

services.AddAuthFacade(configuration);
```

`AddAuthFacade` binds and validates the `OpenEmr` section, so the host fails at
startup when a required value is missing. It also registers the token-grant use case
(`TokenGrantHandler`, scoped).

Required settings:

| Key | Notes |
|---|---|
| `OpenEmr:BaseUrl`, `OpenEmr:ClientId`, `OpenEmr:ClientSecret` | Required. `BaseUrl` is where AuthFacade reaches OpenEMR, and `ClientId` is also the expected token audience |
| `OpenEmr:Issuer` | Required. The issuer string OpenEMR writes into its tokens, for example `https://localhost:9300/oauth2/default`. It can differ from `BaseUrl` inside a Docker network |
| `OpenEmr:Scope` | Optional, defaults to `openid api:oemr` |
| `OpenEmr:AcceptAnyServerCertificate` | Defaults to `false`. Development only, because OpenEMR's dev certificate is self-signed. The API host refuses `true` outside Development; other hosts must add the same check |

Your host supplies these however it likes: appsettings, environment variables or user secrets.

## 3. Resolve the use case inside a scope

`TokenGrantHandler` is scoped. A host with a root provider (a worker, for example) must
create a scope:

```csharp
using var scope = scopeFactory.CreateScope();
var login = scope.ServiceProvider.GetRequiredService<TokenGrantHandler>();
```

An HTTP host gets a request scope automatically.

## 4. Build the inputs with the constructors

```csharp
using OpenEmrNext.AuthFacade.Domain.ValueObjects;

try
{
    var username = new Username(rawUsername);
    var password = new Password(rawPassword);
    // Call the use case with username and password.
}
catch (ArgumentException)
{
    // Your host decides what this means: an HTTP 400, a usage message, and so on.
}
```

Each constructor rejects invalid input with an `ArgumentException`. The token endpoint checks the same rules on its request DTO first, so it never reaches these constructors with invalid input. A refresh grant builds a `RefreshToken` the same way, and calls the `HandleAsync(RefreshToken, CancellationToken)` overload instead.

## 5. Call the use case and handle all four outcomes

```csharp
using OpenEmrNext.AuthFacade.Application.Authentication;

var result = await login.HandleAsync(username, password, cancellationToken);

switch (result)
{
    case LoginSucceeded success:
        // success.Token holds the verified OpenEMR token fields: AccessToken, TokenType, ExpiresIn, RefreshToken
        break;
    case LoginRejected:
        // OpenEMR refused the credentials. Return your own error, such as 400 invalid_grant; do not forward OpenEMR's body
        break;
    case LoginUnavailable:
        // OpenEMR could not be reached
        break;
    case LoginUpstreamError:
        // OpenEMR answered, but the success response was unusable
        break;
}
```

`TokenEndpoints` in the Api project shows one complete mapping to HTTP responses. Handle all
four cases explicitly, and do not add a catch-all that hides a new outcome.

## What stays in your host

- How the inputs arrive and how outcomes are reported (HTTP, console, log, and so on).
- Any transport contract, such as request and response shapes. The Api keeps its own.
- Bearer validation and authorization, if the host serves authenticated requests. The Api
  registers these in `AddAuthFacadeHttpDependencies`.

## What not to do

- Do not read configuration in the layers. Only Composition reads it.
- Do not reference the Api project from a non-HTTP host.
- Do not log the password or the token response.
