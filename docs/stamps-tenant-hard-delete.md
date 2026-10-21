# Stamps tenant hard delete audit

Branch: `fix/stamps-tenant-hard-delete`. Local SQL Server integration tests only;
no STG/PROD access, schema changes, migrations, or changes to FK behavior.

## Existing entry point and protections

`PlatformTenantDetail.razor` is authorized for the SuperAdmin role. Its dangerous
action requires the exact tenant slug and disables the action while busy.
`DeleteTenantCommand` passes through FluentValidation (nonempty tenant ID,
confirmation slug <=100), then `DeleteTenantHandler` calls
`SuperAdminTenantManagementService.DeleteAsync`.
The service checks tenant existence and case-sensitive trimmed slug equality.
These protections and the UI are unchanged.

Relational databases use tenant-filtered `ExecuteDeleteAsync`, not cascade-only
removal or loaded entities. The existing execution strategy wraps all deletes
(including the tenant) in one transaction, commits on success, rolls back and
rethrows on failure. The nonrelational fallback uses `RemoveRange` and
`SaveChangesAsync`; EF orders tracked dependencies, without relational atomicity.

## Reproduction before the fix

Two Stamps tenants were provisioned independently in an isolated LocalDB database.
Each has a customer/card, two program versions and cycles, a welcome transaction,
earns, a self-referencing reversal, receipt rows, Available/Redeemed/Voided reward
snapshots, a pass event context, and both reward image references plus empty/filled
stamp assets. Versions and operations are created through existing commands.

The old delete failed with SQL Server's REFERENCE constraint:
`FK_StampCycles_LoyaltyCards_TenantId_LoyaltyCardSerialNumber`.
None of the nine Stamps tables was included in either deletion path.

## Actual model inventory

All FKs in this table use **DeleteBehavior.Restrict**. Composite Stamps/card FKs
include TenantId in both the dependent and principal keys. There are no direct
Customer FKs on these nine entities; the customer is reached through LoyaltyCard.
All nine tables were missing from the old hard delete.

| Table | Actual foreign keys | Additional context |
|---|---|---|
| StampProgramVersions | TenantId -> Tenants; CreatedByUserId -> TenantAdminUsers | Historical economic rules; immutable creator/rules |
| StampRewardDefinitions | (TenantId, StampProgramVersionId) -> StampProgramVersions; (TenantId, RewardIdentityId) -> StampRewardIdentities | Historical name/ShortName/description/threshold |
| StampRewardIdentities | TenantId -> Tenants | ImageBlobName (available) and UpcomingImageBlobName are strings, not FKs |
| StampVisualConfigurations | TenantId -> Tenants | EmptyStampBlobName / FilledStampBlobName are strings, not FKs |
| StampCycles | (TenantId, LoyaltyCardSerialNumber) -> LoyaltyCards; (TenantId, StampProgramVersionId) -> StampProgramVersions | Includes completed and active historical cycles |
| StampTransactions | (TenantId, StampCycleId) -> StampCycles; (TenantId, ReversesTransactionId) -> StampTransactions | OperatorUserId is audit data, not a configured FK |
| EarnedStampRewards | (TenantId, StampCycleId) -> StampCycles; (TenantId, SourceEarnTransactionId) -> StampTransactions | Available/Redeemed/Voided; definition ID and operator IDs are provenance, not FKs |
| StampOperationReceipts | (TenantId, SerialNumber) -> LoyaltyCards | Idempotency key, operation, persisted response JSON; response references are not FKs |
| StampPassEventContexts | (TenantId, SerialNumber) -> LoyaltyCards; (TenantId, TransactionId) -> StampTransactions | Announcement context; no FK to an earned reward |

Sources: `StampConfigurations.cs`, `StampOperationConfigurations.cs`,
`StampPassEventContextConfiguration.cs`, current `AppDbContext` and domain entities.
Shared Customer/LoyaltyCard, notification, device, branding, subscription and admin
tables retain their existing model and existing deletion logic.

## Minimal fix and final order

Both existing paths now explicitly remove the tenant's:

1. StampPassEventContexts
2. StampOperationReceipts
3. EarnedStampRewards
4. StampTransactions (one SQL statement removes earns and referencing reversals)
5. StampCycles
6. StampRewardDefinitions
7. StampProgramVersions
8. StampRewardIdentities
9. StampVisualConfigurations

The previous order then continues unchanged: NotificationDeliveries,
LoyaltyNotifications, PointLotConsumptions, PointLots, Redemptions,
PointTransactions, DeviceRegistrations, LoyaltyCards, CustomNotificationCampaigns,
PointCampaigns, RewardCatalogItems, Customers, ProgramConfigs, TenantLoyaltyLevels,
TenantAdminUsers, TenantBrandings, TenantSubscriptions, then Tenants.

Each new query explicitly uses `IgnoreQueryFilters().Where(TenantId == target)`.
No global cascade changes, new deletion framework, or economic changes.
The SQL transaction includes all new statements. A test injects an exception at
the card delete, after Stamps rows have been removed, and verifies a full rollback
before successfully retrying.

## Blob behavior

The existing hard delete has no storage dependency and does not delete blobs,
including branding assets and generated pass files. This change preserves that
behavior: deleting reward identities removes both Available/Upcoming references;
deleting visual configuration removes empty/filled references. Physical blobs
remain in storage under their existing tenant prefixes. No new garbage collector
or external blob calls were added. Blob failures therefore cannot interrupt this
DB transaction. This is not a claim that physical assets are garbage-collected.

## Isolation and validation

The regression reads every scalar column of every mapped tenant table directly
from LocalDB (including rowversions, with tenant predicates and stable PK order).
It compares all of tenant B before/after; checks zero rows for tenant A after
success; and compares both complete snapshots after injected rollback.
Existing Points hard-delete/customer-card and exact-slug tests remain in the
focused regression. Production code changes only the existing deletion service.

## Independent omissions found during audit (not fixed here)

The existing explicit delete also omits `CashierRefreshTokens` (Restrict to
TenantAdminUsers), `MemberDigitalWallets` (Restrict to tenant/customer/card),
`GiftCards` / `GiftCardTransactions` / `GiftCardDeviceRegistrations` (Restrict
relationships), and `PaymentTransactions` (NoAction to tenant, Restrict to
BillingOrder). Populated instances can independently block deletion. This is a
source-model finding, not a claim of a separate executed reproduction.
The new Stamps tests intentionally do not seed these unrelated dependencies.

BillingOrders, TenantBillingProfiles, SelfServiceSignupAttempts and GiftCard
configuration/denominations have existing cascade behavior; this fix does not
change that behavior. Successful Stamps regression does not certify a hard delete
with every possible unrelated feature populated. These existing omissions need
separate review before promising universal tenant deletion.

## Validation results

- Before the fix: 2/2 SQL regression cases failed with the StampCycles/card FK.
- After the fix: 3/3 focused deletion cases passed (SQL success, rollback/retry,
  nonrelational fallback).
- Combined PlatformTenantDeletion, SuperAdmin, StampOperations, StampFoundation,
  StampVisualAssets and StampAdmin: 129/129 passed, including existing Points
  deletion and confirmation checks.
- API Release and Admin Release: passed, zero warnings/errors. No MAUI build.
- git diff --check, including new files: passed.

All database setup/migrations used by the existing fixtures ran only against
unique disposable SQL LocalDB databases. No external database update occurred.
