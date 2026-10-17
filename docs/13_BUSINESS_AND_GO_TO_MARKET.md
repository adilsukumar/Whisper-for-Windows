# Business and Go-to-Market

## Business hypothesis

Windows users will pay for a reliable assistant that shortens the time required to learn and operate complex desktop software. The differentiator is trustworthy Windows execution and guidance, not access to models that users can obtain elsewhere.

## Beachhead positioning

Start with “learn complex Windows apps by pointing and asking.” This creates clear demos, reduces initial automation risk, and serves users with immediate pain. Choose two or three launch applications based on interviews and repeated workflows—not personal preference alone.

Candidate categories:

- Video/photo/audio creation.
- 3D/CAD/electronics tools.
- Excel/PowerPoint and business software.
- IDE/debugging/cloud tooling.

## Customer discovery

Before pricing optimization, conduct at least 20 problem interviews and 10 observed workflow sessions.

Questions:

- Which software takes longest to learn?
- What did the user search/watch last time they were stuck?
- Would pointing/explanation have solved it?
- Which tasks would they permit an assistant to perform?
- Which content must remain local?
- What delay or error makes them stop trusting the product?
- Who pays: individual, employer, school, or support team?

Avoid asking only “would you use this?” Ask users to attempt real tasks with the product.

## Proposed packaging

### Free

- Limited talk/vision usage.
- Core local STT/TTS where supported.
- Basic companion and one or two tutor packs.
- No payment card required.

### Pro (validate around US$12–20/month)

- Higher limits and stronger automatic model routing.
- Safe actions and workflow memory.
- All standard tutor packs and voices.
- Priority model downloads/support.

### Private/Power User

- All-local model manager and advanced configuration.
- Larger local models where hardware supports them.
- Exportable history/workflows and skills.
- Consider one-time license plus paid updates only if support economics work.

### Team/Enterprise later

- Central policy, approved models/providers, deployment tooling, audit controls, and support agreement.

### Founder Lifetime Pro

The two owner-approved accounts in `22_ENTITLEMENTS_AND_FOUNDER_GRANTS.md` receive the individual Pro feature set for life at no charge. They retain normal Pro usage limits and do not automatically receive separately priced Team/Enterprise products. Implement this as a durable entitlement, not a coupon or client-side email check.

Pricing is a hypothesis. Test willingness to pay through paid beta seats before building elaborate billing.

## Unit economics

Track per active user:

- STT, text/vision model, and TTS cloud cost.
- Download/CDN and update cost.
- Payment fees, support time, refunds, and taxes.
- Gross margin by plan and hardware mode.

Apply budgets and graceful limits. Local processing reduces inference cost but increases support, download, and compatibility cost; it is not free operationally.

## Launch funnel

1. Short demo showing a real complex workflow.
2. Waitlist segmented by Windows version, hardware, and target apps.
3. Invite users whose workflows match supported packs.
4. Measure download → install → onboarding → first question → first correct point → week-one return.
5. Interview churned and retained users.
6. Charge a small cohort once weekly value is demonstrated.

## Distribution

- Demo videos focused on outcomes, not architecture.
- Communities around the supported applications.
- Partnerships with educators/course creators.
- Creator affiliates only after retention is healthy.
- Product-led referrals using shareable tutorial summaries, never private screenshots by default.
- Microsoft Store considered after installer/update/privacy behavior is mature.

## Competitive strategy

- Do not compete on a checklist of providers or character skins.
- Publish honest benchmark results for pointing and action reliability.
- Demonstrate mixed-DPI/multi-monitor behavior competitors commonly mishandle.
- Make local/cloud boundaries understandable.
- Build app-specific workflow data, evaluations, and user trust.

## Brand and legal work

- Conduct trademark/domain/store-name checks before committing to “Whisper,” which is widely used in AI/audio products.
- Avoid trade dress, assets, code, or confusing branding from HeyClicky/Clicky projects.
- Credit inspiration where appropriate without implying affiliation.
- Prepare terms, privacy policy, refund policy, third-party notices, and tax/payment compliance.
- Obtain legal review before making medical, accessibility, enterprise-security, or “fully private” claims.

## Go/no-go metrics

Proceed from private to paid beta when:

- At least 60% of activated users complete a first successful grounded interaction.
- At least 30% return in week one and qualitative feedback identifies repeated value.
- Supported-app grounding passes the per-app/version/DPI and false-confidence gate defined in `14_EXECUTION_ROADMAP.md`.
- No unconfirmed consequential action occurs.
- Median support burden is sustainable for a side business.

Scale marketing only after week-four retention and gross margin support it.
