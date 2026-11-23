# Google Wallet Stamps

Google Wallet Stamps projects the existing Stamps domain into a tenant-specific
Google `LoyaltyClass` and one stable `LoyaltyObject` per loyalty card. The Stamps
ledger, cycle, historical program version and earned rewards remain the source of
truth; Wallet data is a replaceable post-commit projection.

## Visual representation

The Google object includes native stamp count and reward text fields. Its hero
image is rendered by the same `StampStripRenderer` used by Apple Wallet, including
the configured filled/empty artwork and logical reward positions. The image is
served from an HTTPS endpoint with an opaque, tamper-resistant Data Protection
token and a cycle/progress version query. The endpoint revalidates the operational
tenant and renders the current projection, without exposing database identifiers
or storing generated images.

Apple controls the exact pass layout. Google Wallet controls parts of the final
card composition, spacing and cropping, so pixel-identical layout is not possible.
The shared rendered strip provides the closest supported visual parity while the
native text fields preserve accessible progress and reward information.

## Identity and lifecycle

- Class IDs include issuer, Stamps type and full tenant ID.
- Object IDs include issuer, Stamps type, tenant fragment and normalized card
  serial; they do not include cycle IDs and therefore remain stable through
  rollover.
- Creating a Save Link creates or reuses `MemberDigitalWallet` for Google.
- Earn, reversal and redemption fan out after the economic commit. Apple and
  Google are attempted independently; provider failures never roll back Stamps.
- Each Google update is followed by a fresh projection read. If the cycle changes
  while the remote PATCH is in flight, the service patches the newest projection
  again (up to three stabilization attempts), preventing an older completion from
  becoming the final remote state.
- Automatic updates only target an existing Google wallet. Opening a Save Link
  also performs a full repair/reconciliation from the current projection.
- A failed Google update is persisted as `Error`; a later sync or Save Link retries
  the same deterministic object and returns it to `Active` on success.

## Caching and operations

Progress images use `Cache-Control: no-store`; their URL version is derived from
the cycle plus a SHA-256 digest of the actual rendered PNG, so artwork, reward
state and background changes produce a different URL. Data Protection keys must
be shared and durable across API instances so Google can resolve tokens produced
by any instance. The
configured Google logo URL supplies only the public HTTPS origin; localhost and
non-HTTPS origins are rejected.

Azure App Service persists the default key ring in
`%HOME%\ASP.NET\DataProtection-Keys` and shares it between instances in the same
deployment slot. Slots do not share this ring. If STG uses slot swaps or multiple
apps to serve the same URLs, configure an external shared key ring (Azure Blob,
Key Vault-backed encryption, SQL or Redis) before issuing passes.

The current delivery is best-effort after commit. It has repair-on-Save-Link and
persisted error state, but no durable outbox worker. If guaranteed background
delivery during extended Google outages becomes a requirement, add an outbox/job
without moving the provider call into the economic transaction.

## Deployment verification

Before enabling in STG or production, verify the issuer/service account can manage
the generated tenant Stamps classes, the public logo and progress endpoints are
reachable by Google, Data Protection keys are shared, and the final pass is tested
on a physical Android device for zero, partial, completed and rollover states.
