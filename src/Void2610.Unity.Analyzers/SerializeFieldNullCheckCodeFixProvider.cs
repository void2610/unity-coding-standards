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
using Microsoft.CodeAnalysis.Text;

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
            if (root == null)
                return;

            var diagnostic = context.Diagnostics[0];
            var node = root.FindNode(diagnostic.Location.SourceSpan);

            if (node.FirstAncestorOrSelf<IfStatementSyntax>() is { } ifStatement)
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        "nullチェックを削除",
                        ct => SimplifyIfStatementAsync(context.Document, diagnostic.Location.SourceSpan, ct),
                        nameof(SerializeFieldNullCheckCodeFixProvider) + ".If"),
                    diagnostic);
                return;
            }

            if (node.FirstAncestorOrSelf<ConditionalAccessExpressionSyntax>() is { } conditionalAccess)
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        "nullチェックを削除",
                        ct => ReplaceConditionalAccessAsync(context.Document, conditionalAccess.Span, ct),
                        nameof(SerializeFieldNullCheckCodeFixProvider) + ".ConditionalAccess"),
                    diagnostic);
                return;
            }

            if (node.FirstAncestorOrSelf<BinaryExpressionSyntax>() is { } binary &&
                binary.IsKind(SyntaxKind.CoalesceExpression))
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        "nullチェックを削除",
                        ct => ReplaceCoalesceAsync(context.Document, binary.Span, ct),
                        nameof(SerializeFieldNullCheckCodeFixProvider) + ".Coalesce"),
                    diagnostic);
            }
        }

        private static async Task<Document> SimplifyIfStatementAsync(
            Document document, TextSpan diagnosticSpan, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var diagnosticNode = root?.FindNode(diagnosticSpan, getInnermostNodeForTie: true);
            var ifStatement = diagnosticNode?.FirstAncestorOrSelf<IfStatementSyntax>();
            if (ifStatement == null || diagnosticNode == null || semanticModel == null)
                return document;

            // 条件内の全[SerializeField]フィールド参照を一括でtrue/falseに置換する
            var replacedCondition = ReplaceAllSerializeFieldChecks(ifStatement.Condition, semanticModel);
            var simplifiedCondition = SimplifyCondition(replacedCondition);

            if (TryEvaluateBooleanLiteral(simplifiedCondition, out var conditionValue))
            {
                StatementSyntax replacementStatement = null;

                if (conditionValue)
                {
                    replacementStatement = UnwrapStatement(ifStatement.Statement, ifStatement.GetLeadingTrivia(), ifStatement.GetTrailingTrivia());
                }
                else if (ifStatement.Else != null)
                {
                    replacementStatement = UnwrapStatement(ifStatement.Else.Statement, ifStatement.GetLeadingTrivia(), ifStatement.GetTrailingTrivia());
                }

                SyntaxNode newRoot;
                if (replacementStatement == null)
                {
                    newRoot = root.RemoveNode(ifStatement, SyntaxRemoveOptions.KeepNoTrivia);
                }
                else if (replacementStatement is BlockSyntax multiBlock &&
                         multiBlock.Statements.Count > 1 &&
                         ifStatement.Parent is BlockSyntax parentBlock)
                {
                    // 複数ステートメントのブロックは{}が残らないよう親ブロックにインライン化する
                    // 1番目のステートメントには if 文のトリビア（コメント含む）を維持し、
                    // 2番目以降はインデントのみ付与する
                    var leadingTrivia = ifStatement.GetLeadingTrivia();
                    var indentOnly = GetIndentationTrivia(leadingTrivia);
                    var trailingTrivia = ifStatement.GetTrailingTrivia();
                    var inlined = new List<StatementSyntax>();
                    for (var i = 0; i < multiBlock.Statements.Count; i++)
                    {
                        var trivia = i == 0 ? leadingTrivia : indentOnly;
                        var stmt = multiBlock.Statements[i].WithLeadingTrivia(trivia);
                        if (i == multiBlock.Statements.Count - 1)
                            stmt = stmt.WithTrailingTrivia(trailingTrivia);
                        inlined.Add(stmt);
                    }
                    var idx = parentBlock.Statements.IndexOf(ifStatement);
                    var newStatements = parentBlock.Statements.RemoveAt(idx);
                    for (var i = inlined.Count - 1; i >= 0; i--)
                        newStatements = newStatements.Insert(idx, inlined[i]);
                    newRoot = root.ReplaceNode(parentBlock, parentBlock.WithStatements(newStatements));
                }
                else
                {
                    newRoot = root.ReplaceNode(ifStatement, replacementStatement);
                }

                return document.WithSyntaxRoot(newRoot);
            }

            var newIfStatement = ifStatement.WithCondition(simplifiedCondition);
            return document.WithSyntaxRoot(root.ReplaceNode(ifStatement, newIfStatement));
        }

        private static ExpressionSyntax ReplaceAllSerializeFieldChecks(
            ExpressionSyntax condition, SemanticModel semanticModel)
        {
            // 条件内の全[SerializeField]フィールド識別子を収集し、一括でtrue/falseに置換する
            var replacements = new Dictionary<ExpressionSyntax, ExpressionSyntax>();
            foreach (var expr in condition.DescendantNodesAndSelf().OfType<ExpressionSyntax>())
            {
                if (replacements.ContainsKey(expr)) continue;
                var target = FindReplacementTarget(expr, condition);
                if (target == null || replacements.ContainsKey(target)) continue;
                if (!IsSerializeFieldAccessExpression(target, semanticModel)) continue;
                var value = EvaluateReplacement(target);
                if (value == null) continue;
                replacements[target] = SyntaxFactory.LiteralExpression(
                    value.Value ? SyntaxKind.TrueLiteralExpression : SyntaxKind.FalseLiteralExpression)
                    .WithTriviaFrom(target);
            }
            if (replacements.Count == 0) return condition;
            return condition.ReplaceNodes(replacements.Keys, (original, _) => replacements[original]);
        }

        private static bool IsSerializeFieldAccessExpression(
            ExpressionSyntax expression, SemanticModel semanticModel)
        {
            // 対象となる識別子・メンバーアクセスを特定する
            ExpressionSyntax accessor = expression switch
            {
                PrefixUnaryExpressionSyntax prefix when prefix.IsKind(SyntaxKind.LogicalNotExpression) => prefix.Operand,
                BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.EqualsExpression) || binary.IsKind(SyntaxKind.NotEqualsExpression) => binary.Left,
                IsPatternExpressionSyntax isPattern => isPattern.Expression,
                IdentifierNameSyntax or MemberAccessExpressionSyntax => expression,
                _ => null
            };
            if (accessor == null) return false;
            // セマンティックモデルで[SerializeField]フィールドか確認
            if (semanticModel.GetSymbolInfo(accessor).Symbol is not IFieldSymbol field)
                return false;
            return field.GetAttributes().Any(a => a.AttributeClass?.Name == "SerializeField");
        }

        private static SyntaxTriviaList GetIndentationTrivia(SyntaxTriviaList leadingTrivia)
        {
            // 末尾の空白トリビア（インデント）のみを抽出する
            for (var i = leadingTrivia.Count - 1; i >= 0; i--)
            {
                if (leadingTrivia[i].IsKind(SyntaxKind.WhitespaceTrivia))
                    return SyntaxFactory.TriviaList(leadingTrivia[i]);
            }
            return SyntaxFactory.TriviaList();
        }

        private static StatementSyntax UnwrapStatement(StatementSyntax statement, SyntaxTriviaList leadingTrivia, SyntaxTriviaList trailingTrivia)
        {
            if (statement is BlockSyntax block && block.Statements.Count == 1)
            {
                statement = block.Statements[0];
            }

            return statement
                .WithLeadingTrivia(leadingTrivia)
                .WithTrailingTrivia(trailingTrivia);
        }

        private static ExpressionSyntax FindReplacementTarget(SyntaxNode diagnosticNode, ExpressionSyntax condition)
        {
            var current = diagnosticNode;
            while (current != null && current != condition)
            {
                if (current.Parent is PrefixUnaryExpressionSyntax prefix &&
                    prefix.IsKind(SyntaxKind.LogicalNotExpression))
                {
                    return prefix;
                }

                if (current is BinaryExpressionSyntax binary &&
                    (binary.IsKind(SyntaxKind.EqualsExpression) ||
                     binary.IsKind(SyntaxKind.NotEqualsExpression) ||
                     binary.IsKind(SyntaxKind.CoalesceExpression)))
                {
                    return binary;
                }

                if (current is IsPatternExpressionSyntax isPattern)
                {
                    return isPattern;
                }

                if (current is IdentifierNameSyntax or MemberAccessExpressionSyntax)
                {
                    return (ExpressionSyntax)current;
                }

                current = current.Parent;
            }

            if (current is PrefixUnaryExpressionSyntax currentPrefix &&
                currentPrefix.IsKind(SyntaxKind.LogicalNotExpression))
            {
                return currentPrefix;
            }

            if (current is BinaryExpressionSyntax currentBinary &&
                (currentBinary.IsKind(SyntaxKind.EqualsExpression) ||
                 currentBinary.IsKind(SyntaxKind.NotEqualsExpression) ||
                 currentBinary.IsKind(SyntaxKind.CoalesceExpression)))
            {
                return currentBinary;
            }

            if (current is IsPatternExpressionSyntax currentIsPattern)
            {
                return currentIsPattern;
            }

            if (current is IdentifierNameSyntax or MemberAccessExpressionSyntax)
            {
                return (ExpressionSyntax)current;
            }

            return null;
        }

        private static bool? EvaluateReplacement(ExpressionSyntax expression)
        {
            return expression switch
            {
                PrefixUnaryExpressionSyntax prefix when prefix.IsKind(SyntaxKind.LogicalNotExpression) => false,
                BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.EqualsExpression) => false,
                BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.NotEqualsExpression) => true,
                IsPatternExpressionSyntax isPattern when IsNullPattern(isPattern.Pattern) => isPattern.Pattern is UnaryPatternSyntax,
                IdentifierNameSyntax => true,
                MemberAccessExpressionSyntax => true,
                _ => null
            };
        }

        private static ExpressionSyntax SimplifyCondition(ExpressionSyntax expression)
        {
            expression = StripParentheses(expression);

            if (expression is ParenthesizedExpressionSyntax parenthesized)
            {
                return SimplifyCondition(parenthesized.Expression);
            }

            if (expression is PrefixUnaryExpressionSyntax prefix &&
                prefix.IsKind(SyntaxKind.LogicalNotExpression))
            {
                var operand = SimplifyCondition(prefix.Operand);
                if (TryEvaluateBooleanLiteral(operand, out var value))
                {
                    return SyntaxFactory.LiteralExpression(value ? SyntaxKind.FalseLiteralExpression : SyntaxKind.TrueLiteralExpression)
                        .WithTriviaFrom(prefix);
                }

                return prefix.WithOperand(operand);
            }

            if (expression is BinaryExpressionSyntax binary)
            {
                var left = SimplifyCondition(binary.Left);
                var right = SimplifyCondition(binary.Right);

                if (binary.IsKind(SyntaxKind.LogicalAndExpression))
                {
                    if (TryEvaluateBooleanLiteral(left, out var leftValue))
                        return leftValue ? right.WithTriviaFrom(binary) : left.WithTriviaFrom(binary);
                    if (TryEvaluateBooleanLiteral(right, out var rightValue))
                        return rightValue ? left.WithTriviaFrom(binary) : right.WithTriviaFrom(binary);
                    return binary.WithLeft(left).WithRight(right);
                }

                if (binary.IsKind(SyntaxKind.LogicalOrExpression))
                {
                    if (TryEvaluateBooleanLiteral(left, out var leftValue))
                        return leftValue ? left.WithTriviaFrom(binary) : right.WithTriviaFrom(binary);
                    if (TryEvaluateBooleanLiteral(right, out var rightValue))
                        return rightValue ? right.WithTriviaFrom(binary) : left.WithTriviaFrom(binary);
                    return binary.WithLeft(left).WithRight(right);
                }
            }

            return expression;
        }

        private static ExpressionSyntax StripParentheses(ExpressionSyntax expression)
        {
            while (expression is ParenthesizedExpressionSyntax parenthesized)
                expression = parenthesized.Expression;

            return expression;
        }

        private static bool TryEvaluateBooleanLiteral(ExpressionSyntax expression, out bool value)
        {
            expression = StripParentheses(expression);

            if (expression.IsKind(SyntaxKind.TrueLiteralExpression))
            {
                value = true;
                return true;
            }

            if (expression.IsKind(SyntaxKind.FalseLiteralExpression))
            {
                value = false;
                return true;
            }

            value = false;
            return false;
        }

        private static bool IsNullPattern(PatternSyntax pattern)
        {
            return pattern is ConstantPatternSyntax constant && constant.Expression.IsKind(SyntaxKind.NullLiteralExpression) ||
                   pattern is UnaryPatternSyntax unary &&
                   unary.OperatorToken.IsKind(SyntaxKind.NotKeyword) &&
                   IsNullPattern(unary.Pattern);
        }

        private static async Task<Document> ReplaceConditionalAccessAsync(
            Document document, TextSpan span, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            if (root?.FindNode(span) is not ConditionalAccessExpressionSyntax conditionalAccess)
                return document;

            return document.WithSyntaxRoot(root.ReplaceNode(
                conditionalAccess,
                conditionalAccess.Expression.WithTriviaFrom(conditionalAccess)));
        }

        private static async Task<Document> ReplaceCoalesceAsync(
            Document document, TextSpan span, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            if (root?.FindNode(span) is not BinaryExpressionSyntax binary)
                return document;

            return document.WithSyntaxRoot(root.ReplaceNode(
                binary,
                binary.Left.WithTriviaFrom(binary)));
        }
    }
}
