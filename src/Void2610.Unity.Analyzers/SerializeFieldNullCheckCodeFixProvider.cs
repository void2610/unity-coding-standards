using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Void2610.Unity.Analyzers
{
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public sealed class SerializeFieldNullCheckCodeFixProvider : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds =>
            ImmutableArray.Create("VUA1001");

        public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var diagnostic = context.Diagnostics[0];
            var node = root.FindNode(diagnostic.Location.SourceSpan);
            var ifStatement = node.FirstAncestorOrSelf<IfStatementSyntax>();

            if (ifStatement == null || ifStatement.Else != null)
                return;

            if (ifStatement.Statement is not ExpressionStatementSyntax)
                return;

            context.RegisterCodeFix(
                CodeAction.Create(
                    "nullチェックを削除",
                    ct => RemoveIfStatementAsync(context.Document, ifStatement, ct),
                    nameof(SerializeFieldNullCheckCodeFixProvider)),
                diagnostic);
        }

        private static async Task<Document> RemoveIfStatementAsync(
            Document document, IfStatementSyntax ifStatement, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var statement = ifStatement.Statement
                .WithLeadingTrivia(ifStatement.GetLeadingTrivia())
                .WithTrailingTrivia(ifStatement.GetTrailingTrivia());
            var newRoot = root.ReplaceNode(ifStatement, statement);
            return document.WithSyntaxRoot(newRoot);
        }
    }
}
