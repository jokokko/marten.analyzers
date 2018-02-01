using Microsoft.CodeAnalysis.Diagnostics;

namespace Marten.Analyzers.Infrastructure
{
    public abstract class MartenAnalyzer : DiagnosticAnalyzer
    {
        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(ctx =>
            {
                var martenCtx = new MartenContext(ctx.Compilation);

                if (ContextDefined(martenCtx))
                {
                    AnalyzeCompilation(ctx, martenCtx);
                }
            });
        }
        protected virtual bool ContextDefined(MartenContext martenCtx) => martenCtx.Version != null;
        protected abstract void AnalyzeCompilation(CompilationStartAnalysisContext ctx, MartenContext martenCtx);
    }
}