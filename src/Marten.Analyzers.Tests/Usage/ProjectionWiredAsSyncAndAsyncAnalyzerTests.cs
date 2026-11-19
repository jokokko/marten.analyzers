using System.Linq;
using System.Threading.Tasks;
using Marten.Analyzers.Tests.Infrastructure;
using Marten.Analyzers.Usage;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Marten.Analyzers.Tests.Usage
{
	public sealed class ProjectionWiredAsSyncAndAsyncAnalyzerTests
	{
		private readonly DiagnosticAnalyzer analyzer = new ProjectionWiredAsSyncAndAsyncAnalyzer();

		[Fact]
		public async Task CanIdentifyProjectionsWiredAsSyncAndAsync()
		{
			var diagnostics = await TestHelper.GetDiagnosticsAsync(analyzer,
				@"using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JasperFx.Events;
using JasperFx.Events.Projections;
using Marten;
using Marten.Events.Projections;

class T { }

class TestClass {

	void TestMethod()
	{
		DocumentStore.For(c =>
        {
			c.Projections.Add(new Projection(), ProjectionLifecycle.Async);
			c.Projections.Snapshot<TestClass>(SnapshotLifecycle.Async);
			c.Projections.Snapshot<T>(SnapshotLifecycle.Async);
		});
	}

	void TestMethod2()
	{
		DocumentStore.For(c =>
        {
			c.Projections.Add(new Projection(), ProjectionLifecycle.Inline);
			c.Projections.Snapshot<TestClass>(SnapshotLifecycle.Inline);
			c.Projections.Snapshot<T>(SnapshotLifecycle.Async);
		});
	}

	class Projection : IProjection
	{
		public Task ApplyAsync(IDocumentOperations operations, IReadOnlyList<IEvent> events, CancellationToken cancellation)
		{
			throw new NotImplementedException();
		}
	}
}");

			Assert.Collection(diagnostics.OrderBy(x => x.Location.GetLineSpan().StartLinePosition.Line), d =>
				{
					Assert.Equal(DiagnosticSeverity.Warning, d.Severity);
					Assert.Equal(17, d.Location.GetLineSpan().StartLinePosition.Line);
				}, d =>
				{
					Assert.Equal(DiagnosticSeverity.Warning, d.Severity);
					Assert.Equal(18, d.Location.GetLineSpan().StartLinePosition.Line);
				},
				d =>
				{
					Assert.Equal(DiagnosticSeverity.Warning, d.Severity);
					Assert.Equal(27, d.Location.GetLineSpan().StartLinePosition.Line);
				},
				d =>
				{
					Assert.Equal(DiagnosticSeverity.Warning, d.Severity);
					Assert.Equal(28, d.Location.GetLineSpan().StartLinePosition.Line);
				});
		}

		[Fact]
		public async Task CanIdentifyProjectionsWiredAsSyncAndAsyncThroughAllRegistrations()
		{
			var diagnostics = await TestHelper.GetDiagnosticsAsync(analyzer,
				@"#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JasperFx.Events;
using JasperFx.Events.Projections;
using Marten;
using Marten.Events.Aggregation;
using Marten.Events.Projections;
using Microsoft.Extensions.DependencyInjection;

class Doc { public Guid Id { get; set; } }

partial class ByType : SingleStreamProjection<Doc, Guid> { }
partial class ByInstance : SingleStreamProjection<Doc, Guid> { }
partial class ByServices : SingleStreamProjection<Doc, Guid> { }
partial class ByConstant : SingleStreamProjection<Doc, Guid> { }
partial class LiveAndAsync : SingleStreamProjection<Doc, Guid> { }
partial class InlineOnly : SingleStreamProjection<Doc, Guid> { }

class TestClass {

	const ProjectionLifecycle Asynchronous = ProjectionLifecycle.Async;

	void Inline(IServiceCollection services, ProjectionLifecycle unknown, IProjectionSource<IDocumentOperations, IQuerySession> first)
	{
		var marten = services.AddMarten(c =>
        {
			c.Projections.Add<ByType>(ProjectionLifecycle.Inline);
			c.Projections.Add(lifecycle: ProjectionLifecycle.Inline, source: new ByInstance());
			c.Projections.Add<ByConstant>(ProjectionLifecycle.Inline);
			c.Projections.Add<LiveAndAsync>(ProjectionLifecycle.Live);
			c.Projections.Add<InlineOnly>(ProjectionLifecycle.Inline);
			c.Projections.Add<InlineOnly>(unknown);
			c.Projections.Add(first, ProjectionLifecycle.Inline);
		});
		marten.AddProjectionWithServices<ByServices>(ProjectionLifecycle.Inline, ServiceLifetime.Singleton);
	}

	void Async(IServiceCollection services, IProjectionSource<IDocumentOperations, IQuerySession> second)
	{
		services.AddMarten(c =>
        {
			c.Projections.Add<ByType>(ProjectionLifecycle.Async);
			c.Projections.Register(new ByInstance(), ProjectionLifecycle.Async);
			c.Projections.Add<ByConstant>(Asynchronous);
			c.Projections.Add<LiveAndAsync>(ProjectionLifecycle.Async);
			c.Projections.Add(second, ProjectionLifecycle.Async);
		});
		services.AddProjectionWithServices<ByServices>(ProjectionLifecycle.Async);
	}
}");

			Assert.All(diagnostics, d => Assert.Equal("Marten1008", d.Id));
			Assert.Equal(new[] { 29, 30, 31, 37, 44, 45, 46, 50 },
				diagnostics.Select(d => d.Location.GetLineSpan().StartLinePosition.Line).OrderBy(x => x));
		}

		[Fact]
		public async Task CanIdentifyProjectionsRegisteringThemselves()
		{
			var diagnostics = await TestHelper.GetDiagnosticsAsync(analyzer,
				@"using System;
using JasperFx.Events.Projections;
using Marten;
using Marten.Events.Aggregation;
using Marten.Events.Projections;
using Microsoft.Extensions.DependencyInjection;

class Doc { public Guid Id { get; set; } }
class SingleProjection : SingleStreamProjection<Doc, Guid> { }
class MultiProjection : MultiStreamProjection<Doc, Guid> { }
class EventsProjection : EventProjection { }
class InlineOnly : SingleStreamProjection<Doc, Guid> { }

class TestClass {

	void Inline(IServiceCollection services)
	{
		SingleProjection.Register<SingleProjection>(services, ProjectionLifecycle.Inline, ServiceLifetime.Scoped, null);
		MultiProjection.Register<MultiProjection>(services, ProjectionLifecycle.Inline, ServiceLifetime.Scoped, null);
		EventsProjection.Register<EventsProjection>(services, ProjectionLifecycle.Inline, ServiceLifetime.Scoped, null);
		InlineOnly.Register<InlineOnly>(services, ProjectionLifecycle.Inline, ServiceLifetime.Scoped, null);
	}

	void Async(IServiceCollection services)
	{
		SingleProjection.Register<SingleProjection>(services, ProjectionLifecycle.Async, ServiceLifetime.Scoped, null);
		MultiProjection.Register<MultiProjection>(services, ProjectionLifecycle.Async, ServiceLifetime.Scoped, null);
		EventsProjection.Register<EventsProjection>(services, ProjectionLifecycle.Async, ServiceLifetime.Scoped, null);
	}
}");

			Assert.All(diagnostics, d => Assert.Equal("Marten1008", d.Id));
			Assert.Equal(new[] { 17, 18, 19, 25, 26, 27 },
				diagnostics.Select(d => d.Location.GetLineSpan().StartLinePosition.Line).OrderBy(x => x));
		}
	}
}
