using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Regira.GuideVerifier;

/// <summary>
/// Classifies a snippet as <see cref="SnippetKind.Declarations"/> (contains type / namespace / using /
/// delegate declarations that must live at namespace scope) or <see cref="SnippetKind.Statements"/>
/// (statements or expressions that need wrapping in a method body). Uses Roslyn: parse the block once as
/// a compilation unit and inspect the top-level members it produced.
/// </summary>
public static class SnippetClassifier
{
    public static SnippetKind Classify(string code)
    {
        var root = CSharpSyntaxTree.ParseText(code).GetCompilationUnitRoot();

        // A top-level type/namespace/delegate declaration, or any using directive, means the block wants
        // namespace scope. (Roslyn parses a bare statement block as a GlobalStatement member, not one of
        // these — so the presence of a "real" member declaration is the discriminator.)
        // A `Program.cs` block opens with usings and then runs statements — the shape every host guide
        // shows. Its usings belong at file scope, but the body is still statements, so global statements
        // win over the usings when both are present.
        if (root.Members.Any(m => m is GlobalStatementSyntax))
        {
            return SnippetKind.Statements;
        }

        var hasDeclarations =
            root.Usings.Count > 0 ||
            root.Members.Any(m => m is BaseTypeDeclarationSyntax
                or NamespaceDeclarationSyntax
                or FileScopedNamespaceDeclarationSyntax
                or DelegateDeclarationSyntax);

        return hasDeclarations ? SnippetKind.Declarations : SnippetKind.Statements;
    }
}
