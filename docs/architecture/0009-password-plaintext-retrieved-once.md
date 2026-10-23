# 0009. The password plaintext is retrieved once and cleared

- **Status:** Accepted
- **Date:** 2026-10-21

## Context

The password is held in memory from the request until the OpenEMR call. The longer it is held, the more exposed it is to a memory dump, a debugger, or accidental logging.

## Decision

The `Password` value object stores its characters in a `char[]`. `TakeValue()` returns the plaintext once, clears the buffer, and throws on any later call. `ToString()` returns a mask. Only the OpenEMR identity provider calls `TakeValue()`. The application handler passes the value object through without reading it.

## Consequences

- The plaintext exists in the value object only until the OpenEMR request is built.
- This limits exposure. It does not eliminate it. The incoming form string and the string built for the HTTP request are immutable .NET strings that cannot be zeroed, and they live until garbage collection.
- Eliminating those copies would mean carrying the password as bytes through the whole request path. That is not done.
