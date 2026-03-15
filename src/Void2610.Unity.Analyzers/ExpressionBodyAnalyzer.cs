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
                // switch式を含む場合は複数行でも許可（ブロック本体パスと一貫性を保つ）
                if (method.ExpressionBody.DescendantNodes().OfType<SwitchExpressionSyntax>().Any())
                    return;

                // コレクション初期化子や複雑なオブジェクト生成を含む場合は複数行を許可
                if (method.ExpressionBody.DescendantNodes().OfType<InitializerExpressionSyntax>().Any())
                    return;

                // 三項演算子が複数行にわたる場合は許可
                if (HasMultiLineConditionalExpression(method.ExpressionBody))
                    return;

                // 引数リストが複数行にわたる場合は許可
                if (HasMultiLineArgumentList(method.ExpressionBody))
                    return;

                // メソッドのシグネチャ（パラメータ）が複数行にわたる場合は許可
                if (!IsSingleLineSignature(method))
                    return;

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
            // 属性を除いたメソッドシグネチャの開始行から判定する
            // method.GetLocation() は属性行も含むため ReturnType を基準にする
            var startLine = method.ReturnType.GetLocation().GetLineSpan().StartLinePosition.Line;
            var endLine = method.SemicolonToken.GetLocation().GetLineSpan().EndLinePosition.Line;
            return startLine == endLine;
        }

        private static bool IsSingleLineSignature(MethodDeclarationSyntax method)
        {
            // パラメータリストが複数行にわたる場合は false
            var openParen = method.ParameterList.OpenParenToken.GetLocation().GetLineSpan().StartLinePosition.Line;
            var closeParen = method.ParameterList.CloseParenToken.GetLocation().GetLineSpan().EndLinePosition.Line;
            return openParen == closeParen;
        }

        private static bool HasMultiLineConditionalExpression(ArrowExpressionClauseSyntax expressionBody)
        {
            return expressionBody.DescendantNodes()
                .OfType<ConditionalExpressionSyntax>()
                .Any(c =>
                {
                    var start = c.GetLocation().GetLineSpan().StartLinePosition.Line;
                    var end = c.GetLocation().GetLineSpan().EndLinePosition.Line;
                    return start != end;
                });
        }

        private static bool HasMultiLineArgumentList(ArrowExpressionClauseSyntax expressionBody)
        {
            return expressionBody.DescendantNodes()
                .OfType<ArgumentListSyntax>()
                .Any(a =>
                {
                    var start = a.GetLocation().GetLineSpan().StartLinePosition.Line;
                    var end = a.GetLocation().GetLineSpan().EndLinePosition.Line;
                    return start != end;
                });
        }
    }
}
