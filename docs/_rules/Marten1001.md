---
title: Marten1001
description: Event store queried within an iteration
category: Usage
severity: Warning
---

## Cause

Event store (`session.Events`) is queried within an iteration (`while`, `do-while`, `foreach`, `await foreach`, `for`) through `IQueryEventStore` (`LoadAsync`, `AggregateStreamAsync`, `AggregateStreamToLastKnownAsync`, `FetchStreamAsync`, `FetchStreamStateAsync`, `QueryAllRawEvents`, `QueryRawEventDataOnly`, `QueryByTagsAsync`, `AggregateByTagsAsync`, `EventsExistAsync`)
or `IEventStoreOperations` (`FetchLatest`, `ProjectLatest`, `StreamLatestJson`, `FetchForWriting`, `FetchForExclusiveWriting`, `FetchForWritingByTags`, `FetchManyForWriting`, `WriteToAggregate`, `WriteExclusivelyToAggregate`).

Not reported: the query providing the items iterated (`foreach (var e in await session.Events.FetchStreamAsync(id))`).

## Reason for rule

Queries within an iteration can indicate *Select N+1 issues* (see, [What is N+1 SELECT query issue?](https://stackoverflow.com/questions/97197/what-is-n1-select-query-issue)). This rule aids in locating such sites, supporting manual code review of violations.

## How to fix violations

To fix a violation of this rule, consider using use-case specific [projections](https://martendb.io/events/projections/), projecting over multiple streams if necessary. Otherwise remodeling aggregates might be considered.

Several streams can also be fetched in one round-trip, either by enlisting the fetches in a [batched query](https://martendb.io/documents/querying/batched-queries.html) or, for writing, with `FetchManyForWriting`.

## Examples

### Violates

```csharp
var dict = new Dictionary<Guid, User>();

var issue = await session.Events.FetchLatest<Issue>(issueId);

// Each assignee generates an additional query
foreach (var i in issue.Assignees)
{
	var user = await session.Events.FetchLatest<User>(i);
	dict.Add(user.Id, user);
}
```

### Does not violate

```csharp
var issue = await session.Events.FetchLatest<Issue>(issueId);

var batch = session.CreateBatchQuery();

var users = issue.Assignees.Select(i => batch.Events.FetchLatest<User>(i)).ToList();

// Assignees are fetched in one round-trip.
await batch.Execute();
```
