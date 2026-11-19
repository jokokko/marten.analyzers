using System.Linq;
using System.Threading.Tasks;
using Marten.Analyzers.Tests.Infrastructure;
using Marten.Analyzers.Usage;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Marten.Analyzers.Tests.Usage
{
	public sealed class SqlInjectionAnalyzerTests
	{
		private readonly DiagnosticAnalyzer analyzer = new SqlInjectionAnalyzer();

		[Fact]
		public async Task CanIdentifySqlInjectionCandidates()
		{
			var diagnostics = await TestHelper.GetDiagnosticsAsync(analyzer,
				@"using System;
using System.Threading;
using System.Threading.Tasks;
using Marten;

class TestClass {

	async Task TestMethod(string input)
	{
		IDocumentStore store = null;

		await using (var session = store.LightweightSession())
		{
			string n = input; // e.g. "" and 1 = 1 ; drop table mt_doc_testclass; ""
			await session.QueryAsync<TestClass>(""where data->> 'Id' = 'ABC' "" + n);
			await session.QueryAsync<TestClass>(""where data->> 'Id' = 'ABC' "" + n, CancellationToken.None);
			await session.QueryAsync<TestClass>(n + ""where data->> 'Id' = 'ABC' "");
			await session.QueryAsync<TestClass>(""where data->> 'Id' = 'ABC'"");
			await session.QueryAsync<TestClass>(""where data->> 'Id' = ?"", ""ABC"");
			session.Query<TestClass>();
			await session.QueryAsync<TestClass>(""where data->> 'Id' = 'ABC' "" + "" and 1 = 1 "");
		}
	}
}");

			Assert.Collection(diagnostics, d =>
			{
				Assert.Equal(DiagnosticSeverity.Warning, d.Severity);
				Assert.Equal(14, d.Location.GetLineSpan().StartLinePosition.Line);
			}, d =>
			{
				Assert.Equal(DiagnosticSeverity.Warning, d.Severity);
				Assert.Equal(15, d.Location.GetLineSpan().StartLinePosition.Line);
			}, d =>
			{
				Assert.Equal(DiagnosticSeverity.Warning, d.Severity);
				Assert.Equal(16, d.Location.GetLineSpan().StartLinePosition.Line);
			});
		}

		[Fact]
		public async Task CanIdentifySqlInjectionCandidatesWhereverSqlIsAccepted()
		{
			var diagnostics = await TestHelper.GetDiagnosticsAsync(analyzer,
				@"#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Marten;
using Marten.Linq.MatchesSql;

class TestClass {

	public string Name { get; set; } = """";

	async Task TestMethod(IDocumentSession session, Stream stream, CancellationToken token, string n)
	{
		var batch = session.CreateBatchQuery();
		await session.QueryAsync<TestClass>('$', ""where data ->> 'Name' = '"" + n + ""'"", token);
		await session.QueryAsync<TestClass>(token: token, sql: ""where data ->> 'Name' = '"" + n + ""'"");
		await session.StreamJson<TestClass>(stream, token, ""where data ->> 'Name' = '"" + n + ""'"");
		await session.AdvancedSql.QueryAsync<TestClass>(""select id, data from mt_doc_testclass where data ->> 'Name' = '"" + n + ""'"", token);
		await foreach (var item in session.AdvancedSql.StreamAsync<TestClass>(""select id, data from mt_doc_testclass where data ->> 'Name' = '"" + n + ""'"", token)) { }
		_ = batch.Query<TestClass>(""where data ->> 'Name' = '"" + n + ""'"");
		session.QueueSqlCommand(""delete from mt_doc_testclass where data ->> 'Name' = '"" + n + ""'"");
		await session.Query<TestClass>().Where(x => x.MatchesSql(""data ->> 'Name' = '"" + n + ""'"")).ToListAsync(token);
		await session.Query<TestClass>().OrderBySql(""data ->> '"" + n + ""' desc"").ToListAsync(token);
		await session.QueryAsync<TestClass>('$', ""where data ->> 'Name' = $"", token, n);
		await session.StreamJson<TestClass>(stream, token, ""where data ->> 'Name' = ?"", ""a"" + n);
		await session.Query<TestClass>().Where(x => x.MatchesSql(""data ->> 'Name' = ?"", n)).ToListAsync(token);
		session.QueueSqlCommand(""delete from mt_doc_testclass where data ->> 'Name' = ?"", n);
		await batch.Execute(token);
	}
}");

			Assert.All(diagnostics, d => Assert.Equal("Marten1003", d.Id));
			Assert.Equal(new[] { 16, 17, 18, 19, 20, 21, 22, 23, 24 },
				diagnostics.Select(d => d.Location.GetLineSpan().StartLinePosition.Line).OrderBy(x => x));
		}

		[Fact]
		public async Task CanIdentifyInterpolatedSqlInjectionCandidates()
		{
			var diagnostics = await TestHelper.GetDiagnosticsAsync(analyzer,
				@"#nullable enable
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Marten;
using Marten.Linq.MatchesSql;

enum Status { Open, Closed }

class TestClass {

	public string Name { get; set; } = """";

	const string Schema = ""public"";
	const int Limit = 10;

	async Task TestMethod(IDocumentSession session, CancellationToken token, string n, int count)
	{
		await session.QueryAsync<TestClass>($""where data ->> 'Name' = '{n}'"");
		await session.QueryAsync<TestClass>($@""where data ->> 'Name' = '{n}'"", token);
		await session.QueryAsync<TestClass>($""""""where data ->> 'Name' = '{n}'"""""", token);
		await session.QueryAsync<TestClass>($""where data ->> '{nameof(Name)}' = '{n.Trim()}'"", token);
		await session.QueryAsync<TestClass>($""where data ->> 'Name' = "" + n, token);
		await session.QueryAsync<TestClass>($""where data ->> 'Name' = ? limit {count}"", token, n);
		await session.Query<TestClass>().Where(x => x.MatchesSql($""data ->> 'Name' = '{n}'"")).ToListAsync(token);
		session.QueueSqlCommand($""delete from {Schema}.mt_doc_testclass where data ->> 'Name' = '{n}'"");
		await session.QueryAsync<TestClass>($""where data ->> '{nameof(Name)}' = ?"", n);
		await session.QueryAsync<TestClass>($""select id, data from {Schema}.mt_doc_testclass "" + ""where data ->> 'Name' = ?"", token, n);
		await session.QueryAsync<TestClass>($""where data ->> 'Name' = ? limit {Limit}"", token, n);
		await session.QueryAsync<TestClass>($""where data ->> 'Status' = '{Status.Open}'"", token);
		await session.QueryAsync<TestClass>($""where data ->> 'Name' = ?"", n);
		await session.QueryAsync<TestClass>($""{n}"", token);
		await session.QueryAsync<TestClass>(""where data ->> 'Name' = ?"", $""prefix{n}"");
		await session.QueryAsync<TestClass>(""select id, data from "" + Schema + "".mt_doc_testclass"", token);
	}
}");

			Assert.All(diagnostics, d => Assert.Equal("Marten1003", d.Id));
			Assert.Equal(new[] { 19, 20, 21, 22, 23, 24, 25, 26 },
				diagnostics.Select(d => d.Location.GetLineSpan().StartLinePosition.Line).OrderBy(x => x));
		}

		[Fact]
		public async Task CanIdentifySqlBuiltInVariablesAndByFormat()
		{
			var diagnostics = await TestHelper.GetDiagnosticsAsync(analyzer,
				@"#nullable enable
using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Marten;

class TestClass {

	public string Name { get; set; } = """";

	const int Limit = 10;

	async Task TestMethod(IDocumentSession session, CancellationToken token, string n, string? custom, bool flag)
	{
		var concatenated = ""where data ->> 'Name' = '"" + n + ""'"";
		await session.QueryAsync<TestClass>(concatenated, token);

		var interpolated = $""where data ->> 'Name' = '{n}'"";
		await session.QueryAsync<TestClass>(interpolated, token);

		string assigned;
		assigned = $""where data ->> 'Name' = '{n}'"";
		await session.QueryAsync<TestClass>(assigned, token);

		var appended = ""where true"";
		if (flag) appended += "" and data ->> 'Name' = '"" + n + ""'"";
		await session.QueryAsync<TestClass>(appended, token);

		var appendedInput = ""where data ->> 'Name' = "";
		appendedInput += n;
		await session.QueryAsync<TestClass>(appendedInput, token);

		var reassigned = ""where data ->> 'Name' = "";
		reassigned = reassigned + n;
		await session.QueryAsync<TestClass>(reassigned, token);

		var source = $""where data ->> 'Name' = '{n}'"";
		var copied = source;
		await session.QueryAsync<TestClass>(copied, token);

		var conditional = flag ? $""where data ->> 'Name' = '{n}'"" : ""where true"";
		await session.QueryAsync<TestClass>(conditional, token);

		var parenthesized = (""where data ->> 'Name' = '"" + n + ""'"");
		await session.QueryAsync<TestClass>(parenthesized, token);

		await session.QueryAsync<TestClass>(custom ?? ""where data ->> 'Name' = '"" + n + ""'"", token);
		await session.QueryAsync<TestClass>(string.Format(""where data ->> 'Name' = '{0}'"", n), token);
		await session.QueryAsync<TestClass>(string.Format(CultureInfo.InvariantCulture, ""where data ->> 'Name' = '{0}'"", n), token);
		var formatted = string.Format(""where data ->> 'Name' = '{0}'"", n);
		session.QueueSqlCommand(formatted);

		var literal = ""where data ->> 'Name' = ?"";
		await session.QueryAsync<TestClass>(literal, token, n);

		var constants = $""where data ->> '{nameof(Name)}' = ? limit {Limit}"";
		await session.QueryAsync<TestClass>(constants, token, n);

		var appendedText = ""where true"";
		if (flag) appendedText += "" and data ->> 'Name' = ?"";
		appendedText += flag ? "" limit 10"" : """";
		appendedText = appendedText + "" offset 0"";
		await session.QueryAsync<TestClass>(appendedText, token, n);

		var withoutText = n;
		withoutText += n;
		await session.QueryAsync<TestClass>(withoutText, token);

		await session.QueryAsync<TestClass>(custom ?? ""where true"", token);
		await session.QueryAsync<TestClass>(string.Format(""where data ->> '{0}' = ?"", nameof(Name)), token, n);
		await session.QueryAsync<TestClass>(string.Format(CultureInfo.InvariantCulture, ""where true limit {0}"", Limit), token);

		var parameter = $""prefix{n}"";
		await session.QueryAsync<TestClass>(""where data ->> 'Name' = ?"", token, parameter);

		await session.QueryAsync<TestClass>(string.Format(custom!, n), token);

		var first = n;
		var second = first;
		first = second;
		await session.QueryAsync<TestClass>(second, token);

		await session.QueryAsync<TestClass>(n, token);
	}
}");

			Assert.All(diagnostics, d => Assert.Equal("Marten1003", d.Id));
			Assert.Equal(new[] { 16, 19, 23, 27, 31, 35, 39, 42, 45, 47, 48, 49, 51 },
				diagnostics.Select(d => d.Location.GetLineSpan().StartLinePosition.Line).OrderBy(x => x));
		}

		[Fact]
		public async Task SqlCombinedWithConstantsIsNotReported()
		{
			var diagnostics = await TestHelper.GetDiagnosticsAsync(analyzer,
				@"#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Marten;

enum Status { Open, Closed }

class TestClass {

	const int Limit = 10;

	async Task TestMethod(IQuerySession session, CancellationToken token, string n, bool flag)
	{
		var where = ""where data ->> 'Name' = ?"";
		await session.QueryAsync<TestClass>(where + "" order by id"", token, n);

		var sql = ""where true"";
		if (flag)
		{
			var clause = "" and data ->> 'Name' = ?"";
			sql += clause;
		}
		await session.QueryAsync<TestClass>(sql, token, n);

		await session.QueryAsync<TestClass>(""where data ->> 'Name' = ? limit "" + Limit, token, n);
		await session.QueryAsync<TestClass>(""where true"" + (flag ? "" and data ->> 'Closed' is null"" : """"), token);
		await session.QueryAsync<TestClass>($""where data ->> 'Status' = '{Status.Open}'"" + "" order by id"", token);

		var table = ""mt_doc_testclass"";
		await session.QueryAsync<TestClass>($""select id, data from {table} where data ->> 'Name' = ?"", token, n);

		string? custom = null;
		custom ??= ""where true"";
		await session.QueryAsync<TestClass>(custom + "" limit 1"", token);

		var either = flag ? ""where true"" : n;
		await session.QueryAsync<TestClass>(either, token);
		await session.QueryAsync<TestClass>($""{either}"", token);
	}
}");

			Assert.Empty(diagnostics);
		}

		[Fact]
		public async Task CanFollowTextAndInputThroughVariables()
		{
			var diagnostics = await TestHelper.GetDiagnosticsAsync(analyzer,
				@"#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Marten;

class TestClass {

	public string this[int i] => """";

	void Build(out string sql) { sql = """"; }
	(string, int) Deconstructed() => ("""", 0);

	async Task TestMethod(IQuerySession session, CancellationToken token, string n, bool flag)
	{
		var prefix = ""where data ->> 'Name' = "";
		var concatenated = prefix + n;
		await session.QueryAsync<TestClass>(concatenated, token);

		var appended = prefix;
		appended += n;
		await session.QueryAsync<TestClass>(appended, token);

		var interpolated = $""where data ->> 'Name' = '{n}'"";
		var orderBy = "" order by id"";
		await session.QueryAsync<TestClass>(interpolated + orderBy, token);

		var format = ""where data ->> 'Name' = '{0}'"";
		await session.QueryAsync<TestClass>(string.Format(format, n), token);
		await session.QueryAsync<TestClass>(string.Format(""where data ->> '{0}' = '"" + n + ""'"", nameof(TestMethod)), token);
		await session.QueryAsync<TestClass>(""where data ->> 'Name' = '"" + this[0] + ""'"", token);

		var copy = n;
		await session.QueryAsync<TestClass>($""where data ->> 'Name' = '{copy}'"", token);

		Build(out var built);
		await session.QueryAsync<TestClass>(""where data ->> 'Name' = '"" + built + ""'"", token);

		string given;
		Build(out given);
		await session.QueryAsync<TestClass>(""where data ->> 'Name' = '"" + given + ""'"", token);

		string deconstructed;
		(deconstructed, _) = Deconstructed();
		await session.QueryAsync<TestClass>(""where data ->> 'Name' = '"" + deconstructed + ""'"", token);

		string? coalesced = null;
		coalesced ??= ""where data ->> 'Name' = '"" + n + ""'"";
		await session.QueryAsync<TestClass>(coalesced!, token);

		var reassigned = n;
		reassigned = $""where data ->> 'Name' = '{reassigned}'"";
		await session.QueryAsync<TestClass>(reassigned, token);

		var either = flag ? ""where true"" : n;
		await session.QueryAsync<TestClass>(either + "" limit 1"", token);

		await session.QueryAsync(typeof(TestClass), ""where data ->> 'Name' = '"" + n + ""'"");
	}
}");

			Assert.All(diagnostics, d => Assert.Equal("Marten1003", d.Id));
			Assert.Equal(new[] { 17, 21, 25, 28, 29, 30, 33, 36, 40, 44, 48, 52, 55, 57 },
				diagnostics.Select(d => d.Location.GetLineSpan().StartLinePosition.Line).OrderBy(x => x));
		}
	}
}
