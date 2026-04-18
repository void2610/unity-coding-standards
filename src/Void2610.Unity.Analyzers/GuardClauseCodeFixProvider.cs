using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Void2610.Unity.Analyzers
{
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public sealed class GuardClauseCodeFixProvider : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds =>
            ImmutableArray.Create("VUA3004");

        public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var diagnostic = context.Diagnostics[0];
            var node = root.FindNode(diagnostic.Location.SourceSpan);

            var ifStatement = node.FirstAncestorOrSelf<IfStatementSyntax>();
            if (ifStatement == null) return;

            context.RegisterCodeFix(
                CodeAction.Create(
                    "ガード節を1行に変換",
                    ct => FixGuardClauseAsync(context.Document, ifStatement, ct),
                    nameof(GuardClauseCodeFixProvider)),
                diagnostic);
        }

        private static async Task<Document> FixGuardClauseAsync(
            Document document, IfStatementSyntax ifStatement, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var guardStatement = GuardClauseAnalyzer.GetGuardStatement(ifStatement);
            if (guardStatement == null) return document;

            var leadingTrivia = ifStatement.GetLeadingTrivia();
            var trailingTrivia = ifStatement.GetTrailingTrivia();

            // ガード文からトリビアを除去し、先頭にスペースのみ付与
            var cleanStatement = guardStatement.WithoutTrivia()
                .WithLeadingTrivia(SyntaxFactory.Space);

            // 閉じ括弧の後のトリビア（改行）を除去してスペースなし（文側にスペースがある）にする
            var closeParen = SyntaxFactory.Token(SyntaxKind.CloseParenToken)
                .WithTrailingTrivia(SyntaxTriviaList.Empty);

            var newIfStatement = SyntaxFactory.IfStatement(
                    SyntaxFactory.Token(SyntaxKind.IfKeyword).WithTrailingTrivia(SyntaxFactory.Space),
                    SyntaxFactory.Token(SyntaxKind.OpenParenToken),
                    ifStatement.Condition.WithoutTrivia(),
                    closeParen,
                    cleanStatement,
                    null)
                .WithLeadingTrivia(leadingTrivia)
                .WithTrailingTrivia(trailingTrivia);

            var newRoot = root.ReplaceNode(ifStatement, newIfStatement);
            return document.WithSyntaxRoot(newRoot);
        }
    }
}
