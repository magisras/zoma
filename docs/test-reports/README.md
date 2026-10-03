# Test reports

One file per test session, named by when it was written, in UTC: `YYYY-MM-DD_HHMM.md`.
Sorting the folder by name sorts the reports by time.

Every report starts with this header:

```
# Test report YYYY-MM-DD HH:MM UTC

Status: open
Tested: `main` at <commit>
Tester: <who, or the session link>
Pinned tests: <test file and count, or "none">
```

- **Testers** write a new file; they never edit an older report. How to test and what to put in a
  finding: `docs/TESTING.md`.
- **The builder** reads every report whose status is `open` at the start of a session. When a
  report's findings are dealt with (fixed, or decided against), change its status to `done` and add
  one line under it per finding: fixed in which commit, or why not. A report that is partly handled
  stays `open`, with those lines added for the findings already handled.

"Check the new test reports" means: `grep -l "^Status: open" docs/test-reports/*.md`.
