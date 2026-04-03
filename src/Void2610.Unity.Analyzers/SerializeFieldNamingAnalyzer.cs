using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Void2610.Unity.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class SerializeFieldNamingAnalyzer : DiagnosticAnalyzer
    {
        // 通常のprivateフィールドに_プレフィックスがない場合の警告
        public static readonly DiagnosticDescriptor VUA2002 = new DiagnosticDescriptor(
            "VUA2002",
            "privateフィールドには'_'プレフィックスが必要です",
            "privateフィールド '{0}' には '_' プレフィックスを付けてください",
            "Naming",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        // [SerializeField]付きフィールドに_プレフィックスがある場合の警告
        public static readonly DiagnosticDescriptor VUA2001 = new DiagnosticDescriptor(
            "VUA2001",
            "[SerializeField]フィールドには'_'プレフィックスを付けないでください",
            "[SerializeField]フィールド '{0}' から '_' プレフィックスを除去してください",
            "Naming",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        // [SerializeField]付きprotected/publicフィールドがキャメルケースでない場合の警告
        public static readonly DiagnosticDescriptor VUA2003 = new DiagnosticDescriptor(
            "VUA2003",
            "[SerializeField]フィールドはキャメルケースにしてください",
            "[SerializeField]フィールド '{0}' はキャメルケース（先頭小文字）にしてください",
            "Naming",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        // 非[SerializeField]のprotectedフィールドがパスカルケースでない場合の警告
        public static readonly DiagnosticDescriptor VUA2004 = new DiagnosticDescriptor(
            "VUA2004",
            "protectedフィールドはパスカルケースにしてください",
            "protectedフィールド '{0}' はパスカルケース（先頭大文字）にしてください",
            "Naming",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(VUA2002, VUA2001, VUA2003, VUA2004);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSymbolAction(AnalyzeField, SymbolKind.Field);
        }

        private static void AnalyzeField(SymbolAnalysisContext context)
        {
            if (GeneratedCodeHelper.IsGenerated(context.Symbol)) return;
            var field = (IFieldSymbol)context.Symbol;

            // const, static, コンパイラ生成フィールドは除外
            if (field.IsConst || field.IsStatic || field.IsImplicitlyDeclared)
                return;

            var hasSerializeField = field.GetAttributes().Any(a =>
                a.AttributeClass?.Name is "SerializeField" or "SerializeFieldAttribute"
                    or "SerializeReference" or "SerializeReferenceAttribute");

            if (field.DeclaredAccessibility == Accessibility.Private)
            {
                // privateフィールドの命名チェック
                var startsWithUnderscore = field.Name.StartsWith("_");

                if (hasSerializeField)
                {
                    // [SerializeField]付き → _プレフィックス不要
                    if (startsWithUnderscore)
                    {
                        context.ReportDiagnostic(
                            Diagnostic.Create(VUA2001, field.Locations[0], field.Name));
                    }
                }
                else
                {
                    // 通常のprivateフィールド → _プレフィックス必須
                    if (!startsWithUnderscore)
                    {
                        context.ReportDiagnostic(
                            Diagnostic.Create(VUA2002, field.Locations[0], field.Name));
                    }
                }
            }
            else if (field.DeclaredAccessibility == Accessibility.Protected
                  || field.DeclaredAccessibility == Accessibility.ProtectedOrInternal)
            {
                // protectedフィールドの命名チェック
                if (hasSerializeField)
                {
                    // [SerializeField]付き → キャメルケース必須（先頭小文字）
                    if (field.Name.Length > 0 && char.IsUpper(field.Name[0]))
                    {
                        context.ReportDiagnostic(
                            Diagnostic.Create(VUA2003, field.Locations[0], field.Name));
                    }
                }
                else
                {
                    // 通常のprotectedフィールド → パスカルケース必須（先頭大文字）
                    if (field.Name.Length > 0 && char.IsLower(field.Name[0]))
                    {
                        context.ReportDiagnostic(
                            Diagnostic.Create(VUA2004, field.Locations[0], field.Name));
                    }
                }
            }
        }
    }
}
