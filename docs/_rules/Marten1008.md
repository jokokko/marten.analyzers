---
title: Marten1008
description: Projection wired as synchronous and asynchronous
category: Usage
severity: Warning
---

## Cause

A projection is wired as synchronous (`ProjectionLifecycle.Inline`, `SnapshotLifecycle.Inline`) and asynchronous (`ProjectionLifecycle.Async`, `SnapshotLifecycle.Async`) through the projections of the document store (`Add`, `Register`, `Snapshot`, `AddGlobalProjection` of `StoreOptions.Projections`), the service registrations (`AddProjectionWithServices`) or the projection itself (`Register` of `SingleStreamProjection`, `MultiStreamProjection` and `EventProjection`).

## Reason for rule

Projections wired both as synchronous and asynchronous can race, leading to data loss.

**Note:** analyzers run at project scope (and against open files, if _Full Solution Analysis_ is not enabled). For solution-wide analysis, run [Marten.AnalyzerTool](https://github.com/jokokko/Marten.AnalyzerTool).

# Remarks #

Marten rejects a projection wired both as synchronous and asynchronous within a single store (`DuplicateSubscriptionNamesException` as the store is built). The rule identifies such registrations at compile time, as well as those made to separate stores, e.g. of separate processes.

Synchronous single stream projections can safely be wired in multiple store instances (e.g. in separate processes), as long as versioned methods of the event store are used (see [Marten docs](https://martendb.io/events/appending.html)).

## How to fix violations

Either have a dedicated asynchronous projection or see that the projection is applicable to be wired as synchronous in multiple store instances.

## Examples

### Violates

```csharp
// API
storeOptions.Projections.Add<MyProjection>(ProjectionLifecycle.Inline);

// Worker
storeOptions.Projections.Add<MyProjection>(ProjectionLifecycle.Async);
```

### Does not violate

```csharp
storeOptions.Projections.Add<MyProjection>(ProjectionLifecycle.Async);
```
