# Security Policy

OpenEMR Next is an independent project in architecture and discovery. It is
not approved for production clinical use. Security reports are still handled
as confidentially and urgently because future components may process clinical,
identity, financial, or operationally sensitive data.

## Reporting a vulnerability

Do not open a public issue, discussion, pull request, or social-media post for a
suspected vulnerability. Do not include credentials, patient information,
production data, exploit code, or detailed reproduction steps in a public
channel.

Submit reports through GitHub's private vulnerability reporting feature on this
repository: open the repository's **Security** tab and choose **Report a
vulnerability**. If private reporting is not enabled, notify the repository
maintainers through a private channel listed in the repository profile and ask
for a private security-reporting channel. Do not send sensitive information to
an unverified address.

A useful report includes:

- A concise description and affected component or commit.
- The security impact and, where known, affected data or workflows.
- Reproduction steps using synthetic data only.
- Preconditions, required privileges, and a minimal proof of concept.
- Any known mitigation or proposed fix.
- Whether the report is being shared with another party or is subject to a
  coordinated-disclosure deadline.

If a report contains personal, clinical, financial, credential, or secret data,
state that fact without attaching the data. The maintainers will provide a
secure transfer method if the data is necessary for investigation.

## Triage and response

The security maintainers will acknowledge a report within five business days
and provide an initial severity and impact assessment within ten business
days. These are response targets, not a promise that investigation or
remediation will be complete within those periods.

Triage will consider:

- Confidentiality, integrity, and availability impact.
- Exposure of patient, identity, financial, or audit data.
- Exploitability and required access.
- Impact on clinical continuity or safety.
- Affected versions, deployment modes, and legacy compatibility boundaries.

The maintainers may request additional information, create a private tracking
issue, involve security or clinical-safety reviewers, and coordinate with
integrators or affected upstream projects. Reporters will be kept informed of
material status changes when that can be done without increasing risk.

## Coordinated disclosure

The project will coordinate disclosure with the reporter and affected
maintainers. The disclosure date will be agreed based on remediation status,
exploitability, active exploitation, downstream exposure, and clinical or
privacy risk. A typical target is disclosure within 90 days, but a shorter or
longer period may be appropriate.

Before disclosure, the maintainers will seek to:

- Confirm the affected versions and severity.
- Prepare a fix or documented mitigation.
- Assess whether credentials, keys, or personal data require rotation or
  notification.
- Notify downstream users or integrators who need time to protect themselves.
- Publish a security advisory with affected versions, fixes, mitigations, and
  credit to the reporter if requested.

The project will not publicly disclose a report while it contains usable
exploit details, real sensitive data, active credentials, or information that
could create unacceptable clinical or privacy risk. Active exploitation may
require emergency mitigation and accelerated notification.

## Reporter protections

Good-faith security research is welcome when it avoids accessing, modifying,
or retaining data that does not belong to the researcher; avoids service
disruption or degradation; and stops testing and reports the issue once
sufficient evidence exists. The project will not pursue legal action for
research that follows this policy, subject to applicable law and third-party
terms.

Researchers must not test against production clinical systems or real patient
data. Use a local environment, a project-provided test environment, or
synthetic data.

## Supported versions

No production release is currently supported. During the pre-production phase,
security fixes are evaluated against the default branch and any explicitly
published release or pilot version. Once releases exist, this section will list
the supported versions and their security-maintenance windows.

## Secrets and urgent exposure

If a credential, private key, token, patient record, or other secret is exposed,
report it privately immediately and do not commit it to the repository. The
maintainers will assess revocation, rotation, history removal, affected-system
notification, and any legal or privacy obligations.

## Policy changes

Changes to this policy require maintainer review. Security-sensitive changes,
including reporting-channel or disclosure-process changes, should be reviewed
through a private security discussion before publication.
