# 0083. Times in the WebApp are UTC

- **Status:** Accepted
- **Date:** 2026-11-18
- **Amends:** [0067](0067-webapp-covers-encounters-and-vitals.md) ("Dates cross a zone boundary")
- **Decided by:** the project owner, after the first browser check found the problem

## Context

In the first real browser check, a user in Cairo (UTC+2) opened the New encounter dialog, which proposed **06:30** (the browser's local time), and created the encounter. The page then showed **04:30**.

The cause was two conventions in one app. The encounter dialog built `new DateTimeOffset(local date and time)`, which attaches the *browser's* offset. Clinical Care converts a date sent with an offset to its own clock (review finding R-11), which is UTC, and stores those digits. The pages show the stored digits without converting back (deliberately: `Formatting` says so). So a time was entered in one zone and displayed in another. The issue screens (problems and allergies) already did the opposite: they sent wall-clock digits with a zero offset, so what was typed was what was stored.

## Decision

1. **Every time in the WebApp is UTC.** A default taken from the clock is the UTC clock (`TimeProvider`, injected, so a test can pin it). A time typed into a dialog is read as UTC and sent with a zero offset (`UtcTime.ToWire`), so the stored digits are the typed digits whatever zone the browser is in. A time on a page is shown as stored, with a ` UTC` suffix. The time pickers are labelled "Time (UTC)".
2. **Date-only values go the same way.** Date of birth, policy dates, onset and effective dates are sent as midnight at a zero offset, so a browser ahead of or behind UTC can never move one by a day. "Today", for the date pickers' limits and the age shown on the patient page, is the UTC date.
3. **There is one helper.** `UtcTime` has `Today` and `ToWire`; `IssueWire` uses it too.

## Consequences

- **It depends on the stack's clocks being UTC, and today they are:** the database server, the Clinical Care and Patient Management containers, and legacy OpenEMR's PHP default (its `gbl_time_zone` setting is empty) are all UTC. If the services ran on a host in another zone, Clinical Care would convert the zero-offset value to that zone's clock and the page would label those digits "UTC" wrongly. A deployment that moves a service off UTC must revisit this.
- **Legacy OpenEMR shows the same digits**, as it displays `form_encounter.date` as stored. A legacy user whose own timezone setting is not UTC would see the stored digits as if they were local; the setting is empty today.
- **A clinic that wants local time on screen** needs a clinic timezone setting, conversion on display and on entry, and a rule for the stored column. That is a bigger change than this one and was not chosen.
- **Tests:** the encounter dialog is opened with a clock pinned at 04:30 UTC and asserted to propose 04:30 and send 04:30 at a zero offset. Run with the machine clock set to Cairo time, the old code fails it. On a UTC machine, such as CI, the offset half of that regression cannot show, so the clock half is the part CI protects.
