# Billing vocabulary inventory

This is a representative vocabulary inventory from the legacy fee-sheet,
Billing Manager/payment screens, modern `src/Billing` classes, reports, and
patient-card UI. It preserves the terms used by the code and labels; it does
not propose replacement names.

| Legacy term | Where it appears | Meaning in context | Synonyms used elsewhere |
|---|---|---|---|
| Fee Sheet | [`FeeSheet.class.php`](../library/FeeSheet.class.php), [`FeeSheetHtml.class.php`](../library/FeeSheetHtml.class.php), [`printed_fee_sheet.php`](../interface/patient_file/printed_fee_sheet.php) | Encounter-oriented structure for service/product lines, codes, units, prices, modifiers, diagnoses, provider, payer, and copay. | Superbill, printed fee sheet, fee-sheet items/options |
| Superbill | [`printed_fee_sheet.php`](../interface/patient_file/printed_fee_sheet.php) | Printable list of billing codes/categories assembled from fee-sheet options, the `superbill` list, active codes, and products. | Printed fee sheet, Superbill/Fee Sheet |
| Billing / billing row | [`BillingUtilities.php`](../src/Billing/BillingUtilities.php), [`FeeSheet.class.php`](../library/FeeSheet.class.php) | Persisted charge/service record associated with a patient and encounter; also the broader workflow/UI area. | Charge, service line, line item, bill, claim (related but not equivalent) |
| Charge | [`FeeSheet.class.php`](../library/FeeSheet.class.php), [`InvoiceSummary.php`](../src/Billing/InvoiceSummary.php), [`payment_pat_sel.inc.php`](../interface/billing/payment_pat_sel.inc.php) | Billable service/product amount or line. | Fee, billed amount, service charge, line item |
| Fee | [`FeeSheet.class.php`](../library/FeeSheet.class.php), [`InvoiceSummary.php`](../src/Billing/InvoiceSummary.php) | Monetary value of a line; unit price is multiplied by units before storage. | Price, charge, amount |
| Service code / code type / procedure | [`payment_pat_sel.inc.php`](../interface/billing/payment_pat_sel.inc.php), [`fee_sheet_justify.php`](../interface/forms/fee_sheet/review/fee_sheet_justify.php) | Coded clinical or product item used in billing, including CPT/HCPCS and configured code families. | Procedure, billing code, CPT, HCPCS, product |
| Unit price / price level | [`FeeSheet.class.php`](../library/FeeSheet.class.php), [`FeeSheetHtml.class.php`](../library/FeeSheetHtml.class.php) | Code/product price selection controlled by a price level or fee schedule. | Price, fee schedule, standard price, `pr_level`, `pricelevel` |
| Copay | [`FeeSheet.class.php`](../library/FeeSheet.class.php), [`billing.html.twig`](../templates/patient/card/billing.html.twig), [`payment_pat_sel.inc.php`](../interface/billing/payment_pat_sel.inc.php) | Patient cost-sharing amount; may be represented as a `COPAY` billing line or displayed as insurance-related data. | Patient responsibility, cost share, patient payment (not always equivalent) |
| Payment | [`payment_master.inc.php`](../interface/billing/payment_master.inc.php), [`InvoiceSummary.php`](../src/Billing/InvoiceSummary.php), [`print_daysheet_report_num1.php`](../interface/billing/print_daysheet_report_num1.php) | Money received and posted to accounts receivable, with method, source, category, and distribution. | Patient payment, insurance payment, receipt, posted payment |
| Adjustment / Adj | [`payment_pat_sel.inc.php`](../interface/billing/payment_pat_sel.inc.php), [`InvoiceSummary.php`](../src/Billing/InvoiceSummary.php) | Amount reducing or modifying a balance, often associated with payer adjudication or payment posting. | Adjustment amount, contractual adjustment, `adj_amount`, `adj` |
| Unapplied / undistributed | [`payment_master.inc.php`](../interface/billing/payment_master.inc.php), [`billing.html.twig`](../templates/patient/card/billing.html.twig), [`PrepaymentBalance.php`](../src/Billing/PrepaymentBalance.php) | Money received but not allocated to an encounter/charge, or a patient prepayment not yet applied. | Unapplied, unallocated, undistributed, `global_amount` |
| Paying Entity / Payment From | [`payment_master.inc.php`](../interface/billing/payment_master.inc.php) | Source/category of posted money, such as patient or insurance. | Payer, payor, insurance company, patient, `payment_type` |
| Payer / Payor | [`BillingClaim.php`](../src/Billing/BillingProcessor/BillingClaim.php), [`payment_master.inc.php`](../interface/billing/payment_master.inc.php) | Financial party responsible for claim/payment activity. The code and UI use both spellings. | Insurance company, payer, payor, `payor_id`, `payor_type` |
| Primary / Secondary / Tertiary | [`BillingClaim.php`](../src/Billing/BillingProcessor/BillingClaim.php), [`payment_pat_sel.inc.php`](../interface/billing/payment_pat_sel.inc.php) | Insurance sequence or payer level. | Insurance level, payer level, `payer_type`, `last_level_billed`, `last_level_closed` |
| Claim | [`BillingClaim.php`](../src/Billing/BillingProcessor/BillingClaim.php), [`BillingUtilities.php`](../src/Billing/BillingUtilities.php) | Payer-facing grouping of charges for a patient/encounter and payer level. | Insurance claim, claim row, claim batch; not the same as one charge |
| Billing queue / Billing Manager | [`billing_process.php`](../interface/billing/billing_process.php), [`BillingClaim.php`](../src/Billing/BillingProcessor/BillingClaim.php) | Batch workflow selecting claims, generating files/forms, tracking processing, and marking billing state. | Claim processing, billing process, claim queue, EDI/X12 processing |
| EOB / ERA posting | [`billing_process.php`](../interface/billing/billing_process.php), [`new_payment.php`](../interface/billing/new_payment.php), [`InvoiceSummary.php`](../src/Billing/InvoiceSummary.php) | Remittance/payment processing from a payer. | ERA, EOB, remittance, insurance payment, adjudication |
| Invoice / invoice reference | [`InvoiceSummary.php`](../src/Billing/InvoiceSummary.php), [`FeeSheet.class.php`](../library/FeeSheet.class.php) | Encounter-level financial summary or reference number for charges, payments, adjustments, and balance. | Invoice summary, account balance, `invoice_refno`, `ar_activity` |
| A/R / accounts receivable | [`FeeSheet.class.php`](../library/FeeSheet.class.php), [`SLEOB.php`](../src/Billing/SLEOB.php), [`InvoiceSummary.php`](../src/Billing/InvoiceSummary.php) | Financial ledger state for charges, payments, adjustments, payer responsibility, and patient responsibility. | Integrated A/R, accounting, ledger, `ar_session`, `ar_activity` |
| Day sheet | [`BillRow.php`](../src/Billing/DaySheet/BillRow.php), [`print_daysheet_report_num1.php`](../interface/billing/print_daysheet_report_num1.php) | Operational report aggregating charges, insurance/patient payments, adjustments, users, and providers by date range. | Billing report, daily billing report, daysheet |
| Insurance provider | [`FeeSheet.class.php`](../library/FeeSheet.class.php) | In fee-sheet data, `insurance_data.provider` identifies an insurance company. | Insurance company, payer/payor; not the same as clinician `provider_id` |
| Billed / billing status | [`BillingUtilities.php`](../src/Billing/BillingUtilities.php), [`FeeSheet.class.php`](../library/FeeSheet.class.php) | Status of charge/product rows and encounter-level billing completion. | Billed, unbilled, billing state, closed/reopened |
| Close / reopen | [`FeeSheet.class.php`](../library/FeeSheet.class.php), [`BillingUtilities.php`](../src/Billing/BillingUtilities.php) | Closing marks eligible charges/products billed and may assign an invoice reference; reopening reverses billing flags/state. | Close visit, reopen encounter, billing close/reopen |
| Billing note / balance | [`billing.html.twig`](../templates/patient/card/billing.html.twig) | Patient-card display concepts for patient balance, insurance balance, total balance, collection balance, and billing note. | Patient balance, insurance balance, total balance, collection balance |

## Terms with different meanings across areas

### Encounter

- In [`FeeSheet.class.php`](../library/FeeSheet.class.php), `encounter` is the
  keyed clinical record associated with the patient.
- In [`BillingClaim.php`](../src/Billing/BillingProcessor/BillingClaim.php), it
  participates in the composite claim identity `pid-encounter`.
- In [`payment_pat_sel.inc.php`](../interface/billing/payment_pat_sel.inc.php),
  it is the charge/account context to which a payment is distributed.
- In [`edih_view.php`](../interface/billing/edih_view.php), it is an EDI search
  criterion.

The same term therefore shifts between clinical record identity, claim
identity, payment allocation target, and report/search key.

### Provider

- In [`FeeSheet.class.php`](../library/FeeSheet.class.php), `provider_id`
  comes from `form_encounter.provider_id`, with fallbacks to an authorized
  logged-in user and patient demographics.
- In [`FeeSheetHtml.class.php`](../library/FeeSheetHtml.class.php), authorized
  users or users whose additional information identifies them as providers can
  appear in the provider selector.
- In [`Claim.php`](../src/Billing/Claim.php), provider roles include billing,
  rendering, referring, and ordering provider.
- In [`print_daysheet_report_num1.php`](../interface/billing/print_daysheet_report_num1.php),
  provider is a reporting dimension separate from user.

Provider can therefore mean encounter clinician, provider-capable account,
claim-role provider, or report grouping.

### User

- In [`FeeSheet.class.php`](../library/FeeSheet.class.php), user commonly means
  the authenticated operator/poster (`authUser`, `authUserID`, or `user_id`).
- In [`BillRow.php`](../src/Billing/DaySheet/BillRow.php), `$user` is a
  reporting field distinct from `providerId`.
- In [`FeeSheetHtml.class.php`](../library/FeeSheetHtml.class.php), a `users`
  row may qualify as a provider based on authorization or metadata.
- In [`payment_master.inc.php`](../interface/billing/payment_master.inc.php),
  posting user and paying entity are different fields.

User is not synonymous with provider or payer.

### Visit

- [`FeeSheet.class.php`](../library/FeeSheet.class.php) uses `visit_date` for
  the date of the encounter and comments use “visit” language.
- [`printed_fee_sheet.php`](../interface/patient_file/printed_fee_sheet.php)
  labels “Visit date” and “Prior Visit”.
- [`MiscBillingOptions.php`](../src/Billing/MiscBillingOptions.php) uses
  “Latest Visit or Consultation” and “First Visit or Consultation”.

Visit is generally user-facing/date/clinical language; encounter is commonly
the persisted/accounting identifier. Visit can also describe a service
category rather than one specific encounter.

### Authorization

- `userauthorized` in [`FeeSheet.class.php`](../library/FeeSheet.class.php)
  affects the provider fallback/selection.
- `authorized = 1` in [`FeeSheetHtml.class.php`](../library/FeeSheetHtml.class.php)
  is one provider-selector qualification.
- `pricesAuthorized()` in [`FeeSheet.class.php`](../library/FeeSheet.class.php)
  means ACL permission to see prices, using `acct/disc` or `acct/bill`.
- A fee-sheet line has an `auth` field in [`FeeSheet.class.php`](../library/FeeSheet.class.php).
- [`BillingUtilities.php`](../src/Billing/BillingUtilities.php) uses
  authorization in the payer prior-authorization/pre-certification sense for
  remittance reason text.

These are application ACL authorization, provider eligibility, price
visibility, line-level authorization data, and payer prior authorization—not
one shared concept.

## Additional lexical observations

- Both **payer** and **payor** are used. Modern claim fields include
  `payor_id`/`payor_type`, while UI labels include “Payor ID”.
- `insurance_data.provider` identifies an insurance company in fee-sheet code,
  while `provider_id` generally identifies a clinician/provider user.
- `billed` is a status flag on charge/product rows; a claim is a payer-facing
  grouped object.
- “Close” and “reopen” alter billing flags and encounter billing/statement
  state; they are not merely UI navigation terms.

