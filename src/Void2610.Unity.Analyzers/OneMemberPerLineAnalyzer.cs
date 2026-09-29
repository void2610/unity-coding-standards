using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Void2610.Unity.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class OneMemberPerLineAnalyzer : DiagnosticAnalyzer
    {
        // 直前のメンバーと同じ行で始まるメンバー宣言を警告 (行単位の置換・削除で改行ごと消えた連結を拾う)
        public static readonly DiagnosticDescriptor VUA3005 = new DiagnosticDescriptor(
            "VUA3005",
            "1 行に複数のメンバー宣言があります",
            "メンバー '{0}' を直前のメンバーと別の行に置いてください",
            "Style",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(VUA3005);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeType,
                SyntaxKind.ClassDeclaration,
                SyntaxKind.StructDeclaration,
                SyntaxKind.InterfaceDeclaration,
                SyntaxKind.RecordDeclaration,
                SyntaxKind.RecordStructDeclaration);
        }

        private static void AnalyzeType(SyntaxNodeAnalysisContext context)
        {
            if (GeneratedCodeHelper.IsGenerated(context.Node.SyntaxTree)) return;
            var members = ((TypeDeclarationSyntax)context.Node).Members;

            for (var i = 1; i < members.Count; i++)
            {
                var previousEnd = members[i - 1].GetLastToken().GetLocation().GetLineSpan().EndLinePosition.Line;
                var firstToken = members[i].GetFirstToken();
                if (firstToken.GetLocation().GetLineSpan().StartLinePosition.Line != previousEnd) continue;

                context.ReportDiagnostic(Diagnostic.Create(VUA3005, firstToken.GetLocation(), MemberName(members[i])));
            }
        }

        private static string MemberName(MemberDeclarationSyntax member)
        {
            switch (member)
            {
                case FieldDeclarationSyntax field: return field.Declaration.Variables[0].Identifier.Text;
                case EventFieldDeclarationSyntax eventField: return eventField.Declaration.Variables[0].Identifier.Text;
                case PropertyDeclarationSyntax property: return property.Identifier.Text;
                case MethodDeclarationSyntax method: return method.Identifier.Text;
                case BaseTypeDeclarationSyntax type: return type.Identifier.Text;
                default: return member.Kind().ToString();
            }
        }
    }
}
