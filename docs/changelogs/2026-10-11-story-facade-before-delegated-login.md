PUTTING A FACADE IN FRONT OF OPENEMR: WHAT WE TRIED BEFORE SETTLING ON DELEGATED LOGIN

Today started with a simple question: how should a new service authenticate users who already have accounts in OpenEMR? It ended with a working facade, a clearer picture of the boundaries that matter in a legacy modernization, and a list of things we should have decided earlier. This is the story of how we got there, and why.

STARTING POINT

OpenEMR is a large PHP application with its own user table (users), password hashes (users_secure), and login rules: multi-factor authentication, lockout, and password expiry. We wanted new services in a separate project, and they needed to know who the user was without giving them a second login to maintain.

The first instinct was to build a proper identity server, so we started with OpenIddict, a mature .NET library for OAuth2 and OpenID Connect.

ATTEMPT ONE: OPENIDDICT WITH ITS DATABASE STORES

OpenIddict is built around a database. Its server keeps applications (clients), authorizations, and tokens in tables. The default setup wires those tables through Entity Framework Core, and the server won't start without them.

That collided with a constraint we had set for ourselves: don't add tables to the OpenEMR database. We tried to satisfy both. We removed the stores and got "the core services must be registered." We turned on degraded mode, and the server still called store code during token creation. We disabled token storage, and it failed again in a different place.

Each workaround moved the failure one step down the stack. The lesson was not that OpenIddict is wrong. It is designed around its database store, and every attempt to run it without that store fought the design. When a library's assumptions conflict with your constraints, that is a signal to reconsider the constraint or the library, and we had been treating the constraint as fixed.

We also had to face a second question: was the constraint about avoiding breakage, or about avoiding new tables in general? Those are different. New tables with distinct names don't break existing tables. The real risks are migrations that alter legacy tables, name collisions, and foreign keys added to legacy tables. Once we separated those concerns, the "no new tables" rule looked less like a technical necessity and more like a policy choice, which is a valid choice but should be stated as one.

ATTEMPT TWO: READING OPENEMR'S USERS TABLE DIRECTLY

If OpenIddict needed tables, perhaps the facade could skip tokens entirely and check passwords itself. We wrote an authenticator that read users and users_secure, verified the bcrypt hash, and issued its own JWT.

It worked, and it was the wrong design. Verifying the hash in our service copied OpenEMR's login logic without its MFA, lockout, or password-expiry rules. Any of those rules that changed in OpenEMR would silently stop applying to our service. It also created a second place where credentials lived, which could drift from OpenEMR's view of the same user.

The architectural principle here is simple. Authentication rules belong with the system that owns the users. If you copy the check, you inherit the job of keeping the copy correct, and you will eventually miss a rule.

ATTEMPT THREE: DELEGATING TO OPENEMR'S OAUTH2 SERVER

OpenEMR ships its own OAuth2 server with a password grant. The facade could forward the username and password to that endpoint, along with a registered client ID and secret, and let OpenEMR decide. This was the design we kept.

The first version returned OpenEMR's token to the client unchanged. That worked, but it exposed OpenEMR's token format and signing keys to every consumer of the facade. We then tried to have the facade validate OpenEMR's tokens on protected endpoints. That meant fetching OpenEMR's JSON Web Key Set, dealing with its self-signed development certificate inside the JWT handler's back channel, and matching key identifiers. We chased "signature key not found" through several configurations before deciding the approach was too fragile for a development facade.

The final design issues a facade-owned token after OpenEMR accepts the login:

The client posts a username and password to the facade.
The facade forwards them to OpenEMR's token endpoint with its own client credentials.
If OpenEMR accepts, the facade reads the subject from OpenEMR's token and signs its own short-lived JWT.
Protected endpoints validate only the facade's token, using a single HMAC key.

OpenEMR stays the authority for who the user is and whether the login succeeds. The facade becomes the authority for what clients present on later requests. That split is the core of the design, and it is the part worth keeping.

WHY THE FACADE PATTERN FITS HERE

The facade gives consumers one login endpoint and one token format. It hides OpenEMR's OAuth2 endpoints, client registration, and certificate details behind one adapter class, OpenEmrAuthClient. Feature code depends on the facade's behavior, not on OpenEMR's API.

That matters for modernization. OpenEMR stays a separate, unmodified deployment that can be upgraded on its own schedule. If OpenEMR changes its token endpoint or its scope names, we change one adapter. If we later move identity out of OpenEMR, the facade's contract to its clients stays the same, and only the backend changes.

The facade does not make OpenEMR's internals go away. Every login still depends on OpenEMR being up, and the facade inherits that dependency. We accepted that tradeoff and recorded it.

REGISTRATION: THE PART WE SHOULD HAVE AUTOMATED FIRST

Registering the facade as an OAuth client in OpenEMR was the most error-prone step, and it consumed more time than any code.

The registration form, when a system scope was selected, asked for a JSON Web Key Set and submitted it as a string, not an array. That blocked us until we removed all system scopes. Clients in this version are disabled until an administrator enables them. And client secrets are stored as hashes, so a lost secret can't be recovered, only regenerated.

Each of those issues cost a round trip. The conclusion is that registration should be scripted, and the script should generate and store the secret in one step so nothing is ever lost or pasted. We did not build that today. Our plan is to call OpenEMR's own client-creation code inside its container rather than insert rows into oauth_clients, because secret hashing and scope storage vary by version, and hand-written rows look valid until they fail in confusing ways.

A related lesson came from debugging. We spent a long stretch testing with client_credentials, which OpenEMR's client was never configured for. The request was malformed for that grant, and the errors pointed at the wrong problem. Testing the exact grant the facade uses, against OpenEMR directly, was the faster path. Test the path you ship, with the same parameters, before testing a path you think is similar.

DEVELOPMENT ENVIRONMENT: WHAT THE STACK TAUGHT US

Much of the day was spent on the OpenEMR development stack, and most of that time went to setup, not architecture. A few points generalize.

The development-easy image is designed for OpenEMR's own CI. It installs well over a hundred packages, copies tens of thousands of files from a Windows bind mount, and runs composer and a full install on first boot. For testing an API integration, that was far more than we needed. Switching to the pinned release image removed the source mounts and most of the wait. The first install is still slow, and snapshots or a pre-installed image are the next step for speeding up the loop.

Bind mounts on Windows deserve caution. Docker created empty directories where it expected files, which broke both MariaDB's TLS setup and CouchDB's configuration. A missing source file is not an error for a bind mount on Docker Desktop. It becomes a directory. We now treat bind mounts as a common source of confusion and verify the host paths before starting a stack.

Port ownership on Windows also caused misleading results. localhost resolved to IPv6 in one case, and a stale process held that address. Requests reached the wrong listener, and the symptoms (empty replies, unsupported media type) looked like application bugs. The fix was to check which process owned the port before debugging code. Check the environment before you blame the code.

Finally, debugging was hampered by credentials pasted into logs and chat. Several passwords and tokens were exposed during the session. We treated them as compromised and noted rotation in the changelog. The better practice is to prompt for secrets (Read-Host -AsSecureString) and to print only status codes and property names when sharing output.

CONCLUSIONS

Delegate authentication to the system that owns the users. Copying login logic creates a second system to maintain, and the copy will miss rules over time.

Separate authentication from authorization tokens. OpenEMR decides whether a login is valid. The facade decides what a client presents afterward. Clients should not carry OpenEMR's tokens, and the facade should not require clients to understand OpenEMR's key management.

Choose libraries that match your constraints, or change the constraints deliberately. OpenIddict was the right tool for a different architecture. Forcing it into a database-free design cost more than the constraint was worth. If we need full OAuth2 with refresh tokens and revocation later, the honest path is to give OpenIddict its own database, separate from OpenEMR's.

Automate the steps humans get wrong. Client registration, secret generation, and environment setup were all manual, and all produced failures. Anything you will repeat on every machine should be a script.

Verify the path you ship. Test the exact grant, parameters, and endpoints the code will use, against the real dependency, before adding layers on top.

Know your dependency's costs. Every login now depends on OpenEMR being available. We should decide how the facade behaves when OpenEMR is down, and we currently return a 503 with a clear message. A cache, a degraded mode, or a queue are options for later, but they are product decisions, not just code.

WHAT'S NEXT

The facade works end to end in both the host and container setups. The next steps are a bootstrap script for client registration, refresh tokens and a proper key store for the facade's signing key, and an authorization layer for the endpoints that call OpenEMR's API. Each of those has its own design questions, and we'll write them up as they come.

If there is one takeaway from today, it is this: the hardest parts of this integration were not the code. They were the boundaries (who owns users, who issues tokens, who owns registration) and the environment around them. Settle those first, and the code gets much simpler.