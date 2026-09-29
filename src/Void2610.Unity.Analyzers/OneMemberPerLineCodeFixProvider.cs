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
    public sealed class OneMemberPerLineCodeFixProvider : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds =>
            ImmutableArray.Create("VUA3005");

        public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var diagnostic = context.Diagnostics[0];
            var member = root.FindToken(diagnostic.Location.SourceSpan.Start).Parent?.FirstAncestorOrSelf<MemberDeclarationSyntax>();
            if (member?.Parent is not TypeDeclarationSyntax type) return;

            var index = type.Members.IndexOf(member);
            if (index <= 0) return;

            context.RegisterCodeFix(
                CodeAction.Create(
                    "メンバーを次の行へ移す",
                    ct => MoveToNextLineAsync(context.Document, type.Members[index - 1], member, ct),
                    nameof(OneMemberPerLineCodeFixProvider)),
                diagnostic);
        }

        private static async Task<Document> MoveToNextLineAsync(
            Document document, MemberDeclarationSyntax previous, MemberDeclarationSyntax member, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
            var previousLast = previous.GetLastToken();
            var memberFirst = member.GetFirstToken();

            // 改行は文書の既存の改行コードに揃え、インデントは直前のメンバーの行頭に揃える
            var endOfLine = root.DescendantTrivia().FirstOrDefault(t => t.IsKind(SyntaxKind.EndOfLineTrivia));
            var newLine = endOfLine == default ? SyntaxFactory.LineFeed : endOfLine;
            // 直前のメンバー自体が連結されているとトリビアに行頭の空白が無いため、行のテキストから取る
            var previousLine = text.Lines.GetLineFromPosition(previous.SpanStart).ToString();
            var indentTrivia = SyntaxFactory.Whitespace(previousLine.Substring(0, previousLine.Length - previousLine.TrimStart().Length));

            var keptLeading = memberFirst.LeadingTrivia.Where(t => !t.IsKind(SyntaxKind.WhitespaceTrivia));
            var newPreviousLast = previousLast.WithTrailingTrivia(newLine);
            var newMemberFirst = memberFirst.WithLeadingTrivia(new[] { indentTrivia }.Concat(keptLeading));

            var newRoot = root.ReplaceTokens(
                new[] { previousLast, memberFirst },
                (original, _) => original == previousLast ? newPreviousLast : newMemberFirst);
            return document.WithSyntaxRoot(newRoot);
        }
    }
}
