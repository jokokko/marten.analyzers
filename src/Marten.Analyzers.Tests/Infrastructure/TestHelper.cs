using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Marten.Analyzers.Usage;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Marten.Analyzers.Tests.Infrastructure
{
	internal static class TestHelper
	{
		public static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(DiagnosticAnalyzer analyzer, string source, params string[] additionalSources)
		{
			var (compilation, _, workspace) = await GetCompilationAsync(source, additionalSources);

			using (workspace)
			{
				return await ApplyAnalyzers(compilation, analyzer);
			}
		}

		private static async Task<ImmutableArray<Diagnostic>> ApplyAnalyzers(Compilation compilation, params DiagnosticAnalyzer[] analyzers)
		{
			var compilationWithAnalyzers = compilation
				.WithOptions(((CSharpCompilationOptions)compilation.Options)
					.WithWarningLevel(4))
				.WithAnalyzers(ImmutableArray.Create(analyzers));

			return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
		}
		private static async Task<(Compilation, Document, Workspace)> GetCompilationAsync(string source, params string[] additionalSources)
		{
			const string fileNamePrefix = "Source";
			const string projectName = "Project";

			var projectId = ProjectId.CreateNewId(debugName: projectName);

			var workspace = new AdhocWorkspace();
			var solution = workspace
				.CurrentSolution
				.AddProject(projectId, projectName, projectName, LanguageNames.CSharp)
				.AddMetadataReferences(projectId, SystemReferences);

			var count = 0;
			var firstDocument = default(Document);

			foreach (var text in new[] { source }.Concat(additionalSources))
			{
				var newFileName = $"{fileNamePrefix}{count++}.cs";
				var documentId = DocumentId.CreateNewId(projectId, debugName: newFileName);
				solution = solution.AddDocument(documentId, newFileName, SourceText.From(text));
				if (firstDocument == default(Document))
					firstDocument = solution.GetDocument(documentId);
			}

			var compileWarningLevel = 0;
			var project = solution.GetProject(projectId);
			var compilationOptions = ((CSharpCompilationOptions)project.CompilationOptions)
				.WithOutputKind(OutputKind.DynamicallyLinkedLibrary)
				.WithWarningLevel(compileWarningLevel);
			project = project.WithCompilationOptions(compilationOptions);

			var compilation = await project.GetCompilationAsync();

			var compilationDiagnostics = compilation.GetDiagnostics();
			if (compilationDiagnostics.Length > 0)
			{
				var messages = compilationDiagnostics.Select(d => (diag: d, line: d.Location.GetLineSpan().StartLinePosition))
													 .Select(t => $"source.cs({t.line.Line},{t.line.Character}): {t.diag.Severity.ToString().ToLowerInvariant()} {t.diag.Id}: {t.diag.GetMessage()}");
				throw new InvalidOperationException($"Compilation has issues:{Environment.NewLine}{string.Join(Environment.NewLine, messages)}");
			}


			return (compilation, firstDocument, workspace);
		}

		private static readonly IEnumerable<MetadataReference> SystemReferences;
		static TestHelper()
		{
			var referencedAssemblies = typeof(TestHelper).Assembly.GetReferencedAssemblies()
				.Concat(typeof(SelectNPlus1Candidate).Assembly.GetReferencedAssemblies())
				.Concat(typeof(IDocumentStore).Assembly.GetReferencedAssemblies())
				.Concat(new[]
			    {
			        typeof(IsolationLevel).Assembly.GetName(), typeof(object).Assembly.GetName(), typeof(IQueryable<>).Assembly.GetName(),
			        typeof(List<>).Assembly.GetName(), typeof(Parallel).Assembly.GetName(), typeof(Partitioner<>).Assembly.GetName()
				});

			var refs = referencedAssemblies.Select(x => (MetadataReference)MetadataReference.CreateFromFile(Assembly.Load(x.Name).Location));

			SystemReferences = refs;
		}
	}
}