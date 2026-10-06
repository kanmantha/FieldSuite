# 02-Value-vs-Budget

October 2026

## Objective
Quantify value for money for SME construction/manufacturing (<50 employees, limited budgets). Compare fragmented tool spend against FieldSuite flat bundle, expose hidden costs (implementation, per-seat creep, AI paywalls, demo-gating) and make a pricing recommendation.

## Which tools give best SME value
| Category | Top value tool(s) | Why | Caveats |
|---|---|---|---|
| Safety & EHS | busybusy-free / SafetyCulture free (≤10) / Fieldwire free | Zero/low entry; SafetyCulture wide adoption; Fieldwire easy. | Exceed 10 users → $24/user; Fieldwire EHS depth limited; no cross-module automation. |
| QC/Snagging | Snag Assistant $29–99/mo flat; Snagga low; Fieldwire free→$39 | Flat or low, mobile-first, photo-heavy. | Narrow scope (snags only), project caps, not integrated to permits/safety. |
| ePTW | PermitHub Professional flat £249/mo (unlimited permits) | Flat, predictable, avoids per-seat banding common in this category. | Regional (UK); US coverage/features need confirmation. |
| Estimation | Buildxact flat $169–509/mo (unlimited users); Contractor Foreman flat | Unlimited users spread cost; predictable for 1–3 estimators. | AI add-ons/credits can erode; Contractor Foreman estimation depth varies. |
| Light HR | Buddy Punch $4.99–12.99/user + base; busybusy free→$9.99 | Cheapest per-user among transparent vendors; broad field adoption. | Admin/base fees; HR compliance features thin vs HRIS. |
| Suites (affordability outlier) | Contractor Foreman $49–332/mo flat (unlimited projects) | Affordability leader; broad toolset. | Depth/support scaling for some modules vs enterprise suites. |

Overall best cost-effectiveness for SMEs (<$30/user/mo or freemium): 1) Buddy Punch $4.99, 2) busybusy free→$9.99, 3) SafetyCulture free≤10 then $24, 4) Fieldwire free→$39, 5) PlanRadar $32 (1-user cap), 6) Contractor Foreman flat $49 (= <$5/user for 10-person team), 7) PlanSwift/Buildxact flat annual fine for 1–3 estimators, 8) Procore/Autodesk/Odoo-with-implementation out of reach.

## Which are overpriced enterprise monoliths and why
| Vendor(s) | Why overpriced for SMEs | Hidden Costs |
|---|---|---|
| Procore (~$4.5k–10k+ small, $30k–80k+ typical, volume-based, demo-gated) | GC-centric, priced on Annual Construction Volume, demo-gated access. | Implementation, training, onboarding, volume creep at renewal, third-party connectors. |
| Autodesk Build/Forma (contact-sales, per-seat, BIM-heavy) | BIM-heavy, design-first, onboarding intensive. | Custom onboarding, training, licenses stacking, scope creep. |
| Odoo (apps cheap $0–$61/user, but $10k–50k implementation, year-1 TCO $15k–75k) | "Per user app" illusion; implementation/config dominates. | Integrator fees, customization, data migration, hosting complexity, year-1 TCO 5–10x list. |
| SafetyAmp ($4,800/yr site-based min) | Site minimum high for <20 users, inflexible scaling. | Forced site bundle, limited down-scaling. |
| EHS Insight/SiteDocs/Estimator 360/Candy/SiteMate/Dashpivot | Quote-only/contact-sales creates negotiation friction, hard to benchmark TCO. | Demo-gating delays decision, soft costs in procurement. |
| Bluebeam Max ($590 Max) | AI paywalled to top tier; per-user multiplies fast. | Forced tier upgrade to get AI features. |
| Buildxact AI add-ons (+$99–149/mo) + 5-credit free tier | Erosion of "flat" promise; metered AI credits emerging. | Ongoing AI spend unpredictable. |

Key drivers: implementation costs, per-seat creep, AI paywalls/metering, demo-gating (opacity), volume-based pricing (ACV), and regional/contact-only sales models.

## 5-tool vs FieldSuite TCO (15-person team)
Assume 15-person SME: field crew + foreman + estimator/admin. Blend common transparent pricing.

| Tool (separate) | Typical Cost (monthly/annual, Oct 2026) | Notes |
|---|---|---|
| (1) Safety & EHS: SafetyCulture Premium 15 users @ $24/user/mo annual | $4,320/yr | AI credits often extra; templates limited at lower tiers. |
| (2) QC/Snagging: Fieldwire Business 15 @ $64/user/mo annual | $11,520/yr | Snag/QC subset; can grow. |
| (3) ePTW: PermitHub Professional flat £249/mo (~$335/mo) annual | $4,020/yr | Flat unlimited permits; assume US-adjusted or similar flat. |
| (4) Estimation & Quotation: Buildxact flat $339/mo annual (mid-tier) or PlanSwift 1 license $2,000/yr + training | $4,068/yr | Buildxact flat better for spreading; PlanSwift per-license. |
| (5) Light HR: Buddy Punch 15 users $4.99/user + $19 base/mo annualized | $917/yr (approx) | Transparent base. |

**Estimated 5-tool TCO (blended transparent): ~$24.8k–$26k/yr** (before implementation, AI overages, training, procurement/admin time).

**FieldSuite (flat bundle, single contract): $15k–$20k/yr flat** (recommended range) — **~20–40% savings** vs fragmented, plus savings from:
- One login, data integration (no double-entry), single invoice
- No per-seat creep across all 5, no AI metering/paywalls (as positioned)
- Reduced implementation/onboarding, consolidated audit trail
- Lower admin/support burden

## Pricing Recommendation for FieldSuite
| Recommendation | Rationale |
|---|---|
| Anchor flat pricing (SME-first). Offer 3–4 tiers max. | Predictability beats opaque/contact-gated; matches Contractor Foreman/PermitHub/Buildxact flat models. |
| Suggested entry: $99–199/mo flat (micro team <10) or $399–599/mo flat (team 10–25). Target $14.4k–$19.2k/yr for 15-person team. | Undercuts blended $24.8k–$26k while allowing margin; positions <$30/user equivalent for typical team sizes. |
| Avoid: per-seat above team bands, site-minimums, AI credits/metering, demo-gating for core pricing. | Directly addresses competitor weaknesses (AI paywalls, volume-based, opaque). |
| Bundle all 5, with modular opt-out not required (value of spine). Offer transparent public pricing page. | Reinforces "one flat bundle" positioning, avoids sales-heavy friction for SME. |
| Include implementation onboarding checklist (self-serve + optional paid onboarding) to avoid Odoo-style $10k+ surprises. | Transparent TCO, reduces procurement risk. |

**Final Recommendation:** Public flat pricing ~$499–599/mo for 10–25 users (all 5 modules), annual discount, no AI metering. This hits best-value band (<$30/user equivalent) while preserving savings vs fragmented tools.
