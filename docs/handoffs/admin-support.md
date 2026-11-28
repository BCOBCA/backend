# Admin support inbox

- Added admin-only GET /api/admin/support on AdminOperationsController. Existing feedback list/reply contracts are preserved.
- IAdminOperationsService.SupportAsync and partial AdminSupportService project current Feedback records with global total/open/resolved counts and actual subject options.
- Filters: search (subject/content/admin response/UUID short #HT- prefix), subject, OPEN/RESOLVED/all, optional paired Vietnam dates (<=93 days), server paging (10/20/50). Unknown legacy statuses display as OPEN. No schema change.
- Existing reply flow still saves AdminResponse, sets RESOLVED/ResolvedAt, notifies the user and writes audit; user feedback history reads the same record.
- Added AdminSupportTests for date/paging/filter boundaries and action authorization; extended isolated PostgreSQL FeedbackSearchTests for actual query translation, code lookup, state normalization, unauthorized access and reply visibility.
- Frontend has passed tests/typecheck/lint/export and mocked browser reply-error/retry/success paths. Backend compile/tests remain unverified: installed SDK 9.0.305 cannot target net10.0. Run focused tests with .NET 10 and isolated PostgreSQL before deployment.
