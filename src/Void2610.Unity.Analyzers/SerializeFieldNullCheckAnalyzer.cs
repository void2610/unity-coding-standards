using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Void2610.Unity.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class SerializeFieldNullCheckAnalyzer : DiagnosticAnalyzer
    {
        // [SerializeField]フィールドに対する防御的nullチェックを禁止
        public static readonly DiagnosticDescriptor VUA1001 = new DiagnosticDescriptor(
            "VUA1001",
            "[SerializeField]フィールドのnullチェックは不要です",
            "[SerializeField]フィールド '{0}' のnullチェックを削除してください。設定ミスは即座にクラッシュさせてください",
            "Design",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(VUA1001);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            // x == null, x != null
            context.RegisterSyntaxNodeAction(AnalyzeBinaryExpression,
                SyntaxKind.EqualsExpression, SyntaxKind.NotEqualsExpression);
            // x?.Member
            context.RegisterSyntaxNodeAction(AnalyzeConditionalAccess,
                SyntaxKind.ConditionalAccessExpression);
            // x ?? fallback
            context.RegisterSyntaxNodeAction(AnalyzeCoalesce,
                SyntaxKind.CoalesceExpression);
            // x is null, x is not null
            context.RegisterSyntaxNodeAction(AnalyzeIsPattern,
                SyntaxKind.IsPatternExpression);
            // if (x), if (!x)
            context.RegisterSyntaxNodeAction(AnalyzeIfStatement,
                SyntaxKind.IfStatement);
        }

        private static void AnalyzeBinaryExpression(SyntaxNodeAnalysisContext context)
        {
            var binary = (BinaryExpressionSyntax)context.Node;

            // 片方がnullリテラルの場合のみ対象
            ExpressionSyntax target = null;
            if (IsNullLiteral(binary.Right))
                target = binary.Left;
            else if (IsNullLiteral(binary.Left))
                target = binary.Right;

            if (target == null)
                return;

            ReportIfSerializeField(context, target, binary.GetLocation());
        }

        private static void AnalyzeConditionalAccess(SyntaxNodeAnalysisContext context)
        {
            var conditionalAccess = (ConditionalAccessExpressionSyntax)context.Node;
            ReportIfSerializeField(context, conditionalAccess.Expression, conditionalAccess.GetLocation());
        }

        private static void AnalyzeCoalesce(SyntaxNodeAnalysisContext context)
        {
            var coalesce = (BinaryExpressionSyntax)context.Node;
            ReportIfSerializeField(context, coalesce.Left, coalesce.GetLocation());
        }

        private static void AnalyzeIsPattern(SyntaxNodeAnalysisContext context)
        {
            var isPattern = (IsPatternExpressionSyntax)context.Node;

            if (!IsNullPattern(isPattern.Pattern))
                return;

            ReportIfSerializeField(context, isPattern.Expression, isPattern.GetLocation());
        }

        private static void AnalyzeIfStatement(SyntaxNodeAnalysisContext context)
        {
            var ifStatement = (IfStatementSyntax)context.Node;
            var target = GetNullGuardLikeConditionTarget(ifStatement.Condition);

            if (target == null)
                return;

            ReportIfSerializeField(context, target, ifStatement.Condition.GetLocation());
        }

        private static void ReportIfSerializeField(
            SyntaxNodeAnalysisContext context, ExpressionSyntax expression, Location location)
        {
            if (GeneratedCodeHelper.IsGenerated(context.Node.SyntaxTree)) return;
            var symbol = context.SemanticModel.GetSymbolInfo(expression).Symbol;
            if (!(symbol is IFieldSymbol field))
                return;

            if (field.Type.SpecialType == SpecialType.System_Boolean)
                return;

            var hasSerializeField = field.GetAttributes().Any(a =>
                a.AttributeClass?.Name == "SerializeField" ||
                a.AttributeClass?.Name == "SerializeFieldAttribute");

            if (hasSerializeField)
            {
                context.ReportDiagnostic(Diagnostic.Create(VUA1001, location, field.Name));
            }
        }

        private static ExpressionSyntax GetNullGuardLikeConditionTarget(ExpressionSyntax condition)
        {
            while (condition is ParenthesizedExpressionSyntax parenthesized)
                condition = parenthesized.Expression;

            if (condition is PrefixUnaryExpressionSyntax prefixUnary &&
                prefixUnary.IsKind(SyntaxKind.LogicalNotExpression))
            {
                condition = prefixUnary.Operand;
            }

            while (condition is ParenthesizedExpressionSyntax parenthesized)
                condition = parenthesized.Expression;

            return condition is IdentifierNameSyntax or MemberAccessExpressionSyntax ? condition : null;
        }

        private static bool IsNullLiteral(ExpressionSyntax expression) =>
            expression.IsKind(SyntaxKind.NullLiteralExpression);

        private static bool IsNullPattern(PatternSyntax pattern)
        {
            // is null
            if (pattern is ConstantPatternSyntax constant)
                return constant.Expression.IsKind(SyntaxKind.NullLiteralExpression);

            // is not null
            if (pattern is UnaryPatternSyntax unary && unary.OperatorToken.IsKind(SyntaxKind.NotKeyword))
                return IsNullPattern(unary.Pattern);

            return false;
        }
    }
}
