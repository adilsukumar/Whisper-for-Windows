# Entitlements and Founder Lifetime Grants

## Status and authority

This document is the authoritative product requirement for complimentary lifetime accounts. Owner instructions on 2026-09-28 authorize an owner-admin console now. A development implementation exists in `WhisperAccounts/`; Google OAuth deployment, paid billing, signed offline claims, and Windows account integration remain incomplete. See `WhisperAccounts/README.md` for setup and explicit launch gates.

## Founder Lifetime Pro accounts

The following accounts receive **Founder Lifetime Pro** after their email ownership is verified:

| Canonical account email | Grant | Price | Expiry |
|---|---|---:|---|
| `owner@example.com` | Founder Lifetime Pro | ₹0 / $0 | Never |
| `cofounder@example.com` | Founder Lifetime Pro | ₹0 / $0 | Never |

Email comparison is case-insensitive after canonical normalization. A similar-looking address, alias, typo, or different domain does not qualify automatically.

## Entitlement meaning

Founder Lifetime Pro provides:

- Every shipped paid feature, including the highest product tier, and future paid product features.
- No subscription charge, renewal invoice, trial expiry, or payment-card requirement.
- No product usage quota. Technical rate limits, anti-abuse checks, capacity constraints, safety rules, and acceptable-use controls still apply.
- The same privacy, model availability, regional availability, and hardware requirements as equivalent paid Pro accounts.

The owner's later instruction expands this to all product features. It does not make third-party infrastructure cost-free: hosted compute still costs the operator, and provider availability/capacity rules still apply. Do not silently charge the whitelisted account.

## Owner console authorization

Only the Google-verified `owner@example.com` account with the deployment-pinned stable Google subject may manage the whitelist. Email alone, a local settings value, a client-supplied role, or a token issued for another OAuth client must never authorize the operation. The second founder account is not an administrator.

The owner may invite exact Gmail addresses or already registered verified Google Workspace accounts. Dynamic complimentary grants can be revoked/re-enabled by the owner. The two explicitly promised lifetime founders cannot be revoked by the ordinary console; exceptional security suspension preserves the underlying grant.

All other accounts require authoritative paid entitlement. Billing-provider selection, signed webhooks and desktop enforcement are still pending; the prototype must not be advertised as an enforced paid product yet.

## Non-negotiable implementation rules

1. Do not hardcode email addresses or Pro unlocks in the distributed Windows client.
2. Do not rely only on UI hiding or a local configuration file.
3. Store the grant in the authoritative server-side entitlement database or signed administrative seed/migration.
4. Require successful email verification before activating the grant.
5. Billing-provider state must not override or expire a Founder Lifetime Pro grant.
6. Plan renames or pricing migrations must preserve access to every paid product feature, including the highest tier.
7. Account merging/email changes require an authenticated administrative migration so the grant is not accidentally duplicated or lost.
8. The client receives a signed entitlement claim and treats an expired offline cache as requiring refresh—not as a reason to bill the user.
9. Grant creation, migration, and exceptional removal are audit logged without exposing the account publicly.

## Suggested data model

```text
entitlement_grant
  id: UUID
  account_id: stable internal account ID
  grant_type: FOUNDER_LIFETIME_PRO
  plan_family: INDIVIDUAL_PRO
  starts_at: timestamp
  expires_at: null
  billing_price: 0
  source: OWNER_APPROVED_SEED
  created_by: audited administrator ID
  created_at: timestamp
  updated_at: timestamp
  status: ACTIVE
```

The email is used to locate and verify the initial account. The lasting grant attaches to a stable internal account ID so email changes do not destroy access.

## Account creation flow

1. User signs up or signs in with one of the exact canonical email addresses.
2. Account service requires verified email ownership.
3. Entitlement service resolves the approved founder-grant seed.
4. A durable `FOUNDER_LIFETIME_PRO` grant is attached to the internal account.
5. Billing setup is skipped; no card is requested.
6. The application displays `Founder Lifetime Pro` and `No renewal required`.
7. A signed entitlement claim is returned to the client.

If a qualifying email already has an account, the migration must add the grant without deleting history, settings, or existing purchases.

## Billing behavior

- Checkout must not be offered for individual Pro while the grant is active.
- Billing webhooks cannot downgrade the grant when no subscription exists.
- A cancelled historical subscription does not cancel Founder Lifetime Pro.
- Receipts/invoices are not generated unless legally or operationally required for a zero-price transaction; prefer no billing transaction.
- Usage metering still runs for capacity, abuse prevention, and cost visibility, but never creates a Pro subscription charge.
- Product feature add-ons are included in complimentary all-feature access. Any genuinely separate third-party purchase requires explicit owner/user agreement and must not silently alter or charge the lifetime grant.

## Administrative controls

The owner/admin console should support:

- Search by stable account ID or verified email.
- View grant source, status, audit history, and effective features.
- Migrate the grant after a verified email/account change.
- Repair missing grants idempotently.
- Prevent accidental downgrade during plan migrations.

Because the commitment is lifetime, ordinary subscription cancellation, nonpayment, inactivity, or plan migration must not revoke it. Exceptional suspension/removal should be limited to account compromise, legal/safety requirements, abuse that would also suspend a paid Pro account, or an explicit request from the account owner. Suspension should preserve the underlying grant for restoration when appropriate.

## Seed and migration requirements

- Keep the authoritative seed server-side and out of public client bundles.
- Production deployment applies an idempotent migration keyed by canonical email and grant type.
- Re-running the migration creates no duplicate accounts or grants.
- If the account does not yet exist, keep a pending verified-email grant.
- When the account is created and verified, consume/attach the pending grant exactly once.
- Back up entitlement records under the same durability policy as paid subscription entitlements.

## Required tests

- Exact qualifying email with successful verification receives Founder Lifetime Pro.
- Case variation of the same canonical email resolves correctly.
- Unverified ownership receives no grant until verification succeeds.
- Similar/typo/alias addresses do not receive the grant accidentally.
- No card or checkout is requested.
- Every shipped paid product feature flag resolves enabled, including the highest tier.
- No product usage quota or billing applies; technical rate limits and safety controls remain active.
- Subscription cancellation/webhooks cannot remove the founder grant.
- Plan rename/migration preserves effective Pro access.
- Account email migration preserves one grant on one stable account.
- Offline entitlement refresh failure does not display a payment demand.
- Re-running the seed/migration is idempotent.
- Audit events redact unnecessary personal data.

## Release gate

Do not launch paid accounts until both founder grants are represented in the production entitlement system, tested in staging, verified on the intended accounts, and protected by plan-migration regression tests.
