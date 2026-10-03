# Test reports

One file per test session, named by when it was written, in UTC: `YYYY-MM-DD_HHMM.md`.
Sorting the folder by name sorts the reports by time.

## The cycle

Each report goes through three steps, and its `Status:` line says which one it is at:

| Status | Who acts next | What they do |
|---|---|---|
| `open` | the builder | Answers every finding in the report's **Response** table, then sets `answered`. |
| `answered` | the next tester | Retests every finding answered "fixed", fills the **Retest** column, then sets `closed`, or back to `open` if any fix did not hold. |
| `closed` | nobody | Done. Findings answered "later" are copied into the next report that still sees them. |

- **"Check the new test reports"** (the builder): `grep -l "^Status: open" docs/test-reports/*.md`
- **"Retest the answers"** (a tester, before testing anything new):
  `grep -l "^Status: answered" docs/test-reports/*.md`

Testers write a new file for their own findings and only touch an older report to fill its Retest
column and status. The builder only touches the Response column and the status.

## A report's shape

```
# Test report YYYY-MM-DD HH:MM UTC

Status: open
Tested: `main` at <commit>
Tester: <who, or the session link>
Pinned tests: <test file and count, or "none">

(findings, numbered, most serious first: what you did, what happened, what you expected, and the
frame JSON or headless line that shows it; then what worked as docs/TESTING.md says)

## Response

| # | Finding | Builder | Retest |
|---|---|---|---|
| 1 | one-line title | | |
```

**Builder** column, one of: `fixed in <commit>` · `won't fix: <why>` · `later: <why>` ·
`not a bug: <why>`. **Retest** column: `verified in <report file>` or
`still broken in <report file>: <one line>`. A pinned `[Explicit]` test for a fixed finding loses
its `[Explicit]` in the fixing commit; the retest checks that it now runs and passes.
