# Stamps Apple Wallet strip experiment

This is an offline visual prototype on `feature/stamps-wallet-prototype`.
It implements no loyalty rules, entities, uploads, database changes or Wallet integration.

## Reproduce

From the repository root:

```powershell
dotnet build .\src\LoyaltyCloud.Tools\LoyaltyCloud.Tools.csproj -c Release
dotnet run --project .\src\LoyaltyCloud.Tools -c Release --no-build -- stamps-wallet-prototype
```

Optional destination: `--output "C:\temp\stamps-wallet-prototype"`.
The command executes before Tools configuration/DI setup; it never reads SQL,
Key Vault, tenant configuration or certificates.

Open `artifacts/stamps-wallet-prototype/index.html` to compare samples at the
375-pixel base width. Each image links to its 3x PNG. This directory is already
ignored by Git and is not a product asset directory.

The command generates 22 samples, each with `strip.png`, `strip@2x.png` and
`strip@3x.png`, plus `metrics.csv`. Directory names identify total, progress and
layout; for example `10-stamps/05-of-10-balanced/strip@3x.png`.
All requested progress states are included. Rewards occupy the middle threshold
rounded up and the final position; for 10 stamps these are positions 5 and 10.

## Existing production pipeline

- `TenantBrandingLogoService` generates tenant banner assets with ImageSharp
  3.1.11, centered cover crop and preserved aspect ratio.
- `TenantWalletAssetProvider` loads the three assets from the tenant's
  `wallet-strip` Blob prefix only when Apple primary content mode is `Image`.
- `PassGeneratorService` reads tenant branding, requests those assets, hashes
  each asset into `manifest.json`, signs the manifest with detached PKCS#7 and
  includes the assets, manifest, signature and `pass.json` in the ZIP `.pkpass`.
- Required logo/icon assets and their current fallback behavior are unchanged.

The prototype uses the existing transitive ImageSharp dependency through Tools'
Infrastructure reference. It adds no package and changes no production services.
Neutral geometry is rasterized at 3x and resized for 1x/2x, without fonts, emoji,
logos or generated branding.

## Dimensions and Apple constraints

The existing implementation uses 375x144, 750x288 and 1125x432. These agree with
the current [Apple Wallet HIG](https://developer.apple.com/design/human-interface-guidelines/wallet)
and [Pass Designer guidance](https://developer.apple.com/documentation/walletpasses/creating-a-pass-with-pass-designer).

The strip is raster artwork, not interactive reward controls. Primary fields can
overlay it, so a future implementation needs to reserve this area appropriately.
Apple Watch may crop the strip to its own aspect ratio. These PNGs do not prove
the complete pass presentation or cross-device visibility: validate a signed
experimental pass on actual devices during Wallet integration. The V1 product
maximum has been decided below following visual review of this prototype.

## Visual comparison

Measurements below are at 1x. The slot is the allocated square; geometry occupies
86% of its width. Images at 2x/3x improve sharpness, not physical display space.

| Total | Rows | Slot / visible shape | Gap between slots | Assessment |
| --- | --- | --- | --- | --- |
| 5 | 1 | 59 / 50.7 px | 10 px | Spacious; good for recognizable custom artwork. |
| 8 | 2 | 55 / 47.3 px | 10 px | Clear states and generous room for simple assets. |
| 10 | 2 | 55 / 47.3 px | 10 px | Best practical baseline; intermediate/final rewards remain clear. |
| 12 | 2 | 47.5 / 40.9 px | 10 px | Viable with simple artwork; small details need device validation. |
| 15 | 2 | 33.1 / 28.5 px | 10 px | Dense; reward lock details and custom imagery become small. |

The last row of 15 is centered independently (8 + 7). A 3-row alternative is
included (5 + 5 + 5), but provides only 34.7-pixel slots and little improvement.
One-row alternatives for 8/10/12/15 yield 33.1/24.5/18.8/13-pixel slots;
10 and above are unsuitable for detailed tenant assets.

The 15-stamp and 3-row samples remain experimental comparisons only; they are
excluded from the V1 product design.

## Final prototype decisions - 2026-10-01

- Admin will allow a maximum of 12 stamps in V1. This is a product/UI restriction;
  the database and domain must not depend on an artificial limit of 12.
- The strip will use at most 2 rows.
- 10 stamps is a comfortable visual configuration, not a limit or mandatory default.
- 12 stamps is the maximum visual configuration permitted in V1; 15 is excluded.
- A reward occupies the same cell as its corresponding stamp. Floating or separate
  padlocks are not part of the final design.
- The tenant will upload one image per reward. LoyaltyCloud will automatically
  compose its locked and unlocked states; two distinct reward assets are not required.
- The locked state may use desaturation, reduced contrast and/or an overlay within
  the reward cell. The exact treatment will be finalized during Wallet integration.
- A completed card remains visually full until the next earn. That earn starts a
  new cycle using the version effective at that time and displays 1/N.

These decisions document future implementation behavior. This prototype does not
implement Stamps, ProgramType, cycles or integration into PassGeneratorService.

## Future assets

The composition supports the future concept of `EmptyStampImage`,
`FilledStampImage` and a `RewardImage` per goal. Asset loading is not implemented.
Current circles and diamonds are neutral placeholders, not production artwork.

One reward source image is the agreed V1 upload model. LoyaltyCloud will generate
locked/unlocked presentation from that source. Desaturation, reduced contrast
and/or an in-cell overlay are candidates for locked presentation; opacity alone
can be ambiguous for monochrome or low-contrast artwork. Test light/dark and
detailed assets to finalize the treatment during Wallet integration without
requiring a second reward asset.

The prototype uses a pale diamond + lock versus a dark diamond + check, proving
the composition/state distinction with simple geometry only. It does not yet
demonstrate grayscale transformation of uploaded images, caching, business
progress, notifications, pass signing or APNs.
