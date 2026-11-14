# Business names and model contract ownership

The owner's rule of 14 November 2026 applies to every Niobium Table model.
Read the [framework guide and examples](https://github.com/niobium-nz/Niobium/blob/main/docs/model-contracts.md)
and [Store domain behavior](domain-data-access.md).

Use business property names and annotate storage roles with
`[EntityKey(EntityKeyKind.PartitionKey)]` and
`[EntityKey(EntityKeyKind.RowKey)]`. The repository maps them to the required
physical columns. Do not introduce generic key names into a business model,
or move Azure mapping/client logic into Store domains.

The data contract owns ID/composite-key encoding, canonicalization, serialized
shape and file-path definitions. Expose meaningful factories and opaque values;
keep formatting and hash helpers private. Domains continue to own order,
settlement, shipping and other business decisions and conditional persistence.
Flows coordinate domains and external calls without reconstructing model formats.

The mapper includes public computed properties; JSON attributes do not change
Table mapping. Avoid duplicate old/new key aliases and test the actual installed
adapter before narrowing setters. Framework storage abstractions can retain
their technical parameter/member names.

## Legacy contracts awaiting rollout

At main `a196fa2`, [ShippingOption](../src/Niobium.Store.Contracts/ShippingOption.cs)
still names its singleton catalogue key `PartitionKey` with value `dummy`;
its row property is already `ID`. Do not rename that partition to `Tenant`
without a business basis. The owner selected documentation now and a separate
compatible public-contract rollout.

Store also consumes Finance models such as Transaction, Accounting and
PaymentMethod. Trace their package versions, extension methods, JSON and stored
keys before adopting renamed framework APIs. Keep the reverse-time transaction
identity, payment behavior and existing key values intact. No Contracts, runtime,
package references, workflow or protection settings change in this documentation.
