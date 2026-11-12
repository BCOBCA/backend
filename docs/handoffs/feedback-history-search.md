# Feedback history search

- MemberService.FeedbacksAsync now applies trimmed case-insensitive subject/content/admin-response search (max 200 characters) before ordering, paging and counting. Ownership filter remains mandatory. Empty search retains existing behavior. Existing GET /feedbacks and DTOs unchanged.
- Supports frontend feedback-history redesign and debounced search via existing CatalogQuery.Search. No schema migration required.
- FeedbackSearchTests passed on generated scratch PostgreSQL: subject/content/reply matching, whitespace, case, correct count before pagination, no results, foreign-user isolation. Scratch database removed afterward. Built with temporary .NET 10 SDK at %TEMP%/bcobca-dotnet10.
- No changes applied to configured live database, no feedback submissions and no deployment/push.
