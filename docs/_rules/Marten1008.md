---
title: Marten1008
description: Projection wired as synchronous and asynchronous
category: Usage
severity: Warning
---

## Cause

A projection is wired as synchronous and asynchronous through the `EventGraph` of the document store.

## Reason for rule

Projections wired both as synchronous and asynhcronous can race, leading to data loss.

**Note:** analyzers run at project scope (and against open files, if _Full Solution Analysis_ is not enabled). For solution-wide analysis, run [Marten.AnalyzerTool](https://github.com/jokokko/Marten.AnalyzerTool).

# Remarks #

Synchronous single stream projections can safely be wired in multiple store instances (e.g. in separate processes), as long as versioned methods of the `IEvenStore` are used (see [Marten docs](http://jasperfx.github.io/marten/documentation/events/appending/)).

## How to fix violations

Either have a dedicated asynchronous projection or see that the projection is applicable to be wired as synchronous in multiple store instances.

## Examples

### Violates

```csharp
storeOptions.Events.InlineProjections.Add(new MyProjection());
storeOptions.Events.AsyncProjections.AggregateStreamsWith<MyProjection>();
```

### Does not violate

```csharp
storeOptions.Events.AsyncProjections.AggregateStreamsWith<MyProjection>();
```
