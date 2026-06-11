using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Void2610.Unity.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class EventSystemAnalyzer : DiagnosticAnalyzer
    {
        // C#標準のeventやActionフィールドの代わりにR3のSubjectを使用するよう警告する (報告のみ)。
        // event/Action → Subject の変換は呼び出し側 (Invoke/+=/代入) の書き換えを伴う意味的リファクタで、
        // 宣言だけを機械的に置換するとコンパイル不能になるため自動修正 (CodeFix) は提供しない。手動で置換する。
        public static readonly DiagnosticDescriptor VUA1002 = new DiagnosticDescriptor(
            "VUA1002",
            "イベントにはR3のSubjectを使用してください",
            "'{0}' はC#標準のイベント/デリゲートです。R3のSubject<T>を使用してください",
            "Design",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(VUA1002);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSymbolAction(AnalyzeField, SymbolKind.Field);
            context.RegisterSymbolAction(AnalyzeProperty, SymbolKind.Property);
            context.RegisterSymbolAction(AnalyzeEvent, SymbolKind.Event);
        }

        private static void AnalyzeEvent(SymbolAnalysisContext context)
        {
            if (GeneratedCodeHelper.IsGenerated(context.Symbol)) return;
            var eventSymbol = (IEventSymbol)context.Symbol;

            // コンパイラ生成は除外
            if (eventSymbol.IsImplicitlyDeclared)
                return;

            context.ReportDiagnostic(
                Diagnostic.Create(VUA1002, eventSymbol.Locations[0], eventSymbol.Name));
        }

        private static void AnalyzeField(SymbolAnalysisContext context)
        {
            if (GeneratedCodeHelper.IsGenerated(context.Symbol)) return;
            var field = (IFieldSymbol)context.Symbol;

            // コンパイラ生成フィールドは除外（eventのバッキングフィールドなど）
            if (field.IsImplicitlyDeclared)
                return;

            if (IsActionType(field.Type))
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(VUA1002, field.Locations[0], field.Name));
            }
        }

        private static void AnalyzeProperty(SymbolAnalysisContext context)
        {
            if (GeneratedCodeHelper.IsGenerated(context.Symbol)) return;
            var property = (IPropertySymbol)context.Symbol;

            if (IsActionType(property.Type))
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(VUA1002, property.Locations[0], property.Name));
            }
        }

        private static bool IsActionType(ITypeSymbol type)
        {
            if (type == null)
                return false;

            var name = type.Name;
            var ns = type.ContainingNamespace?.ToDisplayString();

            return ns == "System" && name == "Action";
        }
    }
}
