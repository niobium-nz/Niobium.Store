# Domain model and data access in Store

Read [model names and contract ownership](model-contracts.md) for the required
business-key naming and private representation boundary. Models own identity,
encoding and file/wire formats; rich domains own business decisions and mutations.

Reviewed at `1db8661ad7f3ea763e57bf8365bce4b362c77713` while addressing
[Notification PR #2](https://github.com/niobium-nz/Niobium.Notification/pull/2).
This is source guidance, not a new certification of payment or delivery behavior.

## Use the rich domain boundary

[OrderDomain](../src/Niobium.Store.Core/Domains/OrderDomain.cs) derives from
`GenericDomain<Order>`. It owns order creation, due calculations, settlement/tracking
transitions, and preparation of notification/invoice commands. The entity holds the
persisted snapshot; the domain supplies behavior. Flows obtain domains through
`IDomainRepository` and coordinate other services rather than issuing Table SDK calls.

[ShippingOptionDomain](../src/Niobium.Store.Core/Domains/ShippingOptionDomain.cs)
validates supported destinations and calculates a shipping quote. Listing, promotion
and customer domains provide other examples. Start at the domain relevant to the
business rule before adding another service class.

[Core registration](../src/Niobium.Store.Core/DependencyModule.cs) discovers domain
components and registers external events. `IRepository<TEntity>` is supplied by the
host's database adapter. `IAzureTableClientFactory` belongs to that adapter, not a
business flow. Domain events run after persistence; they are not an atomic outbox.

Use enums such as `OrderStatus` and `ShippingStatus` for decisions. Existing entities
persist integer codes and domains convert explicitly. Keep numeric values compatible.
Execute I/O and mutations separately from `if` conditions, then inspect a named result.

## Persistence details that matter

Default `SaveAsync` creates when the mapped ETag is absent and conditionally updates
when it is present. `force: true` selects an upsert and does not protect against a
stale writer. The existing OrderDomain force-save calls are examples of the object
boundary, not a guarantee that payments are deduplicated or concurrent updates safe.
Do not copy them into retry/claim logic without examining the required semantics.

A domain obtained by keys may not have a persisted entity. `GetEntityAsync` loads it;
`TryGetEntityAsync` supports a missing entity. `Initialize` does not save and is
one-shot. Repository/domain caches are local convenience, not concurrency control.
The Table mapper's write-ETag loss and verified-read-back requirements are documented
in the [framework guide](https://github.com/niobium-nz/Niobium/blob/codex/domain-data-access-guidance/docs/domain-data-access.md).
Do not replace those abstractions with a Store-specific database implementation.

## Match the actual host

Current Contracts reference Niobium.Abstractions/Helper 4.0.61; the Server references
the relevant platform packages at 4.0.60. The repository also contains older Host and
Functions projects with different versions, including StorageTable 4.0.42 and 3.4.29.
A source finding for Server's package graph must not be assumed to describe those
legacy hosts. Inspect the project actually being built and its restored dependency
assets before relying on a framework capability.

## Register the platform adapter in Store's host

Store's generic `AddStore<TBuilder>` imports both `Niobium.Database.StorageTable`
and `Niobium.Platform.StorageTable`. In the installed 4.0.60 packages, an unqualified
`builder.AddDatabase()` selects the generic database-only overload. That registers
`IRepository<T>` but omits `IQueryableRepository<T>`, which finance resolves lazily
when initializing a new customer's balance. The resulting exception occurs after
the order is saved and before its ownership lookup record is written.

[Server registration](../src/Niobium.Store.Server/DependencyModule.cs) explicitly
calls `Niobium.Platform.StorageTable.DependencyModule.AddDatabase(builder)` to
include the platform's queryable repositories. The host-registration regression
test resolves the lazy Accounting and Transaction repositories; constructing the
domain alone would not exercise those dependencies. Keep registration at the host
boundary and continue using the shared repositories inside domains and flows.

The original PR #2 investigation was documentation only. The subsequent delivery
verification in PR #3 exposed the host-registration issue above and added a
regression test against the installed packages.
