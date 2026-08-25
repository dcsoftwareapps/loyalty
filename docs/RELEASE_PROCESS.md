# LoyaltyCloud Release Process

This document defines the simple PROD release process for LoyaltyCloud.

The current stable PROD release is:

| Release | Git SHA |
| --- | --- |
| `v1.0.0` | `cfe607c6f2b8f92922c4c07a1ce94fd089401091` |

## 1. Git Strategy

- `main` remains the primary branch.
- Never develop a new feature directly on `main`.
- Every new feature, bugfix or hotfix must use a dedicated branch created from an updated `main`.
- PROD releases are represented by immutable annotated tags.
- Do not create a separate branch for every deploy.
- Do not reuse old branches for new work.
- Do not use floating tags like `latest` as the source of rollback.
- Prefer versioned immutable tags such as `v1.0.0`, `v1.0.1`, `v1.1.0`.
- Before implementing a new feature, verify the current branch. If currently on `main`, create a dedicated feature branch before modifying code.

Branch naming convention:

- `feature/<descriptive-name>`
- `bugfix/<descriptive-name>`
- `hotfix/<descriptive-name>`

Examples:

- `feature/customer-insights`
- `feature/analytics-dashboard`
- `feature/recurring-payments`
- `bugfix/google-wallet-save-link`
- `hotfix/billing-checkout`

Existing historical checkpoint:

- `prod-2026-08-24-before-billing`

## 2. SemVer Convention

Use Semantic Versioning in a practical way:

- PATCH: compatible bugfix, for example `v1.0.0` -> `v1.0.1`.
- MINOR: compatible functionality, for example `v1.0.1` -> `v1.1.0`.
- MAJOR: important or incompatible change, for example `v1.x` -> `v2.0.0`.

## 3. Flow: Feature -> STG -> Main -> PROD -> Tag

1. Start on `main`.
2. Update `main`.

```powershell
git checkout main
git pull origin main
```

3. Confirm the working tree is clean.

```powershell
git status --short
```

4. Create a dedicated branch.

```powershell
git checkout -b feature/<descriptive-name>
```

5. Implement the feature only on that branch.
6. Validate build/tests as appropriate.
7. Deploy that branch to STG when integrated validation is needed.
8. Validate manually in STG.
9. After approval, commit/push the feature branch and open a PR into `main`.
10. Merge the PR into `main`.
11. Deploy PROD only from integrated `main`, never directly from a feature branch.
12. Smoke test PROD.
13. Only after PROD is confirmed healthy, create and push the release tag.

Do not create a release tag before PROD has been validated.

If the deploy fails before PROD is stable, do not create a new release tag. Redeploy the previous known release tag instead.

If work starts while the local checkout is already on a related feature branch, continue there. Do not create nested or unnecessary branches.

## 4. Create a Release

Verify the current branch and working tree:

```powershell
git checkout main
git pull origin main
git status --short
git log -1 --oneline
```

Create a future release tag:

```powershell
git tag -a v1.0.1 <SHA> -m "LoyaltyCloud PROD v1.0.1"
git push origin v1.0.1
```

Never move a release tag after it has been pushed.

## 5. List Releases

```powershell
git tag --sort=-version:refname
```

View one release:

```powershell
git show v1.0.0 --no-patch
```

## 6. Temporarily Check Out a Release

```powershell
git fetch --tags
git checkout v1.0.0
```

This puts the repository in detached HEAD state. Use it only to publish/deploy that exact release or inspect the code.

## 7. Return to Main

```powershell
git checkout main
git pull origin main
```

## 8. Rollback API/Admin

To rollback code, check out the desired release tag and publish/deploy using the normal LoyaltyCloud deploy procedure.

Current PROD resources:

| Component | App Service | URL | OS |
| --- | --- | --- | --- |
| API | `loyaltycloud-api-894839` | `https://api.loyaltycloud.net` | Linux |
| Admin | `loyaltycloud-admin-prod-01` | `https://admin.loyaltycloud.net` | Linux |

Shared PROD App Service Plan:

- `asp-loyaltycloud-api-free`
- Despite the legacy name, the plan is currently Linux B1.
- API and Admin share this plan.

## 9. Database and Migrations Warning

A code rollback is not automatically a database rollback.

Before deploying an older release:

1. Review migrations introduced after the target tag.
2. Confirm the older code is backward-compatible with the current PROD database.
3. Do not automatically run `database update <old migration>` in PROD.
4. Treat DB rollback as a separate, explicit, reviewed procedure.

Important current PROD database state:

- Migration `AddBillingPayments` is already applied in PROD.
- Billing/Payments is live in PROD.
- Stripe LIVE is configured.

## 10. When to Use Release Branches

Do not create `release/1.0` now.

Create a branch such as `release/1.0` only if both are true:

- `main` has moved on to a later version, such as `v1.1` or `v2.0`;
- we need to maintain or hotfix the old `1.0` line independently.

For the current workflow, release tags are enough and simpler.

## 11. Current PROD Billing State

Billing PROD is active:

- Migration `AddBillingPayments` is applied.
- Stripe LIVE is configured.
- PROD webhook:

```text
https://api.loyaltycloud.net/api/billing/webhooks/stripe
```

Current Founder plan prices:

| Period | Price |
| --- | --- |
| 1 month | `$249 MXN` |
| 3 months | `$699 MXN` |
| 6 months | `$1,299 MXN` |
| 12 months | `$2,490 MXN` |

Billing UI currently shows:

- 3 months: `Ahorras $48`
- 6 months: `Ahorras $195`
- 12 months: `2 meses GRATIS` and `Ahorras $498`

STG remains the required validation environment before PROD.
