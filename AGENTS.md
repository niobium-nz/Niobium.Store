# Working in Niobium.Store

Read [domain model and data access](docs/domain-data-access.md) before domain,
workflow or persistence changes. Use rich `GenericDomain<TEntity>` objects for
business decisions and `IDomainRepository` from coordinating flows. Backend SDK
factories belong to framework adapters, not Store business logic.

Use enums for finite states/outcomes with compatible storage codes. Keep I/O and
state-changing actions out of `if` conditions; execute them and test a named result.
Check the actual host's package graph: Server, legacy Host and legacy Functions
reference different Niobium versions. Do not infer safe concurrency from force-save
examples or silently replace package references with framework source projects.

Inspect existing deployment/publication triggers before pushing or opening a PR.
Respect the user's current authorization and preserve unrelated work.

## Delivery and review

Read [delivery and operations](docs/delivery-and-operations.md) before pushing,
opening a PR or merging. Record the branch, commit and working-tree state; use a
feature branch from current main and preserve other work.

- Normal feature pushes, including names containing `/`, run build, local policy
  and application tests, test deployment, commit verification and the existing
  Store integration suite. Wait for the current commit's complete result; fix
  failures and do not substitute local success, an older run or skipped checks.
  An external configuration blocker may be documented in a draft PR for review;
  keep it draft until the required verification succeeds.
- After successful verification, the implementation agent creates or reuses the
  PR and requests `5he11` when `niobium-nz` is the author (`niobium-nz` otherwise).
  Verify the recorded request. Do not wait for a workflow to create the PR.
- The owner confirmed native Codex automatic review for all PRs and every push
  on 2026-11-07. The native GitHub reviewer is the separate review agent. Verify
  its code/security receipts for the exact head, then read every review, inline
  thread and conversation comment. A manual mention is a recovery action, not
  evidence that the automatic trigger ran.
- Keep actively monitoring reviews, address actionable findings, push fixes and
  repeat tests and review for the new head. A ready PR with no comments yet is
  not completion. The implementation agent must not submit its own reviewer
  decision as `5he11`; native comments/reactions are not formal GitHub approval.
- Before merging, require current-head approval, resolved review issues, green
  current-head checks and an up-to-date main ancestor. Verify the proposed merge
  tree equals the tested tree, then squash merge with an expected-head SHA,
  without another permission request. Never push directly to main or bypass rules.
- A merged PR deploys its actual merge commit to production and publishes changed
  Contracts. Monitor both workflows to terminal success, including production
  read-only verification; investigate failures through the same feature/PR process.
- Repository settings are external. Verify the protections listed in the delivery
  guide; source instructions and automatic review alone do not enforce them.

## Deployed verification boundaries

The order integration suite runs only in the existing test environment. It requires
that environment's approved `TEST_NOTIFICATION_RECIPIENT` secret, never a hardcoded
mailbox or an inherited setting from another repository. Validate it before
deployment. Keep fixture values and provider responses out of logs. The test must
verify the deployed feature commit before placing an order. It creates normal
test records and can invoke downstream Invoicing/Notification; do not claim cleanup
or delivery guarantees that this suite does not verify.

Production verification permits only same-origin `GET /health`, with no test
recipient or order credentials. Never run quote, order, tracking, payment,
subscription, sending or cleanup operations as a production verification test.
Keep Niobium package references unchanged unless an upgrade is explicitly scoped.
