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
