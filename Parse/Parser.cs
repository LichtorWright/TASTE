using System;
using System.Collections.Generic;

namespace taste.Parse
{
    // ── Parser abstract base ───────────────────────────────────────────────

    /// <summary>
    /// Abstract base for all language parsers.
    /// Subclass this to parse a new source language — override the Read* methods
    /// to produce a <see cref="CodeFile"/> AST from source text.
    /// 
    /// The pipeline is: Source text → Parser → CodeFile (AST) → Emitter → Output text.
    /// 
    /// Each Parser subclass should consult its <see cref="LanguageProfile"/>
    /// from <see cref="LanguageMatrix"/> for language-specific syntax patterns
    /// (keywords, comment styles, statement terminators, etc.) rather than
    /// hardcoding them.
    /// 
    /// All Read* methods are virtual with NotSupportedException defaults, so
    /// subclasses only need to override the methods they actually implement.
    /// </summary>
    public abstract class Parser
    {
        /// <summary>
        /// The language profile this parser uses for syntax patterns.
        /// Subclasses should consult this for keywords, delimiters, and templates
        /// rather than hardcoding language-specific strings.
        /// </summary>
        public LanguageProfile Profile { get; }

        protected Parser(LanguageProfile profile)
        {
            Profile = profile;
        }

        // ── Public entry point ─────────────────────────────────────────────────

        /// <summary>
        /// Parse a complete source file into a <see cref="CodeFile"/> AST.
        /// This is the main entry point — call this to transpile a file.
        /// </summary>
        /// <param name="source">The full source text of the file.</param>
        /// <param name="filePath">The file path (used for metadata in the AST).</param>
        /// <returns>A fully-populated <see cref="CodeFile"/> AST.</returns>
        public abstract CodeFile ParseFile(string source, string filePath);

        // ── Top-level parsing hooks ─────────────────────────────────────────────

        /// <summary>Parse file-scope directives (#include, import, use, etc.).</summary>
        protected virtual void ReadFileScopeDirectives(string source, CodeFile file)
            => throw new NotSupportedException($"{Profile.Name} parser does not support file-scope directives.");

        /// <summary>Parse using/import directives.</summary>
        protected virtual void ReadUsings(string source, CodeFile file)
            => throw new NotSupportedException($"{Profile.Name} parser does not support using directives.");

        /// <summary>Parse a namespace/module/package declaration.</summary>
        protected virtual Namespace ReadNamespace(string source)
            => throw new NotSupportedException($"{Profile.Name} parser does not support namespaces.");

        /// <summary>Parse a class/struct/type declaration.</summary>
        protected virtual Class ReadClass(string source)
            => throw new NotSupportedException($"{Profile.Name} parser does not support class declarations.");

        /// <summary>Parse an interface/protocol/trait declaration.</summary>
        protected virtual InterfaceDeclaration ReadInterface(string source)
            => throw new NotSupportedException($"{Profile.Name} parser does not support interface declarations.");

        /// <summary>Parse an enum declaration.</summary>
        protected virtual EnumDecl ReadEnum(string source)
            => throw new NotSupportedException($"{Profile.Name} parser does not support enum declarations.");

        /// <summary>Parse a sum type (discriminated union) declaration.</summary>
        protected virtual SumTypeDeclaration ReadSumType(string source)
            => throw new NotSupportedException($"{Profile.Name} parser does not support sum type declarations.");

        /// <summary>Parse a mixin/extension/impl declaration.</summary>
        protected virtual MixinDeclaration ReadMixin(string source)
            => throw new NotSupportedException($"{Profile.Name} parser does not support mixin declarations.");

        /// <summary>Parse a type alias (typedef/using/type) declaration.</summary>
        protected virtual TypeAliasDeclaration ReadTypeAlias(string source)
            => throw new NotSupportedException($"{Profile.Name} parser does not support type alias declarations.");

        /// <summary>Parse a delegate/function type declaration.</summary>
        protected virtual DelegateDecl ReadDelegate(string source)
            => throw new NotSupportedException($"{Profile.Name} parser does not support delegate declarations.");

        // ── Member parsing hooks ────────────────────────────────────────────────

        /// <summary>Parse a field/member variable declaration.</summary>
        protected virtual Field ReadField(string source)
            => throw new NotSupportedException($"{Profile.Name} parser does not support field declarations.");

        /// <summary>Parse a property (getter/setter) declaration.</summary>
        protected virtual Property ReadProperty(string source)
            => throw new NotSupportedException($"{Profile.Name} parser does not support property declarations.");

        /// <summary>Parse a method/function declaration.</summary>
        protected virtual Method ReadMethod(string source)
            => throw new NotSupportedException($"{Profile.Name} parser does not support method declarations.");

        /// <summary>Parse a constructor declaration.</summary>
        protected virtual Method ReadConstructor(string source)
            => throw new NotSupportedException($"{Profile.Name} parser does not support constructor declarations.");

        /// <summary>Parse a destructor declaration.</summary>
        protected virtual Method ReadDestructor(string source)
            => throw new NotSupportedException($"{Profile.Name} parser does not support destructor declarations.");

        /// <summary>Parse an operator overload declaration.</summary>
        protected virtual OperatorOverload ReadOperatorOverload(string source)
            => throw new NotSupportedException($"{Profile.Name} parser does not support operator overload declarations.");

        /// <summary>Parse an indexer (bracket access) declaration.</summary>
        protected virtual Indexer ReadIndexer(string source)
            => throw new NotSupportedException($"{Profile.Name} parser does not support indexer declarations.");

        /// <summary>Parse an event declaration.</summary>
        protected virtual Event ReadEvent(string source)
            => throw new NotSupportedException($"{Profile.Name} parser does not support event declarations.");

        /// <summary>Parse a constant declaration.</summary>
        protected virtual Constant ReadConstant(string source)
            => throw new NotSupportedException($"{Profile.Name} parser does not support constant declarations.");

        // ── Statement parsing hooks ─────────────────────────────────────────────

        /// <summary>Parse a block of statements (method body, etc.).</summary>
        protected virtual List<Statement> ReadBlock(string source)
            => throw new NotSupportedException($"{Profile.Name} parser does not support block parsing.");

        /// <summary>Parse a single statement from source text.</summary>
        protected virtual Statement ReadStatement(string source)
            => throw new NotSupportedException($"{Profile.Name} parser does not support statement parsing.");

        // ── Expression parsing hooks ────────────────────────────────────────────

        /// <summary>Parse an expression from source text.</summary>
        protected virtual Expression ReadExpression(string source)
            => throw new NotSupportedException($"{Profile.Name} parser does not support expression parsing.");

        // ── Helper utilities ───────────────────────────────────────────────────

        /// <summary>
        /// Convenience accessor for the language's line comment prefix (e.g. "//", "#", "--").
        /// </summary>
        protected string LineComment => Profile.LineComment;

        /// <summary>
        /// Convenience accessor for the language's statement terminator (e.g. ";", "", "").
        /// </summary>
        protected string StatementTerminator => Profile.StatementTerminator;

        /// <summary>
        /// Convenience accessor for the language's namespace separator (e.g. "::", ".", "/").
        /// </summary>
        protected string NamespaceSeparator => Profile.NamespaceSeparator;

        /// <summary>
        /// Look up a keyword for a given <see cref="CodePart"/> in this parser's language.
        /// Returns empty string if the part is not defined for this language.
        /// </summary>
        protected string KeywordFor(CodePart part) => Profile.KeywordFor(part);

        /// <summary>
        /// Look up a syntax template for a given <see cref="CodePart"/> in this parser's language.
        /// Returns null if the part has no template defined.
        /// </summary>
        protected string TemplateFor(CodePart part) => Profile.TemplateFor(part);
    }
}