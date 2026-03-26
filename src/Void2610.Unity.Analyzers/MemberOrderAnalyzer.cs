using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Void2610.Unity.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class MemberOrderAnalyzer : DiagnosticAnalyzer
    {
        // クラスメンバーの宣言順序違反
        public static readonly DiagnosticDescriptor VUA3002 = new DiagnosticDescriptor(
            "VUA3002",
            "クラスメンバーの宣言順序が不正です",
            "メンバー '{0}' ({1}) は '{2}' より前に宣言してください",
            "Style",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        // クラスメンバー間の空行ルール違反
        public static readonly DiagnosticDescriptor VUA3003 = new DiagnosticDescriptor(
            "VUA3003",
            "クラスメンバー間の空行が不正です",
            "メンバー '{0}' の前の空行ルールに違反しています",
            "Style",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        // メンバーカテゴリ定義
        internal enum MemberCategory
        {
            NestedEnum = 0,
            SerializeField = 1,
            PublicProperty = 2,
            Constant = 3,
            PrivateField = 4,
            Constructor = 5,
            PublicMethodOneLine = 6,
            ProtectedMethodOneLine = 7,
            PrivateMethodOneLine = 8,
            PublicMethodMultiLine = 9,
            ProtectedMethodMultiLine = 10,
            PrivateMethodMultiLine = 11,
            UnityEvent = 12,
            Cleanup = 13,
            Excluded = -1
        }

        // カテゴリ名の表示用
        private static readonly Dictionary<MemberCategory, string> CategoryNames = new Dictionary<MemberCategory, string>
        {
            { MemberCategory.NestedEnum, "Enum" },
            { MemberCategory.SerializeField, "SerializeField" },
            { MemberCategory.PublicProperty, "public properties" },
            { MemberCategory.Constant, "constants" },
            { MemberCategory.PrivateField, "private fields" },
            { MemberCategory.Constructor, "constructors" },
            { MemberCategory.PublicMethodOneLine, "public methods (one line)" },
            { MemberCategory.ProtectedMethodOneLine, "protected methods (one line)" },
            { MemberCategory.PrivateMethodOneLine, "private methods (one line)" },
            { MemberCategory.PublicMethodMultiLine, "public methods (multi line)" },
            { MemberCategory.ProtectedMethodMultiLine, "protected methods (multi line)" },
            { MemberCategory.PrivateMethodMultiLine, "private methods (multi line)" },
            { MemberCategory.UnityEvent, "Unity events" },
            { MemberCategory.Cleanup, "cleanup" }
        };

        // Unityイベントメソッド名
        private static readonly HashSet<string> UnityEventNames = new HashSet<string>
        {
            "Awake", "Start", "Update", "FixedUpdate", "LateUpdate",
            "OnEnable", "OnDisable",
            "OnCollisionEnter", "OnCollisionExit", "OnCollisionStay",
            "OnCollisionEnter2D", "OnCollisionExit2D", "OnCollisionStay2D",
            "OnTriggerEnter", "OnTriggerExit", "OnTriggerStay",
            "OnTriggerEnter2D", "OnTriggerExit2D", "OnTriggerStay2D",
            "OnBecameVisible", "OnBecameInvisible",
            "OnApplicationFocus", "OnApplicationPause", "OnApplicationQuit",
            "OnGUI", "OnDrawGizmos", "OnDrawGizmosSelected", "OnValidate", "Reset",
            "OnMouseDown", "OnMouseUp", "OnMouseEnter", "OnMouseExit",
            "OnMouseOver", "OnMouseDrag",
            "OnAnimatorIK", "OnAnimatorMove",
            "OnPointerClick", "OnPointerDown", "OnPointerUp",
            "OnPointerEnter", "OnPointerExit",
            "OnDrag", "OnBeginDrag", "OnEndDrag", "OnDrop",
            "OnSelect", "OnDeselect", "OnSubmit", "OnCancel", "OnScroll",
            "OnMove", "OnInitializePotentialDrag"
        };

        // クリーンアップメソッド名
        private static readonly HashSet<string> CleanupNames = new HashSet<string>
        {
            "OnDestroy", "Dispose"
        };

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(VUA3002, VUA3003);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeTypeDeclaration,
                SyntaxKind.ClassDeclaration, SyntaxKind.StructDeclaration);
        }

        private static void AnalyzeTypeDeclaration(SyntaxNodeAnalysisContext context)
        {
            if (GeneratedCodeHelper.IsGenerated(context.Node.SyntaxTree)) return;
            var typeDeclaration = (TypeDeclarationSyntax)context.Node;
            var members = typeDeclaration.Members;

            if (members.Count == 0)
                return;

            // 各メンバーのカテゴリを判定
            var categorizedMembers = new List<(MemberDeclarationSyntax Member, MemberCategory Category, string Name)>();
            foreach (var member in members)
            {
                var category = ClassifyMember(member);
                if (category == MemberCategory.Excluded)
                    continue;

                var name = GetMemberName(member);
                categorizedMembers.Add((member, category, name));
            }

            var firstMember = typeDeclaration.Members[0];
            var syntaxTree = context.Node.SyntaxTree;
            var sourceText = syntaxTree.GetText(context.CancellationToken);
            var openingBraceLine = syntaxTree.GetLineSpan(typeDeclaration.OpenBraceToken.Span).EndLinePosition.Line;
            var firstMemberStartLine = GetMemberAnchorLine(syntaxTree, sourceText, firstMember);
            var blankLinesAfterOpeningBrace = firstMemberStartLine - openingBraceLine - 1;
            if (blankLinesAfterOpeningBrace > 0)
            {
                var location = firstMember is FieldDeclarationSyntax firstField
                    ? firstField.Declaration.Variables.First().Identifier.GetLocation()
                    : GetMemberIdentifierLocation(firstMember);

                context.ReportDiagnostic(Diagnostic.Create(
                    VUA3003,
                    location,
                    GetMemberName(firstMember)));
            }

            if (categorizedMembers.Count <= 1)
                return;

            // 順序チェック: カテゴリインデックスが単調非減少であること
            var maxCategory = categorizedMembers[0].Category;
            var maxCategoryName = CategoryNames[maxCategory];

            for (var i = 1; i < categorizedMembers.Count; i++)
            {
                var current = categorizedMembers[i];

                if (current.Category < maxCategory)
                {
                    // 順序違反を報告
                    var location = current.Member is FieldDeclarationSyntax fieldDecl
                        ? fieldDecl.Declaration.Variables.First().Identifier.GetLocation()
                        : GetMemberIdentifierLocation(current.Member);

                    context.ReportDiagnostic(Diagnostic.Create(
                        VUA3002,
                        location,
                        current.Name,
                        CategoryNames[current.Category],
                        maxCategoryName));
                }
                else if (current.Category > maxCategory)
                {
                    maxCategory = current.Category;
                    maxCategoryName = CategoryNames[maxCategory];
                }
            }

            // 空行ルールチェック（定数/通常フィールドカテゴリのみ）:
            // 1. Constant <-> PrivateField の境界は空行1行
            // 2. 同一カテゴリ内の連続空行2行以上は禁止
            AnalyzeMemberSpacing(context, categorizedMembers);
        }

        private static void AnalyzeMemberSpacing(
            SyntaxNodeAnalysisContext context,
            List<(MemberDeclarationSyntax Member, MemberCategory Category, string Name)> members)
        {
            if (members.Count <= 1)
            {
                return;
            }

            var syntaxTree = context.Node.SyntaxTree;
            var sourceText = syntaxTree.GetText(context.CancellationToken);

            for (var i = 1; i < members.Count; i++)
            {
                var previous = members[i - 1];
                var current = members[i];

                var previousEndLine = syntaxTree.GetLineSpan(previous.Member.Span).EndLinePosition.Line;
                var currentStartLine = GetMemberAnchorLine(syntaxTree, sourceText, current.Member);
                var blankLines = currentStartLine - previousEndLine - 1;

                var previousIsFieldGroup = IsFieldGroupCategory(previous.Category);
                var currentIsFieldGroup = IsFieldGroupCategory(current.Category);
                if (!previousIsFieldGroup || !currentIsFieldGroup)
                {
                    continue;
                }

                var requiresSingleBlankLine = previous.Category != current.Category;

                var hasViolation =
                    (requiresSingleBlankLine && blankLines != 1) ||
                    (!requiresSingleBlankLine && blankLines > 1);

                if (!hasViolation)
                {
                    continue;
                }

                var location = current.Member switch
                {
                    FieldDeclarationSyntax field => field.Declaration.Variables.First().Identifier.GetLocation(),
                    _ => GetMemberIdentifierLocation(current.Member)
                };

                context.ReportDiagnostic(Diagnostic.Create(
                    VUA3003,
                    location,
                    current.Name));
            }
        }

        internal static int GetMemberAnchorLine(
            SyntaxTree syntaxTree,
            SourceText sourceText,
            MemberDeclarationSyntax member)
        {
            var fullSpan = member.FullSpan;
            var declarationSpan = member.Span;

            var anchorPosition = declarationSpan.Start;
            for (var position = fullSpan.Start; position < declarationSpan.Start; position++)
            {
                if (!char.IsWhiteSpace(sourceText[position]))
                {
                    anchorPosition = position;
                    break;
                }
            }

            return sourceText.Lines.GetLineFromPosition(anchorPosition).LineNumber;
        }

        internal static bool IsFieldGroupCategory(MemberCategory category)
        {
            return category == MemberCategory.Constant
                || category == MemberCategory.PrivateField;
        }

        internal static MemberCategory ClassifyMember(MemberDeclarationSyntax member)
        {
            switch (member)
            {
                case EnumDeclarationSyntax _:
                    return MemberCategory.NestedEnum;

                case FieldDeclarationSyntax field:
                    return ClassifyField(field);

                case PropertyDeclarationSyntax property:
                    return ClassifyProperty(property);

                case ConstructorDeclarationSyntax _:
                    return MemberCategory.Constructor;

                case MethodDeclarationSyntax method:
                    return ClassifyMethod(method);

                // ネストされたクラス/構造体/インターフェース、デストラクタ、演算子、インデクサ、デリゲートは除外
                case ClassDeclarationSyntax _:
                case StructDeclarationSyntax _:
                case InterfaceDeclarationSyntax _:
                case RecordDeclarationSyntax _:
                case DestructorDeclarationSyntax _:
                case OperatorDeclarationSyntax _:
                case ConversionOperatorDeclarationSyntax _:
                case IndexerDeclarationSyntax _:
                case DelegateDeclarationSyntax _:
                case EventDeclarationSyntax _:
                case EventFieldDeclarationSyntax _:
                    return MemberCategory.Excluded;

                default:
                    return MemberCategory.Excluded;
            }
        }

        private static MemberCategory ClassifyField(FieldDeclarationSyntax field)
        {
            // [SerializeField] 属性チェック
            if (HasSerializeFieldAttribute(field))
                return MemberCategory.SerializeField;

            // const フィールド
            if (field.Modifiers.Any(SyntaxKind.ConstKeyword))
                return MemberCategory.Constant;

            // static readonly フィールド
            if (field.Modifiers.Any(SyntaxKind.StaticKeyword) &&
                field.Modifiers.Any(SyntaxKind.ReadOnlyKeyword))
                return MemberCategory.Constant;

            return MemberCategory.PrivateField;
        }

        private static MemberCategory ClassifyProperty(PropertyDeclarationSyntax property)
        {
            // publicプロパティのみ対象、protected/internalは除外
            if (property.Modifiers.Any(SyntaxKind.PublicKeyword))
                return MemberCategory.PublicProperty;

            return MemberCategory.Excluded;
        }

        private static MemberCategory ClassifyMethod(MethodDeclarationSyntax method)
        {
            var name = method.Identifier.Text;

            // クリーンアップメソッド判定（最優先）
            if (CleanupNames.Contains(name))
                return MemberCategory.Cleanup;

            // Unityイベントメソッド判定
            if (UnityEventNames.Contains(name))
                return MemberCategory.UnityEvent;

            // publicメソッド
            if (method.Modifiers.Any(SyntaxKind.PublicKeyword))
            {
                // 式本体メソッド
                if (method.ExpressionBody != null)
                    return MemberCategory.PublicMethodOneLine;

                return MemberCategory.PublicMethodMultiLine;
            }

            if (method.Modifiers.Any(SyntaxKind.ProtectedKeyword) ||
                method.Modifiers.Any(SyntaxKind.InternalKeyword))
            {
                if (method.ExpressionBody != null)
                    return MemberCategory.ProtectedMethodOneLine;

                return MemberCategory.ProtectedMethodMultiLine;
            }

            if (method.ExpressionBody != null)
                return MemberCategory.PrivateMethodOneLine;

            return MemberCategory.PrivateMethodMultiLine;
        }

        private static bool HasSerializeFieldAttribute(FieldDeclarationSyntax field)
        {
            foreach (var attrList in field.AttributeLists)
            {
                foreach (var attr in attrList.Attributes)
                {
                    var attrName = attr.Name.ToString();
                    if (attrName == "SerializeField" || attrName == "SerializeFieldAttribute")
                        return true;
                }
            }
            return false;
        }

        private static string GetMemberName(MemberDeclarationSyntax member)
        {
            switch (member)
            {
                case EnumDeclarationSyntax e:
                    return e.Identifier.Text;
                case FieldDeclarationSyntax f:
                    return f.Declaration.Variables.First().Identifier.Text;
                case PropertyDeclarationSyntax p:
                    return p.Identifier.Text;
                case ConstructorDeclarationSyntax c:
                    return c.Identifier.Text;
                case MethodDeclarationSyntax m:
                    return m.Identifier.Text;
                default:
                    return "unknown";
            }
        }

        private static Location GetMemberIdentifierLocation(MemberDeclarationSyntax member)
        {
            switch (member)
            {
                case EnumDeclarationSyntax e:
                    return e.Identifier.GetLocation();
                case PropertyDeclarationSyntax p:
                    return p.Identifier.GetLocation();
                case ConstructorDeclarationSyntax c:
                    return c.Identifier.GetLocation();
                case MethodDeclarationSyntax m:
                    return m.Identifier.GetLocation();
                default:
                    return member.GetLocation();
            }
        }
    }
}
