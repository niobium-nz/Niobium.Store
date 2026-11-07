# Store delivery and review

The owner requested aligning Store's review process and production triggers with
Notification and Invoicing after confirming native automatic review for all three
repositories on 2026-11-07. This guide governs the author/agent process; repository
administration and native Codex settings remain separate configuration.

## Sequence

1. Work on a feature branch from current `main`. Record the branch, commit and
   working-tree state. Push normally, including documentation changes; do not use
   skip-CI markers to bypass delivery verification.
2. Wait for `Azure Deployment` on that exact commit. It builds and runs application
   and delivery-policy tests, deploys to the existing `test` environment, verifies
   `/health` reports the feature SHA, and runs the Store integration suite. A failed,
   skipped or pending required step is not success. Fix failures before review.
   If an external setting blocks verification, a draft PR may expose the concrete
   change and blocker for early review. Keep it draft until verification succeeds;
   any manual Codex review of that draft must be identified as manual.
3. After success, the author creates/reuses a ready PR to `main` and requests the
   designated reviewer: `5he11` for `niobium-nz` authorship, otherwise `niobium-nz`.
   The implementation agent creates the PR through GitHub tools and verifies that
   the reviewer request was recorded. Workflows do not create or approve PRs.
4. Native Codex reviews in its separate background context. The implementation
   agent monitors the current-head code/security receipts, review submissions,
   inline threads and conversation comments. Address actionable findings, push
   fixes and repeat testing and review. Keep monitoring while review is pending;
   a quiet PR or a completed code review does not replace required approval.
5. Before merging, obtain one valid formal approval of the current head, resolve
   review issues, verify the current test run and main ancestry, and compare the
   proposed merge tree with the tested tree. Squash merge through the connector
   with the expected head SHA once those conditions are satisfied. Do not ask for
   a second merge permission or bypass protections. The implementation agent must
   not act as its own reviewer through the `5he11` connection.
6. Follow production deployment, read-only verification and conditional package
   publishing through terminal success. Investigate failures and prepare fixes
   through the same feature/test/review process. A merge alone is not completion.

## Native review configuration and evidence

The owner confirmed these saved settings for Notification, Store and Invoicing:

| Setting | Code review | Security review |
| --- | --- | --- |
| Scope | All PRs | All PRs |
| Trigger | On every push | Whenever code review runs |

Store [PR #2's receipt](https://github.com/niobium-nz/Niobium.Store/pull/2#issuecomment-6702129606)
records completed code and security reviews for `aa2c086b5a8e8f914d43d9366416902e52a932a6`
when the draft was marked ready. That demonstrates execution for that head;
the owner's confirmation supplies the saved scope/trigger settings. Verify a new
receipt after every subsequent push. The connector cannot inspect or change
[native Codex settings](https://chatgpt.com/settings/code-review).

The native GitHub reviewer supplies the separate agent review. Requesting `5he11`
assigns the formal reviewer; it does not configure Codex triggers. Native findings
or thumbs-up reactions are not formal current-head approval. A manual `@codex review`
request can recover missing execution, but must be recorded as manual. Do not
silently replace native review with a polling task or duplicate implementation chat.

## Workflow events and commit identity

| Event | Effect |
| --- | --- |
| Feature push, including nested branch names | Build/local tests, test deployment, commit check and real test integration |
| PR opened, updated, reopened or marked ready | Native review and author monitoring; no production deployment or package publication |
| PR closed with `merged: true` into main | Deploy and verify the actual merge commit; publish Contracts only if its tree changed |
| PR closed unmerged, direct main push or manual dispatch | No deployment or package publication through these workflows |

The production job and release job explicitly require a closed, merged PR to main
using the trusted base-repository `pull_request_target` event, so merged fork and
Dependabot PRs can use release credentials. They never run for opened or updated
PRs or check out an unmerged PR head. See GitHub's
[merged-PR event guidance](https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows#running-your-pull_request_target-workflow-when-a-pull-request-merges).
They check out `merge_commit_sha`. `SourceRevisionId` is embedded in the Server's
assembly metadata, and `/health` exposes only readiness and that compiled commit.
It performs no Store data access. Production verification rejects other methods,
paths, redirects, malformed/oversized responses and mismatched commits.

Workflow-wide test concurrency covers build, deployment and integration so another
feature deployment cannot replace the host during verification. Production and
package publishing have separate queues. `cancel-in-progress: false` and `queue: max`
retain up to 100 pending runs; additional runs are canceled when that queue is full.
This is supported by GitHub's current
[concurrency reference](https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-syntax#concurrency).
Queue acquisition order can differ from merge order. After acquiring its queue,
production validates the closed merge and compares its SHA with the current main
tip before checkout, authentication or deployment. A superseded run records a skip
in its summary; the agent follows the newer main commit's production run. This
prevents an older queued run from rolling back a newer deployment. Package releases
retain their separate merged ranges so a later documentation-only merge does not
suppress an earlier Contracts release.
Feature prerelease publication is removed; a Contracts tree comparison between the
closed event's pre-merge `base.sha` and actual `merge_commit_sha` determines whether
release work is needed. Fetch complete history and validate ancestry. Comparing
only the merge's first parent would miss earlier changes in a multi-commit rebase
merge; the full range also correctly excludes changes reverted within the PR.
Unchanged Contracts skips restore/build, NuGet login and publication. Missing merge
history or an invalid base fails rather than silently reporting no change.

## Test environment and limits

The existing Store test environment supplies Azure/azd credentials, `CUSTOM_DOMAIN_NAME`,
payment configuration and service settings already used by its deployment. The
runner additionally binds `secrets.TEST_NOTIFICATION_RECIPIENT` to the order test.
This must be an explicitly approved mailbox configured in Store's `test` environment.
Do not copy or infer a recipient from another repository, fall back to a hardcoded
address, or print its value. Fixture validation occurs before Azure login/provisioning.

The existing quote/order/tracking suite operates on existing Store test resources
and can create orders, customers, invoices and notifications. It does not establish
recipient delivery, transactional cleanup or payment-provider correctness. It does
not opt the fixture into marketing subscriptions. The suite follows the deployed
commit check and is never invoked by the production job. Production receives only
the health URL and expected merge SHA for read-only verification.

Local application, event-policy, package-diff, fixture-boundary and health-client
tests run before a feature deployment. These changes add no framework package
upgrade, new coverage threshold or trusted-main integration controller. Store's
existing integration suite is not equivalent to Notification/Invoicing's stronger
independent deployed harness; do not claim that additional protection is installed.
Quote, order and tracking must return JSON with their expected result fields. A
missing order ID, malformed response or failed tracking assertion fails the suite;
those failures must never be swallowed by a JSON-parsing catch block.
If integration fails, the test job queries recent exception types and Niobium
method names from its existing Application Insights resource. It never exports
exception messages, request bodies, recipients or provider responses. Diagnostics
are best effort and do not change the original integration result.

## Repository settings

The audit on 2026-11-07 found main at
`db600f117002509c932d8d60321f9892f8cb7263` unprotected, with no repository rulesets.
The intended settings are PR-only main updates, one approval with stale approvals
dismissed, resolved threads, an up-to-date branch, required successful checks
`Build and local tests` and `Deploy test and run integration`, and squash merging.
Disallow direct/force pushes and bypasses; Actions does not need permission to
create or approve PRs. Configure these after the named checks exist and verify
the saved settings separately. The connected GitHub tools cannot administer them.

Changing source workflow conditions does not install branch protections or prove
external settings. The author/agent must enforce the review sequence while those
administrative settings are pending, and must report that state accurately.
