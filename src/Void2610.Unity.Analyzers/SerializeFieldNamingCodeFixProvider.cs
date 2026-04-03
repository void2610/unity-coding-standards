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

namespace Void2610.Unity.Analyzers
{
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public sealed class SerializeFieldNamingCodeFixProvider : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds =>
            ImmutableArray.Create("VUA2001", "VUA2002", "VUA2003", "VUA2004");

        public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var diagnostic = context.Diagnostics[0];
            var node = root.FindNode(diagnostic.Location.SourceSpan);

            var variableDeclarator = node.FirstAncestorOrSelf<VariableDeclaratorSyntax>();
            if (variableDeclarator == null)
                return;

            var oldName = variableDeclarator.Identifier.Text;
            string newName;
            string title;

            if (diagnostic.Id == "VUA2001")
            {
                // [SerializeField]privateフィールド: _プレフィックス除去
                newName = oldName.TrimStart('_');
                title = $"'_' プレフィックスを除去して '{newName}' にリネーム";
            }
            else if (diagnostic.Id == "VUA2002")
            {
                // 通常privateフィールド: _プレフィックス追加
                newName = "_" + oldName;
                title = $"'_' プレフィックスを追加して '{newName}' にリネーム";
            }
            else if (diagnostic.Id == "VUA2003")
            {
                // [SerializeField]protected/publicフィールド: 先頭を小文字に変換
                newName = char.ToLower(oldName[0]) + oldName.Substring(1);
                title = $"キャメルケース '{newName}' にリネーム";
            }
            else
            {
                // 通常protectedフィールド: 先頭を大文字に変換
                newName = char.ToUpper(oldName[0]) + oldName.Substring(1);
                title = $"パスカルケース '{newName}' にリネーム";
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    title,
                    ct => RenameFieldAsync(context.Document, variableDeclarator, newName, ct),
                    nameof(SerializeFieldNamingCodeFixProvider) + diagnostic.Id),
                diagnostic);
        }

        private static async Task<Document> RenameFieldAsync(
            Document document, VariableDeclaratorSyntax variableDeclarator,
            string newName, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            var newIdentifier = SyntaxFactory.Identifier(newName)
                .WithLeadingTrivia(variableDeclarator.Identifier.LeadingTrivia)
                .WithTrailingTrivia(variableDeclarator.Identifier.TrailingTrivia);
            var newDeclarator = variableDeclarator.WithIdentifier(newIdentifier);

            var newRoot = root.ReplaceNode(variableDeclarator, newDeclarator);
            return document.WithSyntaxRoot(newRoot);
        }
    }
}
