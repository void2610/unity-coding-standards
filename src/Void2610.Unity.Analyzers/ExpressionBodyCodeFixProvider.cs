using System.Collections.Immutable;
using System.Composition;
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
    public sealed class ExpressionBodyCodeFixProvider : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds =>
            ImmutableArray.Create("VUA3001");

        public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var diagnostic = context.Diagnostics[0];
            var node = root.FindNode(diagnostic.Location.SourceSpan);

            var method = node.FirstAncestorOrSelf<MethodDeclarationSyntax>();
            if (method == null)
                return;

            context.RegisterCodeFix(
                CodeAction.Create(
                    "1行の式本体 (=>) に変換",
                    ct => ConvertToExpressionBodyAsync(context.Document, method, ct),
                    nameof(ExpressionBodyCodeFixProvider)),
                diagnostic);
        }

        private static async Task<Document> ConvertToExpressionBodyAsync(
            Document document, MethodDeclarationSyntax method, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            MethodDeclarationSyntax newMethod;

            if (method.ExpressionBody != null)
            {
                newMethod = CreateSingleLineExpressionBodyMethod(method, method.ExpressionBody.Expression);
            }
            else
            {
                var statement = method.Body.Statements[0];
                ExpressionSyntax expression;
                if (statement is ReturnStatementSyntax returnStatement)
                {
                    expression = returnStatement.Expression;
                }
                else if (statement is ExpressionStatementSyntax expressionStatement)
                {
                    expression = expressionStatement.Expression;
                }
                else
                {
                    return document;
                }

                newMethod = CreateSingleLineExpressionBodyMethod(method, expression);
            }

            var newRoot = root.ReplaceNode(method, newMethod);
            return document.WithSyntaxRoot(newRoot);
        }

        private static MethodDeclarationSyntax CreateSingleLineExpressionBodyMethod(
            MethodDeclarationSyntax method,
            ExpressionSyntax expression)
        {
            var leadingTrivia = method.GetLeadingTrivia();
            var trailingTrivia = method.GetTrailingTrivia();

            return method
                .WithBody(null)
                .WithExpressionBody(SyntaxFactory.ArrowExpressionClause(expression.WithoutTrivia()))
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
                .WithoutTrivia()
                .NormalizeWhitespace()
                .WithLeadingTrivia(leadingTrivia)
                .WithTrailingTrivia(trailingTrivia);
        }
    }
}
