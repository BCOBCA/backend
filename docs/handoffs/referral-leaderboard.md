# Referral leaderboard

- Authenticated GET `/api/affiliate/leaderboard` accepts period=week|month|quarter|all, default all for existing clients. Calendar windows are Monday-based weeks/months/quarters in UTC+7 and use inclusive start/exclusive end.
- Count referrals whose invited user currently has a completed/paid order; for finite periods use AffiliateReferral.QualifiedAt. All-time eligibility is unchanged, including legacy referrals without QualifiedAt. Lifetime eligible counts determine Affiliate tier/rate, independently of the selected period's position/count.
- Preserve existing pagination fields and add period/myEntry. Entries add avatarUrl/tierRank/isMe. myEntry includes the authenticated user's position even outside the current page; clients cannot choose its user ID. Existing nickname/anonymized-name behavior remains. Ties preserve profile-ID order.
- `LeaderboardPeriods`, FinanceService, DTO/interface/controller are the implementation. Read-only query changes; no migration or business-data backfill. Existing order/accrual/payment rules unchanged.
- Validation: all 189 backend tests passed, including Vietnam/year-boundary periods, SQL translation/pagination/ties, own rank outside page, inactive profile exclusion, period-vs-lifetime tiers and refunded orders. Disposable test databases only.
- Deploy backend before/with frontend: the new UI rejects unsupported finite-period responses instead of presenting all-time data under another period.
