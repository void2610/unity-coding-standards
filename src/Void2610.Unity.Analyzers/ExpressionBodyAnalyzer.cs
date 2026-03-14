using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Void2610.Unity.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class ExpressionBodyAnalyzer : DiagnosticAnalyzer
    {
        // 単一文の対象メソッドには式本体を使用し、式本体は1行に収めるよう警告
        public static readonly DiagnosticDescriptor VUA3001 = new DiagnosticDescriptor(
            "VUA3001",
            "対象メソッドは1行の式本体で記述してください",
            "メソッド '{0}' は1行の式本体 (=>) で記述してください",
            "Style",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(VUA3001);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
        }

        private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
        {
            if (GeneratedCodeHelper.IsGenerated(context.Node.SyntaxTree)) return;
            var method = (MethodDeclarationSyntax)context.Node;

            if (method.ExpressionBody != null)
            {
                if (IsSingleLineExpressionBody(method))
                    return;

                var expressionBodyDiagnostic = Diagnostic.Create(
                    VUA3001,
                    method.Identifier.GetLocation(),
                    method.Identifier.Text);
                context.ReportDiagnostic(expressionBodyDiagnostic);
                return;
            }

            // ブロック本体がない場合は除外（抽象メソッド等）
            if (method.Body == null)
                return;

            // プリプロセッサ付きメソッドは code fix で壊れやすいため除外
            if (method.ContainsDirectives)
                return;

            // publicメソッドのみ対象
            if (!method.Modifiers.Any(SyntaxKind.PublicKeyword))
                return;

            // IDisposable.Disposeメソッドは除外
            if (method.Identifier.Text == "Dispose" && method.ParameterList.Parameters.Count == 0)
                return;

            // ステートメントが1つだけの場合に警告
            if (method.Body.Statements.Count != 1)
                return;

            // 式本体に変換可能な文のみ対象（return文、式文のみ）
            var statement = method.Body.Statements[0];
            if (!(statement is ReturnStatementSyntax) && !(statement is ExpressionStatementSyntax))
                return;

            // switch式を含む場合は除外（複雑になるため式本体にしない）
            if (statement.DescendantNodes().OfType<SwitchExpressionSyntax>().Any())
                return;

            var diagnostic = Diagnostic.Create(
                VUA3001,
                method.Identifier.GetLocation(),
                method.Identifier.Text);
            context.ReportDiagnostic(diagnostic);
        }

        private static bool IsSingleLineExpressionBody(MethodDeclarationSyntax method)
        {
            var startLine = method.GetLocation().GetLineSpan().StartLinePosition.Line;
            var endLine = method.SemicolonToken.GetLocation().GetLineSpan().EndLinePosition.Line;
            return startLine == endLine;
        }
    }
}
