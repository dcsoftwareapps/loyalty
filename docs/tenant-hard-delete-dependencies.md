# Tenant hard delete dependency audit

Branch: `fix/tenant-hard-delete-dependencies`. Current runtime EF metadata,
configuration, entities, snapshot/migrations, BillingService and docs/Billing.md
were audited before productive changes. No schema or relationship changes.

## Complete runtime inventory before the fix

TenantId predicates are required even on noncomposite FKs. Tenant is the root.
SelfServiceSignupAttempts.TenantId is nullable: only completed/associated attempts
belong to this tenant; unassociated attempts are preserved.

| Table | Scope | Actual FKs / DeleteBehavior | Previous deletion |
|---|---|---|---|
| BillingOrders | tenant | TenantId -> Tenants (Cascade) | SQL Cascade from Tenant; tracked omission |
| BillingSettings | global | None | Global; preserve |
| CashierRefreshTokens | tenant | TenantId,TenantAdminUserId -> TenantAdminUsers (Restrict) | Omitted; blocking FK |
| Customers | tenant | TenantId -> Tenants (Restrict) | Explicit |
| CustomNotificationCampaigns | tenant | TenantId -> Tenants (Restrict) | Explicit |
| DeviceRegistrations | tenant | TenantId -> Tenants (Restrict); TenantId,SerialNumber -> LoyaltyCards (Restrict) | Explicit |
| EarnedStampRewards | tenant | TenantId,SourceEarnTransactionId -> StampTransactions (Restrict); TenantId,StampCycleId -> StampCycles (Restrict) | Explicit |
| GiftCardConfigurations | tenant | TenantId -> Tenants (Cascade) | SQL Cascade from Tenant; tracked omission |
| GiftCardDenominations | tenant | TenantId -> Tenants (Cascade) | SQL Cascade from Tenant; tracked omission |
| GiftCardDeviceRegistrations | tenant | TenantId -> Tenants (Restrict); TenantId,GiftCardId -> GiftCards (Cascade) | Omitted; blocking FK |
| GiftCards | tenant | RecipientMemberId -> Customers (SetNull); TenantId -> Tenants (Restrict) | Omitted; blocking FK |
| GiftCardTransactions | tenant | GiftCardId -> GiftCards (Restrict) | Omitted; blocking FK |
| GiftCardWallets | tenant | GiftCardId -> GiftCards (Cascade) | Cascade from GiftCard; parent omitted |
| LoyaltyCards | tenant | TenantId -> Tenants (Restrict); TenantId,CustomerId -> Customers (Restrict) | Explicit |
| LoyaltyNotifications | tenant | TenantId -> Tenants (Restrict); TenantId,CustomNotificationCampaignId -> CustomNotificationCampaigns (Restrict); TenantId,CustomerId -> Customers (Restrict); TenantId,LoyaltyCardId -> LoyaltyCards (Restrict) | Explicit |
| MemberDigitalWallets | tenant | CustomerId -> Customers (Restrict); LoyaltyCardId -> LoyaltyCards (Restrict); TenantId -> Tenants (Restrict) | Omitted; blocking FK |
| NotificationDeliveries | tenant | TenantId -> Tenants (Restrict); TenantId,LoyaltyNotificationId -> LoyaltyNotifications (Restrict) | Explicit |
| PaymentTransactions | tenant | BillingOrderId -> BillingOrders (Restrict); TenantId -> Tenants (NoAction) | Omitted; blocking FK |
| PaymentWebhookEvents | global | None | Global; preserve |
| PointCampaigns | tenant | TenantId -> Tenants (Restrict) | Explicit |
| PointLotConsumptions | tenant | TenantId -> Tenants (Restrict); TenantId,ConsumingPointTransactionId -> PointTransactions (Restrict); TenantId,PointLotId -> PointLots (Restrict); TenantId,RedemptionId -> Redemptions (Restrict) | Explicit |
| PointLots | tenant | TenantId -> Tenants (Restrict); TenantId,LoyaltyCardId -> LoyaltyCards (Restrict); TenantId,SourcePointTransactionId -> PointTransactions (Restrict) | Explicit |
| PointTransactions | tenant | TenantId -> Tenants (Restrict); TenantId,CampaignId -> PointCampaigns (Restrict); TenantId,LoyaltyCardId -> LoyaltyCards (Restrict) | Explicit |
| ProgramConfigs | tenant | TenantId -> Tenants (Restrict) | Explicit |
| Redemptions | tenant | TenantId -> Tenants (Restrict); TenantId,LoyaltyCardId -> LoyaltyCards (Restrict); TenantId,RewardCatalogItemId -> RewardCatalogItems (Restrict) | Explicit |
| RewardCatalogItems | tenant | TenantId -> Tenants (Restrict) | Explicit |
| SelfServiceSignupAttempts | tenant | TenantId -> Tenants (Cascade) | SQL Cascade from Tenant; tracked omission |
| StampCycles | tenant | TenantId,LoyaltyCardSerialNumber -> LoyaltyCards (Restrict); TenantId,StampProgramVersionId -> StampProgramVersions (Restrict) | Explicit |
| StampOperationReceipts | tenant | TenantId,SerialNumber -> LoyaltyCards (Restrict) | Explicit |
| StampPassEventContexts | tenant | TenantId,SerialNumber -> LoyaltyCards (Restrict); TenantId,TransactionId -> StampTransactions (Restrict) | Explicit |
| StampProgramVersions | tenant | CreatedByUserId -> TenantAdminUsers (Restrict); TenantId -> Tenants (Restrict) | Explicit |
| StampRewardDefinitions | tenant | TenantId,RewardIdentityId -> StampRewardIdentities (Restrict); TenantId,StampProgramVersionId -> StampProgramVersions (Restrict) | Explicit |
| StampRewardIdentities | tenant | TenantId -> Tenants (Restrict) | Explicit |
| StampTransactions | tenant | TenantId,ReversesTransactionId -> StampTransactions (Restrict); TenantId,StampCycleId -> StampCycles (Restrict) | Explicit |
| StampVisualConfigurations | tenant | TenantId -> Tenants (Restrict) | Explicit |
| SubscriptionPlans | global | None | Global; preserve |
| TenantAdminUsers | tenant | TenantId -> Tenants (Restrict) | Explicit |
| TenantBillingProfiles | tenant | TenantId -> Tenants (Cascade) | SQL Cascade from Tenant; tracked omission |
| TenantBrandings | tenant | TenantId -> Tenants (Restrict) | Explicit |
| TenantLoyaltyLevels | tenant | TenantId -> Tenants (Restrict) | Explicit |
| Tenants | root | None | Root; deleted last |
| TenantSubscriptions | tenant | TenantId -> Tenants (Restrict) | Explicit |

## Reproduction before productive changes

Each isolated SQL LocalDB case provisioned a Points tenant and customer/card,
then constructed valid dependencies through domain constructors. Gift Cards
include issued/redeemed ledger entries, remaining balance, recipient, Apple
wallet/device registration, configuration and denomination. Payments have a paid
bank-transfer billing order, succeeded transaction and profile.

| Group | Blocking FK confirmed by SQL Server |
|---|---|
| Tokens | FK_CashierRefreshTokens_TenantAdminUsers_TenantId_TenantAdminUserId |
| Member wallets | FK_MemberDigitalWallets_LoyaltyCards_LoyaltyCardId |
| Gift Cards | FK_GiftCards_Tenants_TenantId |
| Payments | FK_PaymentTransactions_BillingOrders_BillingOrderId |

Tenant cascade reaches billing orders, but Restrict blocks their transactions.
The tracked/InMemory delete completed but left orphan BillingOrders; unloaded
cascade-only children do not receive SQL cascades. The combined Points/Stamps
cases also failed before the fix. No productive code changed before audit and
individual reproductions completed.

## Payment decision from this repository

PaymentTransaction implements ITenantOwned. It records a successful monetary
operation against BillingOrder (tenant, provider ID, amount/currency/date and
safe card/receipt metadata). BillingService creates it when confirming a transfer,
card payment or automatic renewal. TenantSubscription remains the authority for
paid access. Domain/configuration/services/docs specify no retention AFTER hard
delete, archive/anonymization, or detached surviving payment. Both FKs are
nonnullable. This existing destructive operation therefore removes transactions
before orders; no additional product decision is required by current code.
Operational payment-history behavior is unchanged.

PaymentWebhookEvents has no TenantId, payload, tenant FK or order/transaction FK.
It provides global provider-event idempotency and is preserved, together with
BillingSettings and SubscriptionPlans. No external provider calls are added.

## Minimal extension and final order

The nine Stamps statements retain their exact order. Next explicitly delete:

1. CashierRefreshTokens (before users).
2. MemberDigitalWallets (before cards/customers).
3. GiftCardDeviceRegistrations, GiftCardWallets, GiftCardTransactions.
4. GiftCards (balance/recipient link; there is no separate redemption table).
5. GiftCardConfigurations, GiftCardDenominations.
6. PaymentTransactions, BillingOrders.
7. TenantBillingProfiles, associated SelfServiceSignupAttempts.

Then the previous notification/Points/device/card/customer/config/admin/branding/
subscription order continues unchanged, with Tenant last. Both SQL and tracked
routes explicitly filter all twelve new tables by TenantId, using IgnoreQueryFilters.
Cascade-only tables are explicit for parity and to prevent reproduced tracked
orphans. No general deletion redesign, FK/DeleteBehavior changes or economic,
authentication, payment or Wallet behavior changes.

All SQL deletes remain inside the existing execution strategy/transaction.
Rollback and retry use that existing mechanism. Tracked fallback retains its
SaveChanges pattern; no relational-transaction guarantee is asserted for InMemory.
Blob references disappear with DB rows; physical blobs are untouched. No storage
calls or garbage collection.

## Regression coverage

Four individual SQL cases; combined fully populated Points and historical Stamps
fixtures; equivalent dependency groups in B; all scalar columns of every mapped
tenant table compared before/after (including rowversions). A failure injected
immediately before final Tenant deletion verifies full rollback and successful
retry. A global webhook event remains present. Tracked/InMemory coverage checks
all A tables empty and B unchanged, including unloaded cascade-only children.
Existing Stamps helpers are reused via internal access only; their economic setup
and assertions are unchanged.

## Final validation

- Focused new dependencies + existing StampTenantDeletionTests: 11/11 passed.
- Expanded regression (hard delete, SuperAdmin, Points/Stamps program type and
  operations, CashierAuth, WalletProductionUpdate, SelfServiceSignup, GiftCard,
  Billing and existing Google/Loyalty Wallet tests): 437 executed, 435 passed,
  2 failed.
- The two failures are pre-existing source-contract assertions in
  GiftCardFeatureToggleTests: Sidebar_OrderAndFeatureEntriesMatchFinalInformationArchitecture
  and DisabledFeatureUsesAuthenticatedRedirectInsteadOfLoginRedirect. HEAD and
  working copies of the tests, MainLayout.razor, Routes.razor and
  RedirectUnauthorized.razor were compared and are identical. Running their exact
  string assertions against HEAD also returns false: the legacy Gestion label
  is absent and the old cashier/dashboard ternary was already replaced. No UI or
  test expectation was changed here to mask these unrelated failures.
- API Release and Admin Release passed with zero warnings/errors; no MAUI build.
- git diff --check passed, including the new files.
- Existing fixture migrations/data setup ran only on disposable LocalDB databases.
  No STG/PROD/database update, migration, FK or schema modification, storage call,
  commit, push, deploy, merge or PR.
