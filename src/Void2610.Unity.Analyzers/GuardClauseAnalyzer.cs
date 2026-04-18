using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Void2610.Unity.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class GuardClauseAnalyzer : DiagnosticAnalyzer
    {
        // ガード節（else なしの if 文で本体が return/throw/continue/break の単一文）は1行で記述する
        public static readonly DiagnosticDescriptor VUA3004 = new DiagnosticDescriptor(
            "VUA3004",
            "ガード節は1行で記述してください",
            "ガード節 '{0}' は1行で記述してください",
            "Style",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(VUA3004);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeIfStatement, SyntaxKind.IfStatement);
        }

        private static void AnalyzeIfStatement(SyntaxNodeAnalysisContext context)
        {
            if (GeneratedCodeHelper.IsGenerated(context.Node.SyntaxTree)) return;

            var ifStatement = (IfStatementSyntax)context.Node;

            // else がある場合はガード節ではない
            if (ifStatement.Else != null) return;

            // ネストされた if（else if チェーンの一部）は除外
            if (ifStatement.Parent is ElseClauseSyntax) return;

            // 本体からガード文を取得
            var guardStatement = GetGuardStatement(ifStatement);
            if (guardStatement == null) return;

            // 既に1行で書かれている場合はOK
            var ifLineSpan = ifStatement.GetLocation().GetLineSpan();
            if (ifLineSpan.StartLinePosition.Line == ifLineSpan.EndLinePosition.Line) return;

            var conditionText = ifStatement.Condition.ToString();
            var diagnostic = Diagnostic.Create(
                VUA3004,
                ifStatement.IfKeyword.GetLocation(),
                conditionText);
            context.ReportDiagnostic(diagnostic);
        }

        /// <summary>
        /// if文の本体がガード節（return/throw/continue/break の単一文）なら返す
        /// </summary>
        internal static StatementSyntax GetGuardStatement(IfStatementSyntax ifStatement)
        {
            StatementSyntax statement;

            if (ifStatement.Statement is BlockSyntax block)
            {
                // ブロック内が単一文でなければガード節ではない
                if (block.Statements.Count != 1) return null;
                statement = block.Statements[0];
            }
            else
            {
                // ブロックなしの直接文
                statement = ifStatement.Statement;
            }

            if (statement is ReturnStatementSyntax ||
                statement is ThrowStatementSyntax ||
                statement is ContinueStatementSyntax ||
                statement is BreakStatementSyntax)
            {
                return statement;
            }

            return null;
        }
    }
}
