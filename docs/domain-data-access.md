# Domain model and data access in Store

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

Only documentation changes are included here. No Store runtime, financial rules,
package versions, infrastructure or workflows were changed; application tests were
not rerun. Relative documentation links and whitespace were checked.
