# 0082. Patient searches are bounded pages, and text typed into a search is matched as typed

- **Status:** Accepted
- **Date:** 2026-11-18
- **Builds on:** [0079](0079-clinical-care-serves-read-only-fhir-for-five-resources.md) (the paging shape Clinical Care's FHIR searches already use)
- **Found by:** an audit of how both services pass values to SQL (Nov 18)

## Context

The audit found that every value reaches the database as a bound parameter in both services, so there is no SQL injection path. It also found two weaknesses in Patient Management's patient search, which backs `GET /fhir/Patient` and the plain `GET /api/v1/patients`:

- **It was unbounded.** The query had no `ORDER BY`, no `LIMIT` and no paging. A search with no parameters, or `family=%`, loaded every patient in the database, mapped each one and returned one Bundle. Clinical Care's FHIR searches already cap a page at 100.
- **`LIKE` text was not escaped.** A bound value cannot change the statement, but inside a `LIKE` a `%` or `_` is a wildcard, so text a caller typed could match far more than it said. Together with the first point it made the unbounded read trivial to trigger.

## Decision

1. **Every search is one bounded page.** `GET /fhir/Patient` takes `_count` (1 to 100, default 25) and `_offset` (0 or more). A value that is not a whole number in range is refused with a 400 `OperationOutcome`, not quietly replaced. The Bundle's `total` is the number of patients that matched, and it carries `self` and `next` links, as Clinical Care's do.
2. **The order is total and stable.** The page is ordered by `pid`, which is unique, so walking the `next` links neither repeats nor skips a patient. (Ids here are not handed out in registration order, so the order is by `pid`, not by when a patient was registered.)
3. **The count and the page share one filter.** The filter text is built once, from literal column names and numbered placeholders, and used for both the `COUNT(*)` and the page, so the total always describes the same set. Limit and offset are bound parameters too.
4. **The plain list is bounded the same way**, with `limit` (1 to 100) and `offset`, defaulting to 100 patients, and the total in an `X-Total-Count` response header. This is the endpoint the WebApp's patient list calls, so the list now says "Showing the first 100 patients. Search by name to find someone who is not listed." when it is full. The list still pages on the client; paging it on the server is a later change.
5. **`LIKE` text is matched as typed.** `%`, `_` and the escape character are escaped, and each `LIKE` says `ESCAPE '!'` (an explicit character, so the result does not depend on the server's backslash setting).

## Consequences

- A caller that relied on `GET /fhir/Patient` returning every patient now receives 25 and must follow `next`. No client in this repository did.
- The WebApp patient list shows at most 100 patients until it pages on the server.
- Tests send hostile text (quotes, `DROP TABLE`, `UNION`, `SLEEP`) through nine search parameters and assert that it matches nothing and that a known patient is still found afterwards; a mutation check showed the wildcard and paging tests fail with their fix removed.

## Not decided here, on purpose

- **Unknown search parameters are still ignored** by this service, so `?bogus=1` returns the first page of everyone. Clinical Care answers 400. Changing it is a behaviour change for any client that sends extras, and wants its own decision.
- **An unparseable `_lastUpdated` or `birthdate` is still dropped silently** rather than refused, for the same reason.
