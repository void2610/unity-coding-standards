using System.Collections.Generic;
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
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;

namespace Void2610.Unity.Analyzers
{
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public sealed class MemberOrderCodeFixProvider : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds =>
            ImmutableArray.Create("VUA3002", "VUA3003");

        public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var diagnostic = context.Diagnostics[0];
            var node = root.FindNode(diagnostic.Location.SourceSpan);

            var typeDeclaration = node.FirstAncestorOrSelf<TypeDeclarationSyntax>();
            if (typeDeclaration == null)
                return;

            context.RegisterCodeFix(
                CodeAction.Create(
                    "メンバー順序・空行を修正",
                    ct => FixMembersAsync(context.Document, typeDeclaration, ct),
                    nameof(MemberOrderCodeFixProvider)),
                diagnostic);
        }

        private static async Task<Document> FixMembersAsync(
            Document document, TypeDeclarationSyntax typeDeclaration, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var members = NormalizeDirectiveWrappedMembers(typeDeclaration.Members);

            // 各メンバーをカテゴリで分類
            var categorized = new List<(MemberDeclarationSyntax Member, MemberOrderAnalyzer.MemberCategory Category, int OriginalIndex)>();
            var excluded = new List<(MemberDeclarationSyntax Member, int OriginalIndex)>();

            for (var i = 0; i < members.Count; i++)
            {
                var category = MemberOrderAnalyzer.ClassifyMember(members[i]);
                if (category == MemberOrderAnalyzer.MemberCategory.Excluded)
                {
                    excluded.Add((members[i], i));
                }
                else
                {
                    categorized.Add((members[i], category, i));
                }
            }

            // カテゴリ順でソート（同一カテゴリ内は元の順序を維持）
            var sorted = categorized
                .OrderBy(x => (int)x.Category)
                .ThenBy(x => x.OriginalIndex)
                .Select(x => x.Member)
                .ToList();

            // Excludedメンバーは元位置を維持して再挿入する
            foreach (var (member, originalIndex) in excluded.OrderBy(x => x.OriginalIndex))
            {
                var insertIndex = originalIndex;
                if (insertIndex < 0) insertIndex = 0;
                if (insertIndex > sorted.Count) insertIndex = sorted.Count;
                sorted.Insert(insertIndex, member);
            }

            var newMembers = SyntaxFactory.List(sorted);
            var newTypeDeclaration = typeDeclaration.WithMembers(newMembers);

            var newRoot = root.ReplaceNode(typeDeclaration, newTypeDeclaration);
            var updatedDocument = document.WithSyntaxRoot(newRoot);
            updatedDocument = await Formatter.FormatAsync(updatedDocument, cancellationToken: cancellationToken).ConfigureAwait(false);
            newRoot = await updatedDocument.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            newTypeDeclaration = newRoot.FindNode(typeDeclaration.Span).FirstAncestorOrSelf<TypeDeclarationSyntax>() ?? newTypeDeclaration;
            return await NormalizeMemberSpacingAsync(updatedDocument, newTypeDeclaration, cancellationToken).ConfigureAwait(false);
        }

        private static SyntaxList<MemberDeclarationSyntax> NormalizeDirectiveWrappedMembers(
            SyntaxList<MemberDeclarationSyntax> members)
        {
            var normalizedMembers = members.ToList();

            for (var i = 0; i < normalizedMembers.Count - 1; i++)
            {
                var current = normalizedMembers[i];
                var next = normalizedMembers[i + 1];

                if (!HasOpeningConditionalDirective(current.GetLeadingTrivia()))
                    continue;

                var transferredTrivia = ExtractLeadingConditionalEndTrivia(next.GetLeadingTrivia());
                if (transferredTrivia.Count == 0)
                    continue;

                normalizedMembers[i] = current.WithTrailingTrivia(current.GetTrailingTrivia().AddRange(transferredTrivia));
                normalizedMembers[i + 1] = next.WithLeadingTrivia(next.GetLeadingTrivia().Skip(transferredTrivia.Count));
            }

            return SyntaxFactory.List(normalizedMembers);
        }

        private static bool HasOpeningConditionalDirective(SyntaxTriviaList triviaList) => triviaList.Any(trivia => trivia.IsKind(SyntaxKind.IfDirectiveTrivia) || trivia.IsKind(SyntaxKind.ElifDirectiveTrivia) || trivia.IsKind(SyntaxKind.ElseDirectiveTrivia));

        private static SyntaxTriviaList ExtractLeadingConditionalEndTrivia(SyntaxTriviaList triviaList)
        {
            var collected = new List<SyntaxTrivia>();

            foreach (var trivia in triviaList)
            {
                if (trivia.IsKind(SyntaxKind.WhitespaceTrivia) ||
                    trivia.IsKind(SyntaxKind.EndOfLineTrivia))
                {
                    collected.Add(trivia);
                    continue;
                }

                if (trivia.IsKind(SyntaxKind.EndIfDirectiveTrivia))
                {
                    collected.Add(trivia);
                    continue;
                }

                break;
            }

            return SyntaxFactory.TriviaList(collected);
        }

        private static async Task<Document> NormalizeMemberSpacingAsync(
            Document document, TypeDeclarationSyntax typeDeclaration, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var currentType = root.FindNode(typeDeclaration.Span).FirstAncestorOrSelf<TypeDeclarationSyntax>() ?? typeDeclaration;
            var members = currentType.Members;
            if (members.Count <= 1)
            {
                return document;
            }

            var syntaxTree = root.SyntaxTree;
            var sourceText = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
            var lineBreak = sourceText.Lines.Count > 1
                ? sourceText.ToString(TextSpan.FromBounds(sourceText.Lines[0].End, sourceText.Lines[1].Start))
                : "\n";

            var changes = new List<TextChange>();
            for (var i = 1; i < members.Count; i++)
            {
                var previous = members[i - 1];
                var current = members[i];

                var previousCategory = MemberOrderAnalyzer.ClassifyMember(previous);
                var currentCategory = MemberOrderAnalyzer.ClassifyMember(current);
                var previousIsFieldGroup = MemberOrderAnalyzer.IsFieldGroupCategory(previousCategory);
                var currentIsFieldGroup = MemberOrderAnalyzer.IsFieldGroupCategory(currentCategory);
                if (!previousIsFieldGroup || !currentIsFieldGroup)
                {
                    continue;
                }

                var previousEndLine = syntaxTree.GetLineSpan(previous.Span).EndLinePosition.Line;
                var currentStartLine = MemberOrderAnalyzer.GetMemberAnchorLine(syntaxTree, sourceText, current);
                var blankLines = currentStartLine - previousEndLine - 1;

                var requiresSingleBlankLine = previousCategory != currentCategory;

                var desiredBlankLines = blankLines;
                if (blankLines > 1)
                {
                    desiredBlankLines = 1;
                }

                if (requiresSingleBlankLine)
                {
                    desiredBlankLines = 1;
                }

                if (desiredBlankLines == blankLines)
                {
                    continue;
                }

                if (previousEndLine < 0 || currentStartLine < 0 ||
                    previousEndLine >= sourceText.Lines.Count || currentStartLine >= sourceText.Lines.Count ||
                    previousEndLine >= currentStartLine)
                {
                    continue;
                }

                var replaceSpan = TextSpan.FromBounds(
                    sourceText.Lines[previousEndLine].End,
                    sourceText.Lines[currentStartLine].Start);

                var newSeparator = string.Concat(Enumerable.Repeat(lineBreak, desiredBlankLines + 1));
                changes.Add(new TextChange(replaceSpan, newSeparator));
            }

            if (changes.Count == 0)
            {
                return document;
            }

            var newText = sourceText.WithChanges(changes.OrderByDescending(change => change.Span.Start));
            return document.WithText(newText);
        }
    }
}
