---
title: Marten1001
description: Event store queried within an iteration
category: Usage
severity: Warning
---

## Cause

Event store is queried within an iteration (`while`, `do-while`, `foreach`, `for`) through `IEventStore` (`Load`, `LoadAsync`, `AggregateStream`, `AggregateStreamAsync`).

## Reason for rule

Queries within an iteration can indicate *Select N+1 issues* (see, [What is N+1 SELECT query issue?](https://stackoverflow.com/questions/97197/what-is-n1-select-query-issue)). This rule aids in locating such sites, supporting manual code review of violations.

## How to fix violations

To fix a violation of this rule, consider using use-case specific [projections](http://jasperfx.github.io/marten/documentation/events/projections/), projecting over multiple streams if necessary. Otherwise remodeling aggregates might be considered.

## Examples

### Violates

```csharp
var dict = new Dictionary<string, User>();

var issue = query.Events.AggregateStream<Issue>("Marten1001");

foreach (var i in issue.Assignees)
{
	var user = query.Events.AggregateStream<User>(i);
	dict.Add(user.Id, user);
}
```