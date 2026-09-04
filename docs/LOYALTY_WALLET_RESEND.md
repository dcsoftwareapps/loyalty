# Loyalty Wallet branding and transactional email

Normal membership cards (not Gift Cards) use `loyaltyClass.heroImage` for the
existing tenant cover image and `hexBackgroundColor` for the wallet background.
The public tenant-scoped PNG endpoint serves the existing 1125x432 cover asset.
Google renders this as a full-width banner; Apple retains its own rendering.
Only public HTTPS origins are accepted by the mapper. Missing cover images omit
the banner. Confirm public reachability from the configured API origin before release.

Saving wallet branding or uploading a wallet logo/cover refreshes existing Google
classes sequentially. Classes referenced by another tenant are deliberately skipped
and logged for remediation; they must not be patched across tenant boundaries.
No membership objects, points, tiers, IDs, or Gift Cards are mutated by this refresh.
Google failures are best effort and do not undo the saved local design.

## Resend SMTP

The central `ITransactionalEmailSender` remains `SmtpEmailSender` (MailKit), shared
by Billing and Gift Card delivery. There is no Resend HTTP implementation.
Configure these externally in environment/User Secrets:

- `Email__SmtpHost`: `smtp.resend.com`
- `Email__SmtpPort`: `465` (implicit TLS), or `587` (required STARTTLS)
- `Email__Username`: `resend`
- `Email__Password`: the Resend API key; never store it in the repository or DB.

Use a verified sender domain and a public canonical HTTPS application URL.
The Admin displays the effective SMTP provider and credential presence only.
Existing Cloudflare host/credentials remain supported and explicitly labeled legacy;
deployments are not silently switched by this change. Selecting a provider in the DB
cannot change the actual transport. No migration or external configuration is applied.

References: https://developers.google.com/wallet/reference/rest/v1/loyaltyclass
and https://resend.com/docs/send-with-smtp.
