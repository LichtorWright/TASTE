using System.Collections.Generic;

namespace taste
{
    // ── CodePart enum ─────────────────────────────────────────────────────────

    /// <summary>
    /// Every structural element that can appear in source code.
    /// Maps 1:1 to AST node types but is language-agnostic — each <see cref="LanguageProfile"/>
    /// defines how a given CodePart is represented in its target language.
    /// </summary>
    public enum CodePart
    {
        // ── Structural / Module ───────────────────────────────────────────
        CompilationUnit,        // The root file node
        Namespace,              // namespace / module / package
        UsingDirective,         // using / import / include / use
        FileScopeDirective,     // #include / mod / require at file level

        // ── Type Declarations ─────────────────────────────────────────────
        Class,                  // class / struct / type
        Struct,                 // struct (value type)
        Interface,              // interface / protocol / trait
        Enum,                   // enum (simple)
        EnumClass,              // enum class / strongly-typed enum
        EnumMember,             // individual enum value
        SumType,                // enum (discriminated union / Rust enum / Swift enum)
        Mixin,                  // impl block / extension / category
        TypeAlias,              // typedef / using / type / typealias
        Delegate,               // delegate / std::function / fn type

        // ── Class Members ──────────────────────────────────────────────────
        Field,                  // member variable
        Property,               // getter/setter accessor pair
        Method,                  // function / method / fn
        Constructor,             // init / ctor / new
        Destructor,              // dtor / drop / deinit
        Finalizer,               // ~Object() / non-deterministic cleanup
        OperatorOverload,        // operator+ / __add__ / func +
        Indexer,                 // this[] / operator[] / __getitem__
        Event,                   // event / pub-sub callback
        Constant,                // const / constexpr / static final
        FreeFunction,            // free-standing function (not a method)
        ExternVariable,         // extern variable declaration
        CompanionObject,         // companion object / static class

        // ── Access Levels ──────────────────────────────────────────────────
        AccessPublic,            // public / pub / +
        AccessPrivate,           // private / - / fileprivate
        AccessProtected,         // protected / internal visibility
        AccessInternal,          // internal / module-internal

        // ── Modifiers ──────────────────────────────────────────────────────
        StaticModifier,          // static
        AbstractModifier,        // abstract / pure virtual
        VirtualModifier,         // virtual
        OverrideModifier,        // override
        SealedModifier,          // sealed / final
        AsyncModifier,           // async
        ConstModifier,           // const / constexpr / let
        MutableModifier,         // mutable / mut
        VolatileModifier,        // volatile
        ReadOnlyModifier,        // readonly / let
        ExplicitModifier,        // explicit (C++ constructors)
        NoexceptModifier,        // noexcept / throws
        InlineModifier,          // inline / #[inline]

        // ── Statements ─────────────────────────────────────────────────────
        IfStatement,             // if
        ElseIfClause,            // else if / elif
        ElseClause,              // else
        SwitchStatement,          // switch / case / match
        MatchStatement,           // match (Rust/Swift pattern matching)
        ForStatement,             // for (init; cond; incr)
        ForEachStatement,         // for..in / for..of / foreach
        WhileStatement,           // while
        DoWhileStatement,         // do..while / repeat..while
        TryStatement,             // try / do
        CatchClause,              // catch / except / rescue
        FinallyClause,            // finally / ensure
        ReturnStatement,           // return
        ThrowStatement,            // throw / raise / panic
        YieldStatement,            // yield return / co_yield
        BreakStatement,            // break
        ContinueStatement,         // continue
        BlockStatement,             // { } / begin..end / do..end
        UsingStatement,             // using / with / RAII scope
        LockStatement,              // lock / synchronized / @synchronized
        CheckedStatement,            // checked / unchecked
        DeferStatement,             // defer / backtick / scope exit
        MoveStatement,              // <- move semantics
        SwapStatement,              // <-> swap semantics
        UnlessStatement,            // unless (negated if)
        RepeatStatement,            // repeat (counted loop)
        RepeatUntilStatement,       // repeat until (do-while negated)
        PostfixConditional,         // expr if cond : alt / expr unless cond : alt

        // ── Expressions ────────────────────────────────────────────────────
        Literal,                    // 42, "hello", true, null
        Identifier,                 // variable / type name
        BinaryExpression,           // a + b, a && b
        UnaryExpression,            // !a, -a, i++
        AssignmentExpression,       // a = b, a += b
        MemberAccessExpression,     // a.b, a->b, A::b
        InvocationExpression,       // func(args)
        ObjectCreationExpression,   // new T() / T{} / make()
        CastExpression,             // (T)expr / as / static_cast
        IsTypeExpression,           // is / instanceof / typeof
        ParenthesizedExpression,    // (a + b)
        TernaryExpression,          // cond ? a : b
        RangeExpression,             // start..end / start..=end
        LambdaExpression,            // => / -> / lambda
        AwaitExpression,             // await / co_await
        ArrayCreationExpression,     // [1, 2, 3] / {1, 2, 3}
        TupleExpression,             // (1, "hi") / tuple
        PipelineExpression,          // expr |> f |> g (composition)
        NullConditionalDotExpression,   // obj?.Member
        NullConditionalBracketExpression, // obj?[index]

        // ── Type Decorations ───────────────────────────────────────────────
        PointerDecoration,          // T* / *mut T / &T (C++)
        ReferenceDecoration,        // T& / &T (Rust)
        NakedDecoration,             // plain T (strip default decoration)

        // ── Special ────────────────────────────────────────────────────────
        FriendDeclaration,          // friend class / friend function
        ConstructorInitializer,     // : member(expr) / base(args)
        DestructorCall,              // ~T() / drop / deinit
        NewObject,                   // new T() / make_shared / T{}
        NullLiteral,                 // null / nullptr / nil / None
        ThisReference,               // this / self / Me
        BaseReference,               // base / super / __super
        StringInterpolation,         // $"..." / f"..." / format!
        ArrayIndexer,                // arr[i] / arr.get(i)
        InlineNativeBlock,           // cpp { } / unsafe { } / inline
        LazyField,                   // `Type name = expr (lazy init)
        NamedInitialization,         // new T { X = 1 } (fluent setters)
        MoveExpression,              // move(x) / std::move
        SwapExpression,              // swap(a, b) / std::swap
        ForwardExpression,           // forward(x) / std::forward
    }

    // ── Language enum ─────────────────────────────────────────────────────────

    /// <summary>
    /// Every language the transpiler can target or represent.
    /// Each entry corresponds to a <see cref="LanguageProfile"/> in the <see cref="LanguageMatrix"/>.
    /// Also used by <see cref="RepresentsAttribute"/> to tag
    /// stub declarations with their target language.
    /// Note: Db is the source language, not a compilation target, but its profile is retained
    /// here because the Db emitter (inbound pipeline: .h → .stub) needs it for syntax patterns.
    /// </summary>
    public enum Language
    {
        Db,          // Source language profile (used by Db emitter, not a compilation target)
        C,           // C (bare-metal systems language)
        Cpp,         // C++ (was CPlusPlus)
        CSharp,      // C#
        Rust,        // Rust
        Python,      // Python
        Swift,       // Swift
        Kotlin,      // Kotlin
        Java,        // Java
        TypeScript,  // TypeScript
        Go,          // Go
        Zig,         // Zig
    }

    // ── LanguagePart struct ───────────────────────────────────────────────────

    /// <summary>
    /// Defines how a specific <see cref="CodePart"/> is represented in one target language.
    /// Contains the keyword (if any) and an optional syntax template with placeholders.
    /// <para>Template placeholders: {0}=name, {1}=type, {2}=params, {3}=body, {4}=value/initializer.</para>
    /// </summary>
    public struct LanguagePart
    {
        public CodePart Part;
        public string   Keyword;
        public string   SyntaxTemplate;

        /// <summary>Shorthand for creating a keyword-only part (no template).</summary>
        public static LanguagePart Kw(CodePart part, string keyword)
            => new LanguagePart { Part = part, Keyword = keyword, SyntaxTemplate = null };

        /// <summary>Shorthand for creating a part with keyword and template.</summary>
        public static LanguagePart Tmpl(CodePart part, string keyword, string template)
            => new LanguagePart { Part = part, Keyword = keyword, SyntaxTemplate = template };

        public override string ToString()
            => SyntaxTemplate != null ? $"{Part}: {Keyword} → {SyntaxTemplate}" : $"{Part}: {Keyword}";
    }

    // ── LanguageProfile class ──────────────────────────────────────────────────

    /// <summary>
    /// Holds all the <see cref="LanguagePart"/> entries for one target language.
    /// This is the per-language "dictionary" that the emitter consults to know
    /// how to render each <see cref="CodePart"/>.
    /// </summary>
    public class LanguageProfile
    {
        public Language Name;
        public string       FileExtension;      // e.g. ".cpp", ".rs", ".py"
        public string       LineComment;        // e.g. "//", "#", "--"
        public string       BlockCommentStart;  // e.g. "/*", "/*!", "'''"
        public string       BlockCommentEnd;    // e.g. "*/", "'''"
        public string       StringDelimiter;    // e.g. "\"", "\"\"\"", "'"
        public string       CharDelimiter;      // e.g. "'", "\""
        public string       StatementTerminator; // e.g. ";", "", ""
        public string       NamespaceSeparator; // e.g. "::", ".", "::", "."
        public string       MemberAccessDot;    // e.g. ".", ".", ".", "."
        public string       MemberAccessArrow;  // e.g. "->", ".", ".", "."
        public string       MemberAccessScope;  // e.g. "::", ".", "::", "."
        public string       InheritanceMarker;  // e.g. ":", ":", ":", "extends"
        public string       AccessPublic;       // e.g. "public:", "public", "pub", "+"
        public string       AccessPrivate;      // e.g. "private:", "private", "-", "-"
        public string       AccessProtected;    // e.g. "protected:", "protected", "#"
        public string       NullLiteral;        // e.g. "nullptr", "null", "nil", "None"
        public string       ThisReference;      // e.g. "this", "this", "self", "self"
        public string       BaseReference;       // e.g. "Base::", "base", "super", "super"

        public Dictionary<CodePart, LanguagePart> Parts;

        /// <summary>Look up a part; returns null if not defined for this language.</summary>
        public LanguagePart? this[CodePart part]
            => Parts.TryGetValue(part, out var lp) ? lp : (LanguagePart?)null;

        /// <summary>Get the keyword for a part, or empty string if not defined.</summary>
        public string KeywordFor(CodePart part)
            => Parts.TryGetValue(part, out var lp) ? lp.Keyword : "";

        /// <summary>Get the syntax template for a part, or null if not defined.</summary>
        public string TemplateFor(CodePart part)
            => Parts.TryGetValue(part, out var lp) ? lp.SyntaxTemplate : null;
    }

    // ── The Master Matrix ──────────────────────────────────────────────────────

    /// <summary>
    /// The master language matrix: a static dictionary mapping every
    /// <see cref="Language"/> to its <see cref="LanguageProfile"/>.
    /// Each profile defines how every <see cref="CodePart"/> is represented
    /// in that language — keyword, syntax template, and language-specific
    /// conventions like statement terminators, comment styles, etc.
    /// </summary>
    public static class LanguageMatrix
    {
        public static Dictionary<Language, LanguageProfile> Languages = new()
        {
            // ═══════════════════════════════════════════════════════════════
            //  Db (the source language — C#-like DSL)
            // ═══════════════════════════════════════════════════════════════
            {
                Language.Db, new LanguageProfile
                {
                    Name                 = Language.Db,
                    FileExtension        = ".db",
                    LineComment          = "//",
                    BlockCommentStart    = "/*",
                    BlockCommentEnd      = "*/",
                    StringDelimiter      = "\"",
                    CharDelimiter        = "'",
                    StatementTerminator  = ";",
                    NamespaceSeparator  = ".",
                    MemberAccessDot      = ".",
                    MemberAccessArrow    = ".",
                    MemberAccessScope    = ".",
                    InheritanceMarker    = ":",
                    AccessPublic         = "public",
                    AccessPrivate        = "private",
                    AccessProtected      = "protected",
                    NullLiteral          = "null",
                    ThisReference        = "this",
                    BaseReference        = "base",
                    Parts = new Dictionary<CodePart, LanguagePart>
                    {
                        // Structural
                        { CodePart.CompilationUnit,     LanguagePart.Kw(CodePart.CompilationUnit, "") },
                        { CodePart.Namespace,            LanguagePart.Kw(CodePart.Namespace, "namespace") },
                        { CodePart.UsingDirective,       LanguagePart.Kw(CodePart.UsingDirective, "using") },
                        { CodePart.FileScopeDirective,   LanguagePart.Kw(CodePart.FileScopeDirective, "using") },

                        // Type declarations
                        { CodePart.Class,                LanguagePart.Tmpl(CodePart.Class, "class", "class {0}") },
                        { CodePart.Struct,               LanguagePart.Tmpl(CodePart.Struct, "struct", "struct {0}") },
                        { CodePart.Interface,            LanguagePart.Tmpl(CodePart.Interface, "interface", "interface {0}") },
                        { CodePart.Enum,                 LanguagePart.Tmpl(CodePart.Enum, "enum", "enum {0}") },
                        { CodePart.SumType,              LanguagePart.Tmpl(CodePart.SumType, "enum", "enum {0}") },  // oneof pattern
                        { CodePart.Mixin,                LanguagePart.Kw(CodePart.Mixin, "") },  // extension methods
                        { CodePart.TypeAlias,            LanguagePart.Tmpl(CodePart.TypeAlias, "using", "using {0} = {1}") },
                        { CodePart.Delegate,             LanguagePart.Tmpl(CodePart.Delegate, "delegate", "delegate {1} {0}({2})") },

                        // Members
                        { CodePart.Field,                LanguagePart.Tmpl(CodePart.Field, "", "{1} {0}") },
                        { CodePart.Property,             LanguagePart.Tmpl(CodePart.Property, "", "{1} {0} {{ get; set; }}") },
                        { CodePart.Method,               LanguagePart.Tmpl(CodePart.Method, "", "{1} {0}({2})") },
                        { CodePart.Constructor,          LanguagePart.Tmpl(CodePart.Constructor, "", "{0}({2})") },
                        { CodePart.Destructor,           LanguagePart.Tmpl(CodePart.Destructor, "", "~{0}()") },
                        { CodePart.Finalizer,            LanguagePart.Tmpl(CodePart.Finalizer, "", "~{0}()") },
                        { CodePart.OperatorOverload,     LanguagePart.Tmpl(CodePart.OperatorOverload, "operator", "operator {0}({2})") },
                        { CodePart.Indexer,              LanguagePart.Tmpl(CodePart.Indexer, "this", "{1} this[{2}]") },
                        { CodePart.Event,                LanguagePart.Tmpl(CodePart.Event, "event", "event {1} {0}") },
                        { CodePart.Constant,             LanguagePart.Tmpl(CodePart.Constant, "const", "const {1} {0} = {4}") },
                        { CodePart.CompanionObject,      LanguagePart.Kw(CodePart.CompanionObject, "") },  // static class

                        // Access levels
                        { CodePart.AccessPublic,         LanguagePart.Kw(CodePart.AccessPublic, "public") },
                        { CodePart.AccessPrivate,        LanguagePart.Kw(CodePart.AccessPrivate, "private") },
                        { CodePart.AccessProtected,      LanguagePart.Kw(CodePart.AccessProtected, "protected") },
                        { CodePart.AccessInternal,      LanguagePart.Kw(CodePart.AccessInternal, "internal") },

                        // Modifiers
                        { CodePart.StaticModifier,       LanguagePart.Kw(CodePart.StaticModifier, "static") },
                        { CodePart.AbstractModifier,     LanguagePart.Kw(CodePart.AbstractModifier, "abstract") },
                        { CodePart.VirtualModifier,      LanguagePart.Kw(CodePart.VirtualModifier, "virtual") },
                        { CodePart.OverrideModifier,    LanguagePart.Kw(CodePart.OverrideModifier, "override") },
                        { CodePart.SealedModifier,      LanguagePart.Kw(CodePart.SealedModifier, "sealed") },
                        { CodePart.AsyncModifier,       LanguagePart.Kw(CodePart.AsyncModifier, "async") },
                        { CodePart.ConstModifier,        LanguagePart.Kw(CodePart.ConstModifier, "const") },
                        { CodePart.MutableModifier,      LanguagePart.Kw(CodePart.MutableModifier, "mutable") },
                        { CodePart.VolatileModifier,    LanguagePart.Kw(CodePart.VolatileModifier, "volatile") },
                        { CodePart.ReadOnlyModifier,    LanguagePart.Kw(CodePart.ReadOnlyModifier, "readonly") },
                        { CodePart.ExplicitModifier,    LanguagePart.Kw(CodePart.ExplicitModifier, "explicit") },
                        { CodePart.NoexceptModifier,    LanguagePart.Kw(CodePart.NoexceptModifier, "noexcept") },
                        { CodePart.InlineModifier,      LanguagePart.Kw(CodePart.InlineModifier, "inline") },

                        // Statements
                        { CodePart.IfStatement,          LanguagePart.Tmpl(CodePart.IfStatement, "if", "if ({0})") },
                        { CodePart.ElseIfClause,         LanguagePart.Tmpl(CodePart.ElseIfClause, "else if", "else if ({0})") },
                        { CodePart.ElseClause,           LanguagePart.Kw(CodePart.ElseClause, "else") },
                        { CodePart.SwitchStatement,      LanguagePart.Tmpl(CodePart.SwitchStatement, "switch", "switch ({0})") },
                        { CodePart.MatchStatement,       LanguagePart.Tmpl(CodePart.MatchStatement, "switch", "switch ({0})") },
                        { CodePart.ForStatement,          LanguagePart.Tmpl(CodePart.ForStatement, "for", "for ({0})") },
                        { CodePart.ForEachStatement,     LanguagePart.Tmpl(CodePart.ForEachStatement, "foreach", "foreach ({0})") },
                        { CodePart.WhileStatement,       LanguagePart.Tmpl(CodePart.WhileStatement, "while", "while ({0})") },
                        { CodePart.DoWhileStatement,     LanguagePart.Tmpl(CodePart.DoWhileStatement, "do", "do ... while ({0})") },
                        { CodePart.TryStatement,         LanguagePart.Kw(CodePart.TryStatement, "try") },
                        { CodePart.CatchClause,          LanguagePart.Tmpl(CodePart.CatchClause, "catch", "catch ({0})") },
                        { CodePart.FinallyClause,        LanguagePart.Kw(CodePart.FinallyClause, "finally") },
                        { CodePart.ReturnStatement,      LanguagePart.Kw(CodePart.ReturnStatement, "return") },
                        { CodePart.ThrowStatement,       LanguagePart.Kw(CodePart.ThrowStatement, "throw") },
                        { CodePart.YieldStatement,       LanguagePart.Kw(CodePart.YieldStatement, "yield return") },
                        { CodePart.BreakStatement,       LanguagePart.Kw(CodePart.BreakStatement, "break") },
                        { CodePart.ContinueStatement,    LanguagePart.Kw(CodePart.ContinueStatement, "continue") },
                        { CodePart.BlockStatement,       LanguagePart.Kw(CodePart.BlockStatement, "") },
                        { CodePart.UsingStatement,       LanguagePart.Tmpl(CodePart.UsingStatement, "using", "using ({0})") },
                        { CodePart.LockStatement,        LanguagePart.Tmpl(CodePart.LockStatement, "lock", "lock ({0})") },
                        { CodePart.CheckedStatement,    LanguagePart.Kw(CodePart.CheckedStatement, "checked") },

                        // Expressions
                        { CodePart.Literal,              LanguagePart.Kw(CodePart.Literal, "") },
                        { CodePart.Identifier,           LanguagePart.Kw(CodePart.Identifier, "") },
                        { CodePart.BinaryExpression,     LanguagePart.Kw(CodePart.BinaryExpression, "") },
                        { CodePart.UnaryExpression,      LanguagePart.Kw(CodePart.UnaryExpression, "") },
                        { CodePart.AssignmentExpression, LanguagePart.Kw(CodePart.AssignmentExpression, "") },
                        { CodePart.MemberAccessExpression, LanguagePart.Kw(CodePart.MemberAccessExpression, ".") },
                        { CodePart.InvocationExpression, LanguagePart.Kw(CodePart.InvocationExpression, "") },
                        { CodePart.ObjectCreationExpression, LanguagePart.Tmpl(CodePart.ObjectCreationExpression, "new", "new {0}({2})") },
                        { CodePart.CastExpression,       LanguagePart.Tmpl(CodePart.CastExpression, "", "({0}){1}") },
                        { CodePart.IsTypeExpression,     LanguagePart.Tmpl(CodePart.IsTypeExpression, "is", "{1} is {0}") },
                        { CodePart.ParenthesizedExpression, LanguagePart.Kw(CodePart.ParenthesizedExpression, "") },
                        { CodePart.TernaryExpression,    LanguagePart.Tmpl(CodePart.TernaryExpression, "", "{0} ? {1} : {2}") },
                        { CodePart.RangeExpression,      LanguagePart.Tmpl(CodePart.RangeExpression, "..", "{0}..{1}") },
                        { CodePart.LambdaExpression,     LanguagePart.Tmpl(CodePart.LambdaExpression, "=>", "({0}) => {1}") },
                        { CodePart.AwaitExpression,      LanguagePart.Kw(CodePart.AwaitExpression, "await") },
                        { CodePart.ArrayCreationExpression, LanguagePart.Tmpl(CodePart.ArrayCreationExpression, "new", "new {0}[]") },
                        { CodePart.TupleExpression,      LanguagePart.Tmpl(CodePart.TupleExpression, "", "({0})") },

                        // Type decorations
                        { CodePart.PointerDecoration,    LanguagePart.Kw(CodePart.PointerDecoration, "") },  // no pointers in Db
                        { CodePart.ReferenceDecoration,  LanguagePart.Tmpl(CodePart.ReferenceDecoration, "ref", "ref {0}") },
                        { CodePart.NakedDecoration,      LanguagePart.Kw(CodePart.NakedDecoration, "") },

                        // Special — Db-specific attributes
                        { CodePart.FriendDeclaration,    LanguagePart.Tmpl(CodePart.FriendDeclaration, "[Friend]", "[Friend({0})]") },
                        { CodePart.ConstructorInitializer, LanguagePart.Tmpl(CodePart.ConstructorInitializer, ":", ": {0}({1})") },
                        { CodePart.DestructorCall,       LanguagePart.Tmpl(CodePart.DestructorCall, "~", "~{0}()") },
                        { CodePart.NewObject,            LanguagePart.Tmpl(CodePart.NewObject, "new", "new {0}({1})") },
                        { CodePart.NullLiteral,          LanguagePart.Kw(CodePart.NullLiteral, "null") },
                        { CodePart.ThisReference,        LanguagePart.Kw(CodePart.ThisReference, "this") },
                        { CodePart.BaseReference,        LanguagePart.Kw(CodePart.BaseReference, "base") },
                        { CodePart.StringInterpolation, LanguagePart.Tmpl(CodePart.StringInterpolation, "$", "$\"{0}\"") },
                        { CodePart.ArrayIndexer,         LanguagePart.Tmpl(CodePart.ArrayIndexer, "[]", "{0}[{1}]") },
                    }
                }
            },

            // ═══════════════════════════════════════════════════════════════
            //  C++
            // ═══════════════════════════════════════════════════════════════
            {
                Language.Cpp, new LanguageProfile
                {
                    Name                 = Language.Cpp,
                    FileExtension        = ".cpp",
                    LineComment          = "//",
                    BlockCommentStart    = "/*",
                    BlockCommentEnd      = "*/",
                    StringDelimiter      = "\"",
                    CharDelimiter        = "'",
                    StatementTerminator  = ";",
                    NamespaceSeparator  = "::",
                    MemberAccessDot      = ".",
                    MemberAccessArrow    = "->",
                    MemberAccessScope    = "::",
                    InheritanceMarker    = ":",
                    AccessPublic         = "public:",
                    AccessPrivate        = "private:",
                    AccessProtected      = "protected:",
                    NullLiteral          = "nullptr",
                    ThisReference        = "this",
                    BaseReference        = "Base::",
                    Parts = new Dictionary<CodePart, LanguagePart>
                    {
                        // Structural
                        { CodePart.CompilationUnit,     LanguagePart.Kw(CodePart.CompilationUnit, "") },
                        { CodePart.Namespace,            LanguagePart.Kw(CodePart.Namespace, "namespace") },
                        { CodePart.UsingDirective,       LanguagePart.Kw(CodePart.UsingDirective, "#include") },
                        { CodePart.FileScopeDirective,   LanguagePart.Kw(CodePart.FileScopeDirective, "#include") },

                        // Type declarations
                        { CodePart.Class,                LanguagePart.Tmpl(CodePart.Class, "class", "class {0}") },
                        { CodePart.Struct,               LanguagePart.Tmpl(CodePart.Struct, "struct", "struct {0}") },
                        { CodePart.Interface,            LanguagePart.Tmpl(CodePart.Interface, "class", "class {0}") },
                        { CodePart.Enum,                 LanguagePart.Tmpl(CodePart.Enum, "enum", "enum {0}") },
                        { CodePart.SumType,              LanguagePart.Tmpl(CodePart.SumType, "std::variant", "using {0} = std::variant<...>") },
                        { CodePart.Mixin,                LanguagePart.Kw(CodePart.Mixin, "") },  // impl blocks not native
                        { CodePart.TypeAlias,            LanguagePart.Tmpl(CodePart.TypeAlias, "using", "using {0} = {1}") },
                        { CodePart.Delegate,             LanguagePart.Tmpl(CodePart.Delegate, "std::function", "using {0} = std::function<{1}({2})>") },

                        // Members
                        { CodePart.Field,                LanguagePart.Tmpl(CodePart.Field, "", "{1} {0}") },
                        { CodePart.Property,             LanguagePart.Tmpl(CodePart.Property, "", "{1} {0}() const") },
                        { CodePart.Method,               LanguagePart.Tmpl(CodePart.Method, "", "{1} {0}({2})") },
                        { CodePart.Constructor,          LanguagePart.Tmpl(CodePart.Constructor, "", "{0}({2})") },
                        { CodePart.Destructor,           LanguagePart.Tmpl(CodePart.Destructor, "", "~{0}()") },
                        { CodePart.Finalizer,            LanguagePart.Kw(CodePart.Finalizer, "") },  // no direct equivalent
                        { CodePart.OperatorOverload,     LanguagePart.Tmpl(CodePart.OperatorOverload, "operator", "operator{0}({2})") },
                        { CodePart.Indexer,              LanguagePart.Tmpl(CodePart.Indexer, "operator[]", "{1}& operator[]({2})") },
                        { CodePart.Event,                LanguagePart.Kw(CodePart.Event, "") },  // no direct equivalent
                        { CodePart.Constant,             LanguagePart.Tmpl(CodePart.Constant, "constexpr", "static constexpr {1} {0} = {4}") },
                        { CodePart.CompanionObject,      LanguagePart.Kw(CodePart.CompanionObject, "struct") },

                        // Access levels
                        { CodePart.AccessPublic,         LanguagePart.Kw(CodePart.AccessPublic, "public:") },
                        { CodePart.AccessPrivate,        LanguagePart.Kw(CodePart.AccessPrivate, "private:") },
                        { CodePart.AccessProtected,      LanguagePart.Kw(CodePart.AccessProtected, "protected:") },
                        { CodePart.AccessInternal,      LanguagePart.Kw(CodePart.AccessInternal, "public:") },

                        // Modifiers
                        { CodePart.StaticModifier,       LanguagePart.Kw(CodePart.StaticModifier, "static") },
                        { CodePart.AbstractModifier,     LanguagePart.Kw(CodePart.AbstractModifier, "virtual") },  // + = 0
                        { CodePart.VirtualModifier,      LanguagePart.Kw(CodePart.VirtualModifier, "virtual") },
                        { CodePart.OverrideModifier,    LanguagePart.Kw(CodePart.OverrideModifier, "override") },
                        { CodePart.SealedModifier,      LanguagePart.Kw(CodePart.SealedModifier, "final") },
                        { CodePart.AsyncModifier,       LanguagePart.Kw(CodePart.AsyncModifier, "") },  // no native async
                        { CodePart.ConstModifier,        LanguagePart.Kw(CodePart.ConstModifier, "const") },
                        { CodePart.MutableModifier,      LanguagePart.Kw(CodePart.MutableModifier, "mutable") },
                        { CodePart.VolatileModifier,    LanguagePart.Kw(CodePart.VolatileModifier, "volatile") },
                        { CodePart.ReadOnlyModifier,    LanguagePart.Kw(CodePart.ReadOnlyModifier, "const") },
                        { CodePart.ExplicitModifier,    LanguagePart.Kw(CodePart.ExplicitModifier, "explicit") },
                        { CodePart.NoexceptModifier,    LanguagePart.Kw(CodePart.NoexceptModifier, "noexcept") },
                        { CodePart.InlineModifier,      LanguagePart.Kw(CodePart.InlineModifier, "inline") },

                        // Statements
                        { CodePart.IfStatement,          LanguagePart.Tmpl(CodePart.IfStatement, "if", "if ({0})") },
                        { CodePart.ElseIfClause,         LanguagePart.Tmpl(CodePart.ElseIfClause, "else if", "else if ({0})") },
                        { CodePart.ElseClause,           LanguagePart.Kw(CodePart.ElseClause, "else") },
                        { CodePart.SwitchStatement,      LanguagePart.Tmpl(CodePart.SwitchStatement, "switch", "switch ({0})") },
                        { CodePart.MatchStatement,       LanguagePart.Kw(CodePart.MatchStatement, "switch") },  // C++ uses switch
                        { CodePart.ForStatement,          LanguagePart.Tmpl(CodePart.ForStatement, "for", "for ({0})") },
                        { CodePart.ForEachStatement,     LanguagePart.Tmpl(CodePart.ForEachStatement, "for", "for (auto& {0} : {1})") },
                        { CodePart.WhileStatement,       LanguagePart.Tmpl(CodePart.WhileStatement, "while", "while ({0})") },
                        { CodePart.DoWhileStatement,     LanguagePart.Tmpl(CodePart.DoWhileStatement, "do", "do ... while ({0})") },
                        { CodePart.TryStatement,         LanguagePart.Kw(CodePart.TryStatement, "try") },
                        { CodePart.CatchClause,          LanguagePart.Tmpl(CodePart.CatchClause, "catch", "catch ({0})") },
                        { CodePart.FinallyClause,        LanguagePart.Kw(CodePart.FinallyClause, "") },  // no direct equivalent
                        { CodePart.ReturnStatement,      LanguagePart.Kw(CodePart.ReturnStatement, "return") },
                        { CodePart.ThrowStatement,       LanguagePart.Kw(CodePart.ThrowStatement, "throw") },
                        { CodePart.YieldStatement,       LanguagePart.Kw(CodePart.YieldStatement, "co_yield") },
                        { CodePart.BreakStatement,       LanguagePart.Kw(CodePart.BreakStatement, "break") },
                        { CodePart.ContinueStatement,    LanguagePart.Kw(CodePart.ContinueStatement, "continue") },
                        { CodePart.BlockStatement,       LanguagePart.Kw(CodePart.BlockStatement, "") },
                        { CodePart.UsingStatement,       LanguagePart.Kw(CodePart.UsingStatement, "") },  // RAII handles this
                        { CodePart.LockStatement,        LanguagePart.Tmpl(CodePart.LockStatement, "lock_guard", "std::lock_guard<std::mutex> {0}") },
                        { CodePart.CheckedStatement,    LanguagePart.Kw(CodePart.CheckedStatement, "") },  // no equivalent

                        // Expressions
                        { CodePart.Literal,              LanguagePart.Kw(CodePart.Literal, "") },
                        { CodePart.Identifier,           LanguagePart.Kw(CodePart.Identifier, "") },
                        { CodePart.BinaryExpression,     LanguagePart.Kw(CodePart.BinaryExpression, "") },
                        { CodePart.UnaryExpression,      LanguagePart.Kw(CodePart.UnaryExpression, "") },
                        { CodePart.AssignmentExpression, LanguagePart.Kw(CodePart.AssignmentExpression, "") },
                        { CodePart.MemberAccessExpression, LanguagePart.Kw(CodePart.MemberAccessExpression, "") },
                        { CodePart.InvocationExpression, LanguagePart.Kw(CodePart.InvocationExpression, "") },
                        { CodePart.ObjectCreationExpression, LanguagePart.Tmpl(CodePart.ObjectCreationExpression, "new", "std::make_shared<{0}>({2})") },
                        { CodePart.CastExpression,       LanguagePart.Tmpl(CodePart.CastExpression, "static_cast", "static_cast<{0}>({1})") },
                        { CodePart.IsTypeExpression,     LanguagePart.Tmpl(CodePart.IsTypeExpression, "dynamic_cast", "dynamic_cast<{0}*>(&{1}) != nullptr") },
                        { CodePart.ParenthesizedExpression, LanguagePart.Kw(CodePart.ParenthesizedExpression, "") },
                        { CodePart.TernaryExpression,    LanguagePart.Tmpl(CodePart.TernaryExpression, "", "{0} ? {1} : {2}") },
                        { CodePart.RangeExpression,      LanguagePart.Kw(CodePart.RangeExpression, "") },  // no native range
                        { CodePart.LambdaExpression,     LanguagePart.Tmpl(CodePart.LambdaExpression, "", "[{0}]({1}) -> {2} {{ {3} }}") },
                        { CodePart.AwaitExpression,      LanguagePart.Kw(CodePart.AwaitExpression, "co_await") },
                        { CodePart.ArrayCreationExpression, LanguagePart.Tmpl(CodePart.ArrayCreationExpression, "", "std::array<{0}, {1}>") },
                        { CodePart.TupleExpression,      LanguagePart.Tmpl(CodePart.TupleExpression, "std::tuple", "std::tuple<{0}>") },

                        // Type decorations
                        { CodePart.PointerDecoration,    LanguagePart.Tmpl(CodePart.PointerDecoration, "*", "{0}*") },
                        { CodePart.ReferenceDecoration,  LanguagePart.Tmpl(CodePart.ReferenceDecoration, "&", "{0}&") },
                        { CodePart.NakedDecoration,      LanguagePart.Kw(CodePart.NakedDecoration, "") },

                        // Special
                        { CodePart.FriendDeclaration,    LanguagePart.Tmpl(CodePart.FriendDeclaration, "friend", "friend {0}") },
                        { CodePart.ConstructorInitializer, LanguagePart.Tmpl(CodePart.ConstructorInitializer, ":", ": {0}({1})") },
                        { CodePart.DestructorCall,       LanguagePart.Tmpl(CodePart.DestructorCall, "~", "~{0}()") },
                        { CodePart.NewObject,            LanguagePart.Tmpl(CodePart.NewObject, "new", "new {0}({1})") },
                        { CodePart.NullLiteral,          LanguagePart.Kw(CodePart.NullLiteral, "nullptr") },
                        { CodePart.ThisReference,        LanguagePart.Kw(CodePart.ThisReference, "this") },
                        { CodePart.BaseReference,        LanguagePart.Kw(CodePart.BaseReference, "Base::") },
                        { CodePart.StringInterpolation, LanguagePart.Kw(CodePart.StringInterpolation, "") },  // no native
                        { CodePart.ArrayIndexer,         LanguagePart.Tmpl(CodePart.ArrayIndexer, "[]", "{0}[{1}]") },
                    }
                }
            },

            // ═══════════════════════════════════════════════════════════════
            //  C#
            // ═══════════════════════════════════════════════════════════════
            {
                Language.CSharp, new LanguageProfile
                {
                    Name                 = Language.CSharp,
                    FileExtension        = ".cs",
                    LineComment          = "//",
                    BlockCommentStart    = "/*",
                    BlockCommentEnd      = "*/",
                    StringDelimiter      = "\"",
                    CharDelimiter        = "'",
                    StatementTerminator  = ";",
                    NamespaceSeparator  = ".",
                    MemberAccessDot      = ".",
                    MemberAccessArrow    = ".",
                    MemberAccessScope    = ".",
                    InheritanceMarker    = ":",
                    AccessPublic         = "public",
                    AccessPrivate        = "private",
                    AccessProtected      = "protected",
                    NullLiteral          = "null",
                    ThisReference        = "this",
                    BaseReference        = "base",
                    Parts = new Dictionary<CodePart, LanguagePart>
                    {
                        // Structural
                        { CodePart.CompilationUnit,     LanguagePart.Kw(CodePart.CompilationUnit, "") },
                        { CodePart.Namespace,            LanguagePart.Kw(CodePart.Namespace, "namespace") },
                        { CodePart.UsingDirective,       LanguagePart.Kw(CodePart.UsingDirective, "using") },
                        { CodePart.FileScopeDirective,   LanguagePart.Kw(CodePart.FileScopeDirective, "using") },

                        // Type declarations
                        { CodePart.Class,                LanguagePart.Tmpl(CodePart.Class, "class", "class {0}") },
                        { CodePart.Struct,               LanguagePart.Tmpl(CodePart.Struct, "struct", "struct {0}") },
                        { CodePart.Interface,            LanguagePart.Tmpl(CodePart.Interface, "interface", "interface {0}") },
                        { CodePart.Enum,                 LanguagePart.Tmpl(CodePart.Enum, "enum", "enum {0}") },
                        { CodePart.SumType,              LanguagePart.Tmpl(CodePart.SumType, "enum", "enum {0}") },  // oneof pattern
                        { CodePart.Mixin,                LanguagePart.Kw(CodePart.Mixin, "") },  // extension methods
                        { CodePart.TypeAlias,            LanguagePart.Tmpl(CodePart.TypeAlias, "using", "using {0} = {1}") },
                        { CodePart.Delegate,             LanguagePart.Tmpl(CodePart.Delegate, "delegate", "delegate {1} {0}({2})") },

                        // Members
                        { CodePart.Field,                LanguagePart.Tmpl(CodePart.Field, "", "{1} {0}") },
                        { CodePart.Property,             LanguagePart.Tmpl(CodePart.Property, "", "{1} {0} {{ get; set; }}") },
                        { CodePart.Method,               LanguagePart.Tmpl(CodePart.Method, "", "{1} {0}({2})") },
                        { CodePart.Constructor,          LanguagePart.Tmpl(CodePart.Constructor, "", "{0}({2})") },
                        { CodePart.Destructor,           LanguagePart.Tmpl(CodePart.Destructor, "", "~{0}()") },
                        { CodePart.Finalizer,            LanguagePart.Tmpl(CodePart.Finalizer, "", "~{0}()") },
                        { CodePart.OperatorOverload,     LanguagePart.Tmpl(CodePart.OperatorOverload, "operator", "operator {0}({2})") },
                        { CodePart.Indexer,              LanguagePart.Tmpl(CodePart.Indexer, "this", "{1} this[{2}]") },
                        { CodePart.Event,                LanguagePart.Tmpl(CodePart.Event, "event", "event {1} {0}") },
                        { CodePart.Constant,             LanguagePart.Tmpl(CodePart.Constant, "const", "const {1} {0} = {4}") },
                        { CodePart.CompanionObject,      LanguagePart.Kw(CodePart.CompanionObject, "") },  // static class

                        // Access levels
                        { CodePart.AccessPublic,         LanguagePart.Kw(CodePart.AccessPublic, "public") },
                        { CodePart.AccessPrivate,        LanguagePart.Kw(CodePart.AccessPrivate, "private") },
                        { CodePart.AccessProtected,      LanguagePart.Kw(CodePart.AccessProtected, "protected") },
                        { CodePart.AccessInternal,      LanguagePart.Kw(CodePart.AccessInternal, "internal") },

                        // Modifiers
                        { CodePart.StaticModifier,       LanguagePart.Kw(CodePart.StaticModifier, "static") },
                        { CodePart.AbstractModifier,     LanguagePart.Kw(CodePart.AbstractModifier, "abstract") },
                        { CodePart.VirtualModifier,      LanguagePart.Kw(CodePart.VirtualModifier, "virtual") },
                        { CodePart.OverrideModifier,    LanguagePart.Kw(CodePart.OverrideModifier, "override") },
                        { CodePart.SealedModifier,      LanguagePart.Kw(CodePart.SealedModifier, "sealed") },
                        { CodePart.AsyncModifier,       LanguagePart.Kw(CodePart.AsyncModifier, "async") },
                        { CodePart.ConstModifier,        LanguagePart.Kw(CodePart.ConstModifier, "const") },
                        { CodePart.MutableModifier,      LanguagePart.Kw(CodePart.MutableModifier, "") },  // no equivalent
                        { CodePart.VolatileModifier,    LanguagePart.Kw(CodePart.VolatileModifier, "volatile") },
                        { CodePart.ReadOnlyModifier,    LanguagePart.Kw(CodePart.ReadOnlyModifier, "readonly") },
                        { CodePart.ExplicitModifier,    LanguagePart.Kw(CodePart.ExplicitModifier, "") },  // no equivalent
                        { CodePart.NoexceptModifier,    LanguagePart.Kw(CodePart.NoexceptModifier, "") },  // no equivalent
                        { CodePart.InlineModifier,      LanguagePart.Kw(CodePart.InlineModifier, "") },  // no equivalent (JIT inlines)

                        // Statements
                        { CodePart.IfStatement,          LanguagePart.Tmpl(CodePart.IfStatement, "if", "if ({0})") },
                        { CodePart.ElseIfClause,         LanguagePart.Tmpl(CodePart.ElseIfClause, "else if", "else if ({0})") },
                        { CodePart.ElseClause,           LanguagePart.Kw(CodePart.ElseClause, "else") },
                        { CodePart.SwitchStatement,      LanguagePart.Tmpl(CodePart.SwitchStatement, "switch", "switch ({0})") },
                        { CodePart.MatchStatement,       LanguagePart.Tmpl(CodePart.MatchStatement, "switch", "switch ({0})") },
                        { CodePart.ForStatement,          LanguagePart.Tmpl(CodePart.ForStatement, "for", "for ({0})") },
                        { CodePart.ForEachStatement,     LanguagePart.Tmpl(CodePart.ForEachStatement, "foreach", "foreach ({0})") },
                        { CodePart.WhileStatement,       LanguagePart.Tmpl(CodePart.WhileStatement, "while", "while ({0})") },
                        { CodePart.DoWhileStatement,     LanguagePart.Tmpl(CodePart.DoWhileStatement, "do", "do ... while ({0})") },
                        { CodePart.TryStatement,         LanguagePart.Kw(CodePart.TryStatement, "try") },
                        { CodePart.CatchClause,          LanguagePart.Tmpl(CodePart.CatchClause, "catch", "catch ({0})") },
                        { CodePart.FinallyClause,        LanguagePart.Kw(CodePart.FinallyClause, "finally") },
                        { CodePart.ReturnStatement,      LanguagePart.Kw(CodePart.ReturnStatement, "return") },
                        { CodePart.ThrowStatement,       LanguagePart.Kw(CodePart.ThrowStatement, "throw") },
                        { CodePart.YieldStatement,       LanguagePart.Kw(CodePart.YieldStatement, "yield return") },
                        { CodePart.BreakStatement,       LanguagePart.Kw(CodePart.BreakStatement, "break") },
                        { CodePart.ContinueStatement,    LanguagePart.Kw(CodePart.ContinueStatement, "continue") },
                        { CodePart.BlockStatement,       LanguagePart.Kw(CodePart.BlockStatement, "") },
                        { CodePart.UsingStatement,       LanguagePart.Tmpl(CodePart.UsingStatement, "using", "using ({0})") },
                        { CodePart.LockStatement,        LanguagePart.Tmpl(CodePart.LockStatement, "lock", "lock ({0})") },
                        { CodePart.CheckedStatement,    LanguagePart.Kw(CodePart.CheckedStatement, "checked") },

                        // Expressions
                        { CodePart.Literal,              LanguagePart.Kw(CodePart.Literal, "") },
                        { CodePart.Identifier,           LanguagePart.Kw(CodePart.Identifier, "") },
                        { CodePart.BinaryExpression,     LanguagePart.Kw(CodePart.BinaryExpression, "") },
                        { CodePart.UnaryExpression,      LanguagePart.Kw(CodePart.UnaryExpression, "") },
                        { CodePart.AssignmentExpression, LanguagePart.Kw(CodePart.AssignmentExpression, "") },
                        { CodePart.MemberAccessExpression, LanguagePart.Kw(CodePart.MemberAccessExpression, ".") },
                        { CodePart.InvocationExpression, LanguagePart.Kw(CodePart.InvocationExpression, "") },
                        { CodePart.ObjectCreationExpression, LanguagePart.Tmpl(CodePart.ObjectCreationExpression, "new", "new {0}({2})") },
                        { CodePart.CastExpression,       LanguagePart.Tmpl(CodePart.CastExpression, "", "({0}){1}") },
                        { CodePart.IsTypeExpression,     LanguagePart.Tmpl(CodePart.IsTypeExpression, "is", "{1} is {0}") },
                        { CodePart.ParenthesizedExpression, LanguagePart.Kw(CodePart.ParenthesizedExpression, "") },
                        { CodePart.TernaryExpression,    LanguagePart.Tmpl(CodePart.TernaryExpression, "", "{0} ? {1} : {2}") },
                        { CodePart.RangeExpression,      LanguagePart.Tmpl(CodePart.RangeExpression, "..", "{0}..{1}") },
                        { CodePart.LambdaExpression,     LanguagePart.Tmpl(CodePart.LambdaExpression, "=>", "({0}) => {1}") },
                        { CodePart.AwaitExpression,      LanguagePart.Kw(CodePart.AwaitExpression, "await") },
                        { CodePart.ArrayCreationExpression, LanguagePart.Tmpl(CodePart.ArrayCreationExpression, "new", "new {0}[]") },
                        { CodePart.TupleExpression,      LanguagePart.Tmpl(CodePart.TupleExpression, "", "({0})") },

                        // Type decorations
                        { CodePart.PointerDecoration,    LanguagePart.Kw(CodePart.PointerDecoration, "") },  // unsafe only
                        { CodePart.ReferenceDecoration,  LanguagePart.Kw(CodePart.ReferenceDecoration, "ref") },
                        { CodePart.NakedDecoration,      LanguagePart.Kw(CodePart.NakedDecoration, "") },

                        // Special
                        { CodePart.FriendDeclaration,    LanguagePart.Kw(CodePart.FriendDeclaration, "") },  // no equivalent
                        { CodePart.ConstructorInitializer, LanguagePart.Tmpl(CodePart.ConstructorInitializer, ":", ": {0}({1})") },
                        { CodePart.DestructorCall,       LanguagePart.Tmpl(CodePart.DestructorCall, "~", "~{0}()") },
                        { CodePart.NewObject,            LanguagePart.Tmpl(CodePart.NewObject, "new", "new {0}({1})") },
                        { CodePart.NullLiteral,          LanguagePart.Kw(CodePart.NullLiteral, "null") },
                        { CodePart.ThisReference,        LanguagePart.Kw(CodePart.ThisReference, "this") },
                        { CodePart.BaseReference,        LanguagePart.Kw(CodePart.BaseReference, "base") },
                        { CodePart.StringInterpolation, LanguagePart.Tmpl(CodePart.StringInterpolation, "$", "$\"{0}\"") },
                        { CodePart.ArrayIndexer,         LanguagePart.Tmpl(CodePart.ArrayIndexer, "[]", "{0}[{1}]") },
                    }
                }
            },

            // ═══════════════════════════════════════════════════════════════
            //  Rust
            // ═══════════════════════════════════════════════════════════════
            {
                Language.Rust, new LanguageProfile
                {
                    Name                 = Language.Rust,
                    FileExtension        = ".rs",
                    LineComment          = "//",
                    BlockCommentStart    = "/*",
                    BlockCommentEnd      = "*/",
                    StringDelimiter      = "\"",
                    CharDelimiter        = "'",
                    StatementTerminator  = "",
                    NamespaceSeparator  = "::",
                    MemberAccessDot      = ".",
                    MemberAccessArrow    = ".",
                    MemberAccessScope    = "::",
                    InheritanceMarker    = ":",
                    AccessPublic         = "pub",
                    AccessPrivate        = "",
                    AccessProtected      = "pub(crate)",
                    NullLiteral          = "None",
                    ThisReference        = "self",
                    BaseReference        = "Parent::",
                    Parts = new Dictionary<CodePart, LanguagePart>
                    {
                        // Structural
                        { CodePart.CompilationUnit,     LanguagePart.Kw(CodePart.CompilationUnit, "") },
                        { CodePart.Namespace,            LanguagePart.Kw(CodePart.Namespace, "mod") },
                        { CodePart.UsingDirective,       LanguagePart.Kw(CodePart.UsingDirective, "use") },
                        { CodePart.FileScopeDirective,   LanguagePart.Kw(CodePart.FileScopeDirective, "use") },

                        // Type declarations
                        { CodePart.Class,                LanguagePart.Tmpl(CodePart.Class, "struct", "struct {0}") },
                        { CodePart.Struct,               LanguagePart.Tmpl(CodePart.Struct, "struct", "struct {0}") },
                        { CodePart.Interface,            LanguagePart.Tmpl(CodePart.Interface, "trait", "trait {0}") },
                        { CodePart.Enum,                 LanguagePart.Tmpl(CodePart.Enum, "enum", "enum {0}") },
                        { CodePart.SumType,              LanguagePart.Tmpl(CodePart.SumType, "enum", "enum {0}") },
                        { CodePart.Mixin,                LanguagePart.Tmpl(CodePart.Mixin, "impl", "impl {0}") },
                        { CodePart.TypeAlias,            LanguagePart.Tmpl(CodePart.TypeAlias, "type", "type {0} = {1}") },
                        { CodePart.Delegate,             LanguagePart.Tmpl(CodePart.Delegate, "fn", "type {0} = fn({2}) -> {1}") },

                        // Members
                        { CodePart.Field,                LanguagePart.Tmpl(CodePart.Field, "", "{0}: {1}") },
                        { CodePart.Property,             LanguagePart.Kw(CodePart.Property, "") },  // methods instead
                        { CodePart.Method,               LanguagePart.Tmpl(CodePart.Method, "fn", "fn {0}({2}) -> {1}") },
                        { CodePart.Constructor,          LanguagePart.Tmpl(CodePart.Constructor, "fn new", "fn new({2}) -> Self") },
                        { CodePart.Destructor,           LanguagePart.Tmpl(CodePart.Destructor, "drop", "fn drop(&mut self)") },
                        { CodePart.Finalizer,            LanguagePart.Kw(CodePart.Finalizer, "") },  // no equivalent
                        { CodePart.OperatorOverload,     LanguagePart.Tmpl(CodePart.OperatorOverload, "impl", "fn {0}({2})") },
                        { CodePart.Indexer,              LanguagePart.Tmpl(CodePart.Indexer, "Index", "fn index(&self, {2}) -> &{1}") },
                        { CodePart.Event,                LanguagePart.Kw(CodePart.Event, "") },  // no direct equivalent
                        { CodePart.Constant,             LanguagePart.Tmpl(CodePart.Constant, "const", "const {0}: {1} = {4}") },
                        { CodePart.CompanionObject,      LanguagePart.Kw(CodePart.CompanionObject, "") },  // inherent impl

                        // Access levels
                        { CodePart.AccessPublic,         LanguagePart.Kw(CodePart.AccessPublic, "pub") },
                        { CodePart.AccessPrivate,        LanguagePart.Kw(CodePart.AccessPrivate, "") },  // default
                        { CodePart.AccessProtected,      LanguagePart.Kw(CodePart.AccessProtected, "pub(crate)") },
                        { CodePart.AccessInternal,      LanguagePart.Kw(CodePart.AccessInternal, "pub(crate)") },

                        // Modifiers
                        { CodePart.StaticModifier,       LanguagePart.Kw(CodePart.StaticModifier, "") },  // no static, use inherent impl
                        { CodePart.AbstractModifier,     LanguagePart.Kw(CodePart.AbstractModifier, "") },  // trait default
                        { CodePart.VirtualModifier,      LanguagePart.Kw(CodePart.VirtualModifier, "") },  // no virtual, trait dispatch
                        { CodePart.OverrideModifier,    LanguagePart.Kw(CodePart.OverrideModifier, "") },  // impl required
                        { CodePart.SealedModifier,      LanguagePart.Kw(CodePart.SealedModifier, "") },  // no sealed
                        { CodePart.AsyncModifier,       LanguagePart.Kw(CodePart.AsyncModifier, "async") },
                        { CodePart.ConstModifier,        LanguagePart.Kw(CodePart.ConstModifier, "const") },
                        { CodePart.MutableModifier,      LanguagePart.Kw(CodePart.MutableModifier, "mut") },
                        { CodePart.VolatileModifier,    LanguagePart.Kw(CodePart.VolatileModifier, "") },  // no volatile
                        { CodePart.ReadOnlyModifier,    LanguagePart.Kw(CodePart.ReadOnlyModifier, "") },  // no readonly, let binding
                        { CodePart.ExplicitModifier,    LanguagePart.Kw(CodePart.ExplicitModifier, "") },  // no equivalent
                        { CodePart.NoexceptModifier,    LanguagePart.Kw(CodePart.NoexceptModifier, "") },  // no equivalent
                        { CodePart.InlineModifier,      LanguagePart.Tmpl(CodePart.InlineModifier, "#[inline]", "#[inline]") },

                        // Statements
                        { CodePart.IfStatement,          LanguagePart.Tmpl(CodePart.IfStatement, "if", "if {0}") },
                        { CodePart.ElseIfClause,         LanguagePart.Tmpl(CodePart.ElseIfClause, "else if", "else if {0}") },
                        { CodePart.ElseClause,           LanguagePart.Kw(CodePart.ElseClause, "else") },
                        { CodePart.SwitchStatement,      LanguagePart.Tmpl(CodePart.SwitchStatement, "match", "match {0}") },
                        { CodePart.MatchStatement,       LanguagePart.Tmpl(CodePart.MatchStatement, "match", "match {0}") },
                        { CodePart.ForStatement,          LanguagePart.Tmpl(CodePart.ForStatement, "for", "for {0}") },
                        { CodePart.ForEachStatement,     LanguagePart.Tmpl(CodePart.ForEachStatement, "for", "for {0} in {1}") },
                        { CodePart.WhileStatement,       LanguagePart.Tmpl(CodePart.WhileStatement, "while", "while {0}") },
                        { CodePart.DoWhileStatement,     LanguagePart.Tmpl(CodePart.DoWhileStatement, "while", "while {0}") },  // no do-while
                        { CodePart.TryStatement,         LanguagePart.Kw(CodePart.TryStatement, "") },  // Result type instead
                        { CodePart.CatchClause,          LanguagePart.Kw(CodePart.CatchClause, "") },  // match on Result
                        { CodePart.FinallyClause,        LanguagePart.Kw(CodePart.FinallyClause, "") },  // no equivalent
                        { CodePart.ReturnStatement,      LanguagePart.Kw(CodePart.ReturnStatement, "return") },
                        { CodePart.ThrowStatement,       LanguagePart.Kw(CodePart.ThrowStatement, "") },  // return Err()
                        { CodePart.YieldStatement,       LanguagePart.Kw(CodePart.YieldStatement, "yield") },  // generators nightly
                        { CodePart.BreakStatement,       LanguagePart.Kw(CodePart.BreakStatement, "break") },
                        { CodePart.ContinueStatement,    LanguagePart.Kw(CodePart.ContinueStatement, "continue") },
                        { CodePart.BlockStatement,       LanguagePart.Kw(CodePart.BlockStatement, "") },
                        { CodePart.UsingStatement,       LanguagePart.Kw(CodePart.UsingStatement, "") },  // RAII
                        { CodePart.LockStatement,        LanguagePart.Tmpl(CodePart.LockStatement, "Mutex", "let _lock = {0}.lock().unwrap()") },
                        { CodePart.CheckedStatement,    LanguagePart.Kw(CodePart.CheckedStatement, "") },  // no equivalent

                        // Expressions
                        { CodePart.Literal,              LanguagePart.Kw(CodePart.Literal, "") },
                        { CodePart.Identifier,           LanguagePart.Kw(CodePart.Identifier, "") },
                        { CodePart.BinaryExpression,     LanguagePart.Kw(CodePart.BinaryExpression, "") },
                        { CodePart.UnaryExpression,      LanguagePart.Kw(CodePart.UnaryExpression, "") },
                        { CodePart.AssignmentExpression, LanguagePart.Kw(CodePart.AssignmentExpression, "") },
                        { CodePart.MemberAccessExpression, LanguagePart.Kw(CodePart.MemberAccessExpression, ".") },
                        { CodePart.InvocationExpression, LanguagePart.Kw(CodePart.InvocationExpression, "") },
                        { CodePart.ObjectCreationExpression, LanguagePart.Tmpl(CodePart.ObjectCreationExpression, "", "{0}::new({2})") },
                        { CodePart.CastExpression,       LanguagePart.Tmpl(CodePart.CastExpression, "as", "{1} as {0}") },
                        { CodePart.IsTypeExpression,     LanguagePart.Tmpl(CodePart.IsTypeExpression, "is", "{1} is {0}") },  // nightly
                        { CodePart.ParenthesizedExpression, LanguagePart.Kw(CodePart.ParenthesizedExpression, "") },
                        { CodePart.TernaryExpression,    LanguagePart.Kw(CodePart.TernaryExpression, "") },  // if/else expression
                        { CodePart.RangeExpression,      LanguagePart.Tmpl(CodePart.RangeExpression, "..", "{0}..{1}") },
                        { CodePart.LambdaExpression,     LanguagePart.Tmpl(CodePart.LambdaExpression, "|..|", "|{0}| {1}") },
                        { CodePart.AwaitExpression,      LanguagePart.Kw(CodePart.AwaitExpression, ".await") },
                        { CodePart.ArrayCreationExpression, LanguagePart.Tmpl(CodePart.ArrayCreationExpression, "", "[{0}]") },
                        { CodePart.TupleExpression,      LanguagePart.Tmpl(CodePart.TupleExpression, "", "({0})") },

                        // Type decorations
                        { CodePart.PointerDecoration,    LanguagePart.Tmpl(CodePart.PointerDecoration, "*", "*mut {0}") },
                        { CodePart.ReferenceDecoration,  LanguagePart.Tmpl(CodePart.ReferenceDecoration, "&", "&{0}") },
                        { CodePart.NakedDecoration,      LanguagePart.Kw(CodePart.NakedDecoration, "") },

                        // Special
                        { CodePart.FriendDeclaration,    LanguagePart.Kw(CodePart.FriendDeclaration, "") },  // no equivalent
                        { CodePart.ConstructorInitializer, LanguagePart.Kw(CodePart.ConstructorInitializer, "") },  // Self { field: val }
                        { CodePart.DestructorCall,       LanguagePart.Kw(CodePart.DestructorCall, "drop") },
                        { CodePart.NewObject,            LanguagePart.Tmpl(CodePart.NewObject, "", "{0}::new({1})") },
                        { CodePart.NullLiteral,          LanguagePart.Kw(CodePart.NullLiteral, "None") },
                        { CodePart.ThisReference,        LanguagePart.Kw(CodePart.ThisReference, "self") },
                        { CodePart.BaseReference,        LanguagePart.Kw(CodePart.BaseReference, "super::") },
                        { CodePart.StringInterpolation, LanguagePart.Tmpl(CodePart.StringInterpolation, "", "\"{0}\"") },  // format!
                        { CodePart.ArrayIndexer,         LanguagePart.Tmpl(CodePart.ArrayIndexer, "[]", "{0}[{1}]") },
                    }
                }
            },

            // ═══════════════════════════════════════════════════════════════
            //  Python
            // ═══════════════════════════════════════════════════════════════
            {
                Language.Python, new LanguageProfile
                {
                    Name                 = Language.Python,
                    FileExtension        = ".py",
                    LineComment          = "#",
                    BlockCommentStart    = "\"\"\"",
                    BlockCommentEnd      = "\"\"\"",
                    StringDelimiter      = "\"",
                    CharDelimiter        = "'",
                    StatementTerminator  = "",
                    NamespaceSeparator  = ".",
                    MemberAccessDot      = ".",
                    MemberAccessArrow    = ".",
                    MemberAccessScope    = ".",
                    InheritanceMarker    = ":",
                    AccessPublic         = "",
                    AccessPrivate        = "_",
                    AccessProtected      = "_",
                    NullLiteral          = "None",
                    ThisReference        = "self",
                    BaseReference        = "super()",
                    Parts = new Dictionary<CodePart, LanguagePart>
                    {
                        // Structural
                        { CodePart.CompilationUnit,     LanguagePart.Kw(CodePart.CompilationUnit, "") },
                        { CodePart.Namespace,            LanguagePart.Kw(CodePart.Namespace, "") },  // packages via folders
                        { CodePart.UsingDirective,       LanguagePart.Kw(CodePart.UsingDirective, "import") },
                        { CodePart.FileScopeDirective,   LanguagePart.Kw(CodePart.FileScopeDirective, "import") },

                        // Type declarations
                        { CodePart.Class,                LanguagePart.Tmpl(CodePart.Class, "class", "class {0}") },
                        { CodePart.Struct,               LanguagePart.Tmpl(CodePart.Struct, "@dataclass", "@dataclass\nclass {0}") },
                        { CodePart.Interface,            LanguagePart.Tmpl(CodePart.Interface, "ABC", "class {0}(ABC)") },
                        { CodePart.Enum,                 LanguagePart.Tmpl(CodePart.Enum, "Enum", "class {0}(Enum)") },
                        { CodePart.SumType,              LanguagePart.Tmpl(CodePart.SumType, "Union", "{0} = Union[...]") },
                        { CodePart.Mixin,                LanguagePart.Kw(CodePart.Mixin, "") },  // just methods
                        { CodePart.TypeAlias,            LanguagePart.Tmpl(CodePart.TypeAlias, "type", "{0} = {1}") },
                        { CodePart.Delegate,             LanguagePart.Tmpl(CodePart.Delegate, "Callable", "{0} = Callable[[{2}], {1}]") },

                        // Members
                        { CodePart.Field,                LanguagePart.Tmpl(CodePart.Field, "", "{0}: {1}") },
                        { CodePart.Property,             LanguagePart.Tmpl(CodePart.Property, "@property", "@property\ndef {0}(self)") },
                        { CodePart.Method,               LanguagePart.Tmpl(CodePart.Method, "def", "def {0}({2})") },
                        { CodePart.Constructor,          LanguagePart.Tmpl(CodePart.Constructor, "__init__", "def __init__(self, {2})") },
                        { CodePart.Destructor,           LanguagePart.Tmpl(CodePart.Destructor, "__del__", "def __del__(self)") },
                        { CodePart.Finalizer,            LanguagePart.Tmpl(CodePart.Finalizer, "__del__", "def __del__(self)") },
                        { CodePart.OperatorOverload,     LanguagePart.Tmpl(CodePart.OperatorOverload, "__", "def __{0}__(self, {2})") },
                        { CodePart.Indexer,              LanguagePart.Tmpl(CodePart.Indexer, "__getitem__", "def __getitem__(self, {2})") },
                        { CodePart.Event,                LanguagePart.Kw(CodePart.Event, "") },  // no direct equivalent
                        { CodePart.Constant,             LanguagePart.Tmpl(CodePart.Constant, "", "{0} = {4}") },
                        { CodePart.CompanionObject,      LanguagePart.Kw(CodePart.CompanionObject, "") },  // @classmethod

                        // Access levels
                        { CodePart.AccessPublic,         LanguagePart.Kw(CodePart.AccessPublic, "") },  // default
                        { CodePart.AccessPrivate,        LanguagePart.Kw(CodePart.AccessPrivate, "_") },  // convention
                        { CodePart.AccessProtected,      LanguagePart.Kw(CodePart.AccessProtected, "_") },
                        { CodePart.AccessInternal,      LanguagePart.Kw(CodePart.AccessInternal, "_") },

                        // Modifiers
                        { CodePart.StaticModifier,       LanguagePart.Tmpl(CodePart.StaticModifier, "@staticmethod", "@staticmethod") },
                        { CodePart.AbstractModifier,     LanguagePart.Tmpl(CodePart.AbstractModifier, "@abstractmethod", "@abstractmethod") },
                        { CodePart.VirtualModifier,      LanguagePart.Kw(CodePart.VirtualModifier, "") },  // all methods virtual
                        { CodePart.OverrideModifier,    LanguagePart.Kw(CodePart.OverrideModifier, "") },  // no override keyword
                        { CodePart.SealedModifier,      LanguagePart.Kw(CodePart.SealedModifier, "") },  // no sealed
                        { CodePart.AsyncModifier,       LanguagePart.Kw(CodePart.AsyncModifier, "async") },
                        { CodePart.ConstModifier,        LanguagePart.Kw(CodePart.ConstModifier, "") },  // no const
                        { CodePart.MutableModifier,      LanguagePart.Kw(CodePart.MutableModifier, "") },  // no mutable
                        { CodePart.VolatileModifier,    LanguagePart.Kw(CodePart.VolatileModifier, "") },  // no volatile
                        { CodePart.ReadOnlyModifier,    LanguagePart.Kw(CodePart.ReadOnlyModifier, "") },  // no readonly
                        { CodePart.ExplicitModifier,    LanguagePart.Kw(CodePart.ExplicitModifier, "") },  // no equivalent
                        { CodePart.NoexceptModifier,    LanguagePart.Kw(CodePart.NoexceptModifier, "") },  // no equivalent
                        { CodePart.InlineModifier,      LanguagePart.Kw(CodePart.InlineModifier, "") },  // no equivalent

                        // Statements
                        { CodePart.IfStatement,          LanguagePart.Tmpl(CodePart.IfStatement, "if", "if {0}:") },
                        { CodePart.ElseIfClause,         LanguagePart.Tmpl(CodePart.ElseIfClause, "elif", "elif {0}:") },
                        { CodePart.ElseClause,           LanguagePart.Kw(CodePart.ElseClause, "else") },
                        { CodePart.SwitchStatement,      LanguagePart.Tmpl(CodePart.SwitchStatement, "match", "match {0}:") },  // 3.10+
                        { CodePart.MatchStatement,       LanguagePart.Tmpl(CodePart.MatchStatement, "match", "match {0}:") },
                        { CodePart.ForStatement,          LanguagePart.Tmpl(CodePart.ForStatement, "for", "for {0}:") },
                        { CodePart.ForEachStatement,     LanguagePart.Tmpl(CodePart.ForEachStatement, "for", "for {0} in {1}:") },
                        { CodePart.WhileStatement,       LanguagePart.Tmpl(CodePart.WhileStatement, "while", "while {0}:") },
                        { CodePart.DoWhileStatement,     LanguagePart.Tmpl(CodePart.DoWhileStatement, "while", "while True: ... if not {0}: break") },
                        { CodePart.TryStatement,         LanguagePart.Kw(CodePart.TryStatement, "try") },
                        { CodePart.CatchClause,          LanguagePart.Tmpl(CodePart.CatchClause, "except", "except {0}:") },
                        { CodePart.FinallyClause,        LanguagePart.Kw(CodePart.FinallyClause, "finally") },
                        { CodePart.ReturnStatement,      LanguagePart.Kw(CodePart.ReturnStatement, "return") },
                        { CodePart.ThrowStatement,       LanguagePart.Kw(CodePart.ThrowStatement, "raise") },
                        { CodePart.YieldStatement,       LanguagePart.Kw(CodePart.YieldStatement, "yield") },
                        { CodePart.BreakStatement,       LanguagePart.Kw(CodePart.BreakStatement, "break") },
                        { CodePart.ContinueStatement,    LanguagePart.Kw(CodePart.ContinueStatement, "continue") },
                        { CodePart.BlockStatement,       LanguagePart.Kw(CodePart.BlockStatement, "") },
                        { CodePart.UsingStatement,       LanguagePart.Tmpl(CodePart.UsingStatement, "with", "with {0}:") },
                        { CodePart.LockStatement,        LanguagePart.Tmpl(CodePart.LockStatement, "with", "with {0}:") },
                        { CodePart.CheckedStatement,    LanguagePart.Kw(CodePart.CheckedStatement, "") },  // no equivalent

                        // Expressions
                        { CodePart.Literal,              LanguagePart.Kw(CodePart.Literal, "") },
                        { CodePart.Identifier,           LanguagePart.Kw(CodePart.Identifier, "") },
                        { CodePart.BinaryExpression,     LanguagePart.Kw(CodePart.BinaryExpression, "") },
                        { CodePart.UnaryExpression,      LanguagePart.Kw(CodePart.UnaryExpression, "") },
                        { CodePart.AssignmentExpression, LanguagePart.Kw(CodePart.AssignmentExpression, "") },
                        { CodePart.MemberAccessExpression, LanguagePart.Kw(CodePart.MemberAccessExpression, ".") },
                        { CodePart.InvocationExpression, LanguagePart.Kw(CodePart.InvocationExpression, "") },
                        { CodePart.ObjectCreationExpression, LanguagePart.Tmpl(CodePart.ObjectCreationExpression, "", "{0}({2})") },
                        { CodePart.CastExpression,       LanguagePart.Tmpl(CodePart.CastExpression, "", "{0}({1})") },
                        { CodePart.IsTypeExpression,     LanguagePart.Tmpl(CodePart.IsTypeExpression, "isinstance", "isinstance({1}, {0})") },
                        { CodePart.ParenthesizedExpression, LanguagePart.Kw(CodePart.ParenthesizedExpression, "") },
                        { CodePart.TernaryExpression,    LanguagePart.Tmpl(CodePart.TernaryExpression, "", "{1} if {0} else {2}") },
                        { CodePart.RangeExpression,      LanguagePart.Tmpl(CodePart.RangeExpression, "range", "range({0}, {1})") },
                        { CodePart.LambdaExpression,     LanguagePart.Tmpl(CodePart.LambdaExpression, "lambda", "lambda {0}: {1}") },
                        { CodePart.AwaitExpression,      LanguagePart.Kw(CodePart.AwaitExpression, "await") },
                        { CodePart.ArrayCreationExpression, LanguagePart.Tmpl(CodePart.ArrayCreationExpression, "", "[{0}]") },
                        { CodePart.TupleExpression,      LanguagePart.Tmpl(CodePart.TupleExpression, "", "({0})") },

                        // Type decorations
                        { CodePart.PointerDecoration,    LanguagePart.Kw(CodePart.PointerDecoration, "") },  // no pointers
                        { CodePart.ReferenceDecoration,  LanguagePart.Kw(CodePart.ReferenceDecoration, "") },  // no references
                        { CodePart.NakedDecoration,      LanguagePart.Kw(CodePart.NakedDecoration, "") },

                        // Special
                        { CodePart.FriendDeclaration,    LanguagePart.Kw(CodePart.FriendDeclaration, "") },  // no equivalent
                        { CodePart.ConstructorInitializer, LanguagePart.Kw(CodePart.ConstructorInitializer, "") },
                        { CodePart.DestructorCall,       LanguagePart.Kw(CodePart.DestructorCall, "__del__") },
                        { CodePart.NewObject,            LanguagePart.Tmpl(CodePart.NewObject, "", "{0}({1})") },
                        { CodePart.NullLiteral,          LanguagePart.Kw(CodePart.NullLiteral, "None") },
                        { CodePart.ThisReference,        LanguagePart.Kw(CodePart.ThisReference, "self") },
                        { CodePart.BaseReference,        LanguagePart.Kw(CodePart.BaseReference, "super()") },
                        { CodePart.StringInterpolation, LanguagePart.Tmpl(CodePart.StringInterpolation, "f", "f\"{0}\"") },
                        { CodePart.ArrayIndexer,         LanguagePart.Tmpl(CodePart.ArrayIndexer, "[]", "{0}[{1}]") },
                    }
                }
            },

            // ═══════════════════════════════════════════════════════════════
            //  Swift
            // ═══════════════════════════════════════════════════════════════
            {
                Language.Swift, new LanguageProfile
                {
                    Name                 = Language.Swift,
                    FileExtension        = ".swift",
                    LineComment          = "//",
                    BlockCommentStart    = "/*",
                    BlockCommentEnd      = "*/",
                    StringDelimiter      = "\"",
                    CharDelimiter        = "'",
                    StatementTerminator  = "",
                    NamespaceSeparator  = ".",
                    MemberAccessDot      = ".",
                    MemberAccessArrow    = ".",
                    MemberAccessScope    = ".",
                    InheritanceMarker    = ":",
                    AccessPublic         = "public",
                    AccessPrivate        = "private",
                    AccessProtected      = "internal",
                    NullLiteral          = "nil",
                    ThisReference        = "self",
                    BaseReference        = "super",
                    Parts = new Dictionary<CodePart, LanguagePart>
                    {
                        // Structural
                        { CodePart.CompilationUnit,     LanguagePart.Kw(CodePart.CompilationUnit, "") },
                        { CodePart.Namespace,            LanguagePart.Kw(CodePart.Namespace, "") },  // modules
                        { CodePart.UsingDirective,       LanguagePart.Kw(CodePart.UsingDirective, "import") },
                        { CodePart.FileScopeDirective,   LanguagePart.Kw(CodePart.FileScopeDirective, "import") },

                        // Type declarations
                        { CodePart.Class,                LanguagePart.Tmpl(CodePart.Class, "class", "class {0}") },
                        { CodePart.Struct,               LanguagePart.Tmpl(CodePart.Struct, "struct", "struct {0}") },
                        { CodePart.Interface,            LanguagePart.Tmpl(CodePart.Interface, "protocol", "protocol {0}") },
                        { CodePart.Enum,                 LanguagePart.Tmpl(CodePart.Enum, "enum", "enum {0}") },
                        { CodePart.SumType,              LanguagePart.Tmpl(CodePart.SumType, "enum", "enum {0}") },
                        { CodePart.Mixin,                LanguagePart.Tmpl(CodePart.Mixin, "extension", "extension {0}") },
                        { CodePart.TypeAlias,            LanguagePart.Tmpl(CodePart.TypeAlias, "typealias", "typealias {0} = {1}") },
                        { CodePart.Delegate,             LanguagePart.Tmpl(CodePart.Delegate, "", "({2}) -> {1}") },

                        // Members
                        { CodePart.Field,                LanguagePart.Tmpl(CodePart.Field, "var", "var {0}: {1}") },
                        { CodePart.Property,             LanguagePart.Tmpl(CodePart.Property, "var", "var {0}: {1}") },
                        { CodePart.Method,               LanguagePart.Tmpl(CodePart.Method, "func", "func {0}({2}) -> {1}") },
                        { CodePart.Constructor,          LanguagePart.Tmpl(CodePart.Constructor, "init", "init({2})") },
                        { CodePart.Destructor,           LanguagePart.Tmpl(CodePart.Destructor, "deinit", "deinit") },
                        { CodePart.Finalizer,            LanguagePart.Tmpl(CodePart.Finalizer, "deinit", "deinit") },
                        { CodePart.OperatorOverload,     LanguagePart.Tmpl(CodePart.OperatorOverload, "func", "static func {0}({2})") },
                        { CodePart.Indexer,              LanguagePart.Tmpl(CodePart.Indexer, "subscript", "subscript({2}) -> {1}") },
                        { CodePart.Event,                LanguagePart.Kw(CodePart.Event, "") },  // no direct equivalent
                        { CodePart.Constant,             LanguagePart.Tmpl(CodePart.Constant, "let", "let {0}: {1} = {4}") },
                        { CodePart.CompanionObject,      LanguagePart.Kw(CodePart.CompanionObject, "") },  // static var/methods

                        // Access levels
                        { CodePart.AccessPublic,         LanguagePart.Kw(CodePart.AccessPublic, "public") },
                        { CodePart.AccessPrivate,        LanguagePart.Kw(CodePart.AccessPrivate, "private") },
                        { CodePart.AccessProtected,      LanguagePart.Kw(CodePart.AccessProtected, "internal") },
                        { CodePart.AccessInternal,      LanguagePart.Kw(CodePart.AccessInternal, "fileprivate") },

                        // Modifiers
                        { CodePart.StaticModifier,       LanguagePart.Kw(CodePart.StaticModifier, "static") },
                        { CodePart.AbstractModifier,     LanguagePart.Kw(CodePart.AbstractModifier, "") },  // protocol default
                        { CodePart.VirtualModifier,      LanguagePart.Kw(CodePart.VirtualModifier, "") },  // dynamic dispatch default
                        { CodePart.OverrideModifier,    LanguagePart.Kw(CodePart.OverrideModifier, "override") },
                        { CodePart.SealedModifier,      LanguagePart.Kw(CodePart.SealedModifier, "final") },
                        { CodePart.AsyncModifier,       LanguagePart.Kw(CodePart.AsyncModifier, "async") },
                        { CodePart.ConstModifier,        LanguagePart.Kw(CodePart.ConstModifier, "let") },
                        { CodePart.MutableModifier,      LanguagePart.Kw(CodePart.MutableModifier, "mutating") },
                        { CodePart.VolatileModifier,    LanguagePart.Kw(CodePart.VolatileModifier, "") },  // no volatile
                        { CodePart.ReadOnlyModifier,    LanguagePart.Kw(CodePart.ReadOnlyModifier, "let") },
                        { CodePart.ExplicitModifier,    LanguagePart.Kw(CodePart.ExplicitModifier, "") },  // no equivalent
                        { CodePart.NoexceptModifier,    LanguagePart.Kw(CodePart.NoexceptModifier, "") },  // throws instead
                        { CodePart.InlineModifier,      LanguagePart.Tmpl(CodePart.InlineModifier, "@inlinable", "@inlinable") },

                        // Statements
                        { CodePart.IfStatement,          LanguagePart.Tmpl(CodePart.IfStatement, "if", "if {0}") },
                        { CodePart.ElseIfClause,         LanguagePart.Tmpl(CodePart.ElseIfClause, "else if", "else if {0}") },
                        { CodePart.ElseClause,           LanguagePart.Kw(CodePart.ElseClause, "else") },
                        { CodePart.SwitchStatement,      LanguagePart.Tmpl(CodePart.SwitchStatement, "switch", "switch {0}") },
                        { CodePart.MatchStatement,       LanguagePart.Tmpl(CodePart.MatchStatement, "switch", "switch {0}") },
                        { CodePart.ForStatement,          LanguagePart.Tmpl(CodePart.ForStatement, "for", "for {0}") },
                        { CodePart.ForEachStatement,     LanguagePart.Tmpl(CodePart.ForEachStatement, "for", "for {0} in {1}") },
                        { CodePart.WhileStatement,       LanguagePart.Tmpl(CodePart.WhileStatement, "while", "while {0}") },
                        { CodePart.DoWhileStatement,     LanguagePart.Tmpl(CodePart.DoWhileStatement, "repeat", "repeat ... while {0}") },
                        { CodePart.TryStatement,         LanguagePart.Kw(CodePart.TryStatement, "try") },
                        { CodePart.CatchClause,          LanguagePart.Tmpl(CodePart.CatchClause, "catch", "catch {0}") },
                        { CodePart.FinallyClause,        LanguagePart.Kw(CodePart.FinallyClause, "") },  // defer instead
                        { CodePart.ReturnStatement,      LanguagePart.Kw(CodePart.ReturnStatement, "return") },
                        { CodePart.ThrowStatement,       LanguagePart.Kw(CodePart.ThrowStatement, "throw") },
                        { CodePart.YieldStatement,       LanguagePart.Kw(CodePart.YieldStatement, "yield") },  // AsyncStream
                        { CodePart.BreakStatement,       LanguagePart.Kw(CodePart.BreakStatement, "break") },
                        { CodePart.ContinueStatement,    LanguagePart.Kw(CodePart.ContinueStatement, "continue") },
                        { CodePart.BlockStatement,       LanguagePart.Kw(CodePart.BlockStatement, "") },
                        { CodePart.UsingStatement,       LanguagePart.Kw(CodePart.UsingStatement, "") },  // defer / RAII
                        { CodePart.LockStatement,        LanguagePart.Tmpl(CodePart.LockStatement, "NSLock", "lock.lock()") },
                        { CodePart.CheckedStatement,    LanguagePart.Kw(CodePart.CheckedStatement, "") },  // no equivalent

                        // Expressions
                        { CodePart.Literal,              LanguagePart.Kw(CodePart.Literal, "") },
                        { CodePart.Identifier,           LanguagePart.Kw(CodePart.Identifier, "") },
                        { CodePart.BinaryExpression,     LanguagePart.Kw(CodePart.BinaryExpression, "") },
                        { CodePart.UnaryExpression,      LanguagePart.Kw(CodePart.UnaryExpression, "") },
                        { CodePart.AssignmentExpression, LanguagePart.Kw(CodePart.AssignmentExpression, "") },
                        { CodePart.MemberAccessExpression, LanguagePart.Kw(CodePart.MemberAccessExpression, ".") },
                        { CodePart.InvocationExpression, LanguagePart.Kw(CodePart.InvocationExpression, "") },
                        { CodePart.ObjectCreationExpression, LanguagePart.Tmpl(CodePart.ObjectCreationExpression, "", "{0}({2})") },
                        { CodePart.CastExpression,       LanguagePart.Tmpl(CodePart.CastExpression, "as", "{1} as! {0}") },
                        { CodePart.IsTypeExpression,     LanguagePart.Tmpl(CodePart.IsTypeExpression, "is", "{1} is {0}") },
                        { CodePart.ParenthesizedExpression, LanguagePart.Kw(CodePart.ParenthesizedExpression, "") },
                        { CodePart.TernaryExpression,    LanguagePart.Tmpl(CodePart.TernaryExpression, "", "{0} ? {1} : {2}") },
                        { CodePart.RangeExpression,      LanguagePart.Tmpl(CodePart.RangeExpression, "...", "{0}...{1}") },
                        { CodePart.LambdaExpression,     LanguagePart.Tmpl(CodePart.LambdaExpression, "in", "{{ {0} in {1} }}") },
                        { CodePart.AwaitExpression,      LanguagePart.Kw(CodePart.AwaitExpression, "await") },
                        { CodePart.ArrayCreationExpression, LanguagePart.Tmpl(CodePart.ArrayCreationExpression, "", "[{0}]") },
                        { CodePart.TupleExpression,      LanguagePart.Tmpl(CodePart.TupleExpression, "", "({0})") },

                        // Type decorations
                        { CodePart.PointerDecoration,    LanguagePart.Kw(CodePart.PointerDecoration, "") },  // no pointers
                        { CodePart.ReferenceDecoration,  LanguagePart.Tmpl(CodePart.ReferenceDecoration, "inout", "inout {0}") },
                        { CodePart.NakedDecoration,      LanguagePart.Kw(CodePart.NakedDecoration, "") },

                        // Special
                        { CodePart.FriendDeclaration,    LanguagePart.Kw(CodePart.FriendDeclaration, "") },  // no equivalent
                        { CodePart.ConstructorInitializer, LanguagePart.Kw(CodePart.ConstructorInitializer, "") },
                        { CodePart.DestructorCall,       LanguagePart.Kw(CodePart.DestructorCall, "deinit") },
                        { CodePart.NewObject,            LanguagePart.Tmpl(CodePart.NewObject, "", "{0}({1})") },
                        { CodePart.NullLiteral,          LanguagePart.Kw(CodePart.NullLiteral, "nil") },
                        { CodePart.ThisReference,        LanguagePart.Kw(CodePart.ThisReference, "self") },
                        { CodePart.BaseReference,        LanguagePart.Kw(CodePart.BaseReference, "super") },
                        { CodePart.StringInterpolation, LanguagePart.Tmpl(CodePart.StringInterpolation, "", "\"\\({0})\"") },
                        { CodePart.ArrayIndexer,         LanguagePart.Tmpl(CodePart.ArrayIndexer, "[]", "{0}[{1}]") },
                    }
                }
            },

            // ═══════════════════════════════════════════════════════════════
            //  Kotlin
            // ═══════════════════════════════════════════════════════════════
            {
                Language.Kotlin, new LanguageProfile
                {
                    Name                 = Language.Kotlin,
                    FileExtension        = ".kt",
                    LineComment          = "//",
                    BlockCommentStart    = "/*",
                    BlockCommentEnd      = "*/",
                    StringDelimiter      = "\"",
                    CharDelimiter        = "'",
                    StatementTerminator  = "",
                    NamespaceSeparator  = ".",
                    MemberAccessDot      = ".",
                    MemberAccessArrow    = ".",
                    MemberAccessScope    = ".",
                    InheritanceMarker    = ":",
                    AccessPublic         = "",
                    AccessPrivate        = "private",
                    AccessProtected      = "protected",
                    NullLiteral          = "null",
                    ThisReference        = "this",
                    BaseReference        = "super",
                    Parts = new Dictionary<CodePart, LanguagePart>
                    {
                        // Structural
                        { CodePart.CompilationUnit,     LanguagePart.Kw(CodePart.CompilationUnit, "") },
                        { CodePart.Namespace,            LanguagePart.Kw(CodePart.Namespace, "package") },
                        { CodePart.UsingDirective,       LanguagePart.Kw(CodePart.UsingDirective, "import") },
                        { CodePart.FileScopeDirective,   LanguagePart.Kw(CodePart.FileScopeDirective, "import") },

                        // Type declarations
                        { CodePart.Class,                LanguagePart.Tmpl(CodePart.Class, "class", "class {0}") },
                        { CodePart.Struct,               LanguagePart.Tmpl(CodePart.Struct, "data class", "data class {0}") },
                        { CodePart.Interface,            LanguagePart.Tmpl(CodePart.Interface, "interface", "interface {0}") },
                        { CodePart.Enum,                 LanguagePart.Tmpl(CodePart.Enum, "enum", "enum class {0}") },
                        { CodePart.SumType,              LanguagePart.Tmpl(CodePart.SumType, "sealed class", "sealed class {0}") },
                        { CodePart.Mixin,                LanguagePart.Tmpl(CodePart.Mixin, "fun", "fun {0}.") },  // extension
                        { CodePart.TypeAlias,            LanguagePart.Tmpl(CodePart.TypeAlias, "typealias", "typealias {0} = {1}") },
                        { CodePart.Delegate,             LanguagePart.Tmpl(CodePart.Delegate, "", "({2}) -> {1}") },

                        // Members
                        { CodePart.Field,                LanguagePart.Tmpl(CodePart.Field, "var", "var {0}: {1}") },
                        { CodePart.Property,             LanguagePart.Tmpl(CodePart.Property, "val", "val {0}: {1}") },
                        { CodePart.Method,               LanguagePart.Tmpl(CodePart.Method, "fun", "fun {0}({2}): {1}") },
                        { CodePart.Constructor,          LanguagePart.Tmpl(CodePart.Constructor, "constructor", "constructor({2})") },
                        { CodePart.Destructor,           LanguagePart.Kw(CodePart.Destructor, "") },  // no dtor
                        { CodePart.Finalizer,            LanguagePart.Kw(CodePart.Finalizer, "") },  // no finalizer
                        { CodePart.OperatorOverload,     LanguagePart.Tmpl(CodePart.OperatorOverload, "operator", "operator fun {0}({2})") },
                        { CodePart.Indexer,              LanguagePart.Tmpl(CodePart.Indexer, "get", "operator fun get({2})") },
                        { CodePart.Event,                LanguagePart.Kw(CodePart.Event, "") },  // no direct equivalent
                        { CodePart.Constant,             LanguagePart.Tmpl(CodePart.Constant, "const", "const val {0}: {1} = {4}") },
                        { CodePart.CompanionObject,      LanguagePart.Tmpl(CodePart.CompanionObject, "companion object", "companion object") },

                        // Access levels
                        { CodePart.AccessPublic,         LanguagePart.Kw(CodePart.AccessPublic, "") },  // default
                        { CodePart.AccessPrivate,        LanguagePart.Kw(CodePart.AccessPrivate, "private") },
                        { CodePart.AccessProtected,      LanguagePart.Kw(CodePart.AccessProtected, "protected") },
                        { CodePart.AccessInternal,      LanguagePart.Kw(CodePart.AccessInternal, "internal") },

                        // Modifiers
                        { CodePart.StaticModifier,       LanguagePart.Kw(CodePart.StaticModifier, "") },  // companion object
                        { CodePart.AbstractModifier,     LanguagePart.Kw(CodePart.AbstractModifier, "abstract") },
                        { CodePart.VirtualModifier,      LanguagePart.Kw(CodePart.VirtualModifier, "open") },
                        { CodePart.OverrideModifier,    LanguagePart.Kw(CodePart.OverrideModifier, "override") },
                        { CodePart.SealedModifier,      LanguagePart.Kw(CodePart.SealedModifier, "final") },
                        { CodePart.AsyncModifier,       LanguagePart.Kw(CodePart.AsyncModifier, "suspend") },
                        { CodePart.ConstModifier,        LanguagePart.Kw(CodePart.ConstModifier, "val") },
                        { CodePart.MutableModifier,      LanguagePart.Kw(CodePart.MutableModifier, "var") },
                        { CodePart.VolatileModifier,    LanguagePart.Tmpl(CodePart.VolatileModifier, "@Volatile", "@Volatile") },
                        { CodePart.ReadOnlyModifier,    LanguagePart.Kw(CodePart.ReadOnlyModifier, "val") },
                        { CodePart.ExplicitModifier,    LanguagePart.Kw(CodePart.ExplicitModifier, "") },  // no equivalent
                        { CodePart.NoexceptModifier,    LanguagePart.Kw(CodePart.NoexceptModifier, "") },  // no equivalent
                        { CodePart.InlineModifier,      LanguagePart.Tmpl(CodePart.InlineModifier, "inline", "inline") },

                        // Statements
                        { CodePart.IfStatement,          LanguagePart.Tmpl(CodePart.IfStatement, "if", "if ({0})") },
                        { CodePart.ElseIfClause,         LanguagePart.Tmpl(CodePart.ElseIfClause, "else if", "else if ({0})") },
                        { CodePart.ElseClause,           LanguagePart.Kw(CodePart.ElseClause, "else") },
                        { CodePart.SwitchStatement,      LanguagePart.Tmpl(CodePart.SwitchStatement, "when", "when ({0})") },
                        { CodePart.MatchStatement,       LanguagePart.Tmpl(CodePart.MatchStatement, "when", "when ({0})") },
                        { CodePart.ForStatement,          LanguagePart.Tmpl(CodePart.ForStatement, "for", "for ({0})") },
                        { CodePart.ForEachStatement,     LanguagePart.Tmpl(CodePart.ForEachStatement, "for", "for ({0} in {1})") },
                        { CodePart.WhileStatement,       LanguagePart.Tmpl(CodePart.WhileStatement, "while", "while ({0})") },
                        { CodePart.DoWhileStatement,     LanguagePart.Tmpl(CodePart.DoWhileStatement, "do", "do ... while ({0})") },
                        { CodePart.TryStatement,         LanguagePart.Kw(CodePart.TryStatement, "try") },
                        { CodePart.CatchClause,          LanguagePart.Tmpl(CodePart.CatchClause, "catch", "catch ({0})") },
                        { CodePart.FinallyClause,        LanguagePart.Kw(CodePart.FinallyClause, "finally") },
                        { CodePart.ReturnStatement,      LanguagePart.Kw(CodePart.ReturnStatement, "return") },
                        { CodePart.ThrowStatement,       LanguagePart.Kw(CodePart.ThrowStatement, "throw") },
                        { CodePart.YieldStatement,       LanguagePart.Kw(CodePart.YieldStatement, "yield") },
                        { CodePart.BreakStatement,       LanguagePart.Kw(CodePart.BreakStatement, "break") },
                        { CodePart.ContinueStatement,    LanguagePart.Kw(CodePart.ContinueStatement, "continue") },
                        { CodePart.BlockStatement,       LanguagePart.Kw(CodePart.BlockStatement, "") },
                        { CodePart.UsingStatement,       LanguagePart.Tmpl(CodePart.UsingStatement, "use", "{0}.use") },
                        { CodePart.LockStatement,        LanguagePart.Tmpl(CodePart.LockStatement, "synchronized", "synchronized({0})") },
                        { CodePart.CheckedStatement,    LanguagePart.Kw(CodePart.CheckedStatement, "") },  // no equivalent

                        // Expressions
                        { CodePart.Literal,              LanguagePart.Kw(CodePart.Literal, "") },
                        { CodePart.Identifier,           LanguagePart.Kw(CodePart.Identifier, "") },
                        { CodePart.BinaryExpression,     LanguagePart.Kw(CodePart.BinaryExpression, "") },
                        { CodePart.UnaryExpression,      LanguagePart.Kw(CodePart.UnaryExpression, "") },
                        { CodePart.AssignmentExpression, LanguagePart.Kw(CodePart.AssignmentExpression, "") },
                        { CodePart.MemberAccessExpression, LanguagePart.Kw(CodePart.MemberAccessExpression, ".") },
                        { CodePart.InvocationExpression, LanguagePart.Kw(CodePart.InvocationExpression, "") },
                        { CodePart.ObjectCreationExpression, LanguagePart.Tmpl(CodePart.ObjectCreationExpression, "", "{0}({2})") },
                        { CodePart.CastExpression,       LanguagePart.Tmpl(CodePart.CastExpression, "as", "{1} as {0}") },
                        { CodePart.IsTypeExpression,     LanguagePart.Tmpl(CodePart.IsTypeExpression, "is", "{1} is {0}") },
                        { CodePart.ParenthesizedExpression, LanguagePart.Kw(CodePart.ParenthesizedExpression, "") },
                        { CodePart.TernaryExpression,    LanguagePart.Tmpl(CodePart.TernaryExpression, "", "if ({0}) {1} else {2}") },
                        { CodePart.RangeExpression,      LanguagePart.Tmpl(CodePart.RangeExpression, "..", "{0}..{1}") },
                        { CodePart.LambdaExpression,     LanguagePart.Tmpl(CodePart.LambdaExpression, "", "{{ {0} -> {1} }}") },
                        { CodePart.AwaitExpression,      LanguagePart.Kw(CodePart.AwaitExpression, "await") },
                        { CodePart.ArrayCreationExpression, LanguagePart.Tmpl(CodePart.ArrayCreationExpression, "", "arrayOf({0})") },
                        { CodePart.TupleExpression,      LanguagePart.Tmpl(CodePart.TupleExpression, "", "Pair({0})") },

                        // Type decorations
                        { CodePart.PointerDecoration,    LanguagePart.Kw(CodePart.PointerDecoration, "") },  // no pointers
                        { CodePart.ReferenceDecoration,  LanguagePart.Kw(CodePart.ReferenceDecoration, "") },  // no references
                        { CodePart.NakedDecoration,      LanguagePart.Kw(CodePart.NakedDecoration, "") },

                        // Special
                        { CodePart.FriendDeclaration,    LanguagePart.Kw(CodePart.FriendDeclaration, "") },  // no equivalent
                        { CodePart.ConstructorInitializer, LanguagePart.Kw(CodePart.ConstructorInitializer, "") },
                        { CodePart.DestructorCall,       LanguagePart.Kw(CodePart.DestructorCall, "") },  // no dtor
                        { CodePart.NewObject,            LanguagePart.Tmpl(CodePart.NewObject, "", "{0}({1})") },
                        { CodePart.NullLiteral,          LanguagePart.Kw(CodePart.NullLiteral, "null") },
                        { CodePart.ThisReference,        LanguagePart.Kw(CodePart.ThisReference, "this") },
                        { CodePart.BaseReference,        LanguagePart.Kw(CodePart.BaseReference, "super") },
                        { CodePart.StringInterpolation, LanguagePart.Tmpl(CodePart.StringInterpolation, "$", "\"${0}\"") },
                        { CodePart.ArrayIndexer,         LanguagePart.Tmpl(CodePart.ArrayIndexer, "[]", "{0}[{1}]") },
                    }
                }
            },

            // ═══════════════════════════════════════════════════════════════
            //  Java
            // ═══════════════════════════════════════════════════════════════
            {
                Language.Java, new LanguageProfile
                {
                    Name                 = Language.Java,
                    FileExtension        = ".java",
                    LineComment          = "//",
                    BlockCommentStart    = "/*",
                    BlockCommentEnd      = "*/",
                    StringDelimiter      = "\"",
                    CharDelimiter        = "'",
                    StatementTerminator  = ";",
                    NamespaceSeparator  = ".",
                    MemberAccessDot      = ".",
                    MemberAccessArrow    = ".",
                    MemberAccessScope    = ".",
                    InheritanceMarker    = "extends",
                    AccessPublic         = "public",
                    AccessPrivate        = "private",
                    AccessProtected      = "protected",
                    NullLiteral          = "null",
                    ThisReference        = "this",
                    BaseReference        = "super",
                    Parts = new Dictionary<CodePart, LanguagePart>
                    {
                        // Structural
                        { CodePart.CompilationUnit,     LanguagePart.Kw(CodePart.CompilationUnit, "") },
                        { CodePart.Namespace,            LanguagePart.Kw(CodePart.Namespace, "package") },
                        { CodePart.UsingDirective,       LanguagePart.Kw(CodePart.UsingDirective, "import") },
                        { CodePart.FileScopeDirective,   LanguagePart.Kw(CodePart.FileScopeDirective, "import") },

                        // Type declarations
                        { CodePart.Class,                LanguagePart.Tmpl(CodePart.Class, "class", "class {0}") },
                        { CodePart.Struct,               LanguagePart.Tmpl(CodePart.Struct, "record", "record {0}") },  // Java 16+
                        { CodePart.Interface,            LanguagePart.Tmpl(CodePart.Interface, "interface", "interface {0}") },
                        { CodePart.Enum,                 LanguagePart.Tmpl(CodePart.Enum, "enum", "enum {0}") },
                        { CodePart.SumType,              LanguagePart.Tmpl(CodePart.SumType, "sealed", "sealed class {0}") },
                        { CodePart.Mixin,                LanguagePart.Kw(CodePart.Mixin, "") },  // default methods
                        { CodePart.TypeAlias,            LanguagePart.Kw(CodePart.TypeAlias, "") },  // no typealias
                        { CodePart.Delegate,             LanguagePart.Tmpl(CodePart.Delegate, "", "{1} {0}({2})") },  // functional interface

                        // Members
                        { CodePart.Field,                LanguagePart.Tmpl(CodePart.Field, "", "{1} {0}") },
                        { CodePart.Property,             LanguagePart.Tmpl(CodePart.Property, "", "{1} {0}") },  // getter/setter
                        { CodePart.Method,               LanguagePart.Tmpl(CodePart.Method, "", "{1} {0}({2})") },
                        { CodePart.Constructor,          LanguagePart.Tmpl(CodePart.Constructor, "", "{0}({2})") },
                        { CodePart.Destructor,           LanguagePart.Kw(CodePart.Destructor, "") },  // no dtor, finalize deprecated
                        { CodePart.Finalizer,            LanguagePart.Tmpl(CodePart.Finalizer, "finalize", "protected void finalize()") },
                        { CodePart.OperatorOverload,     LanguagePart.Kw(CodePart.OperatorOverload, "") },  // no operator overloading
                        { CodePart.Indexer,              LanguagePart.Tmpl(CodePart.Indexer, "get", "{1} get({2})") },
                        { CodePart.Event,                LanguagePart.Kw(CodePart.Event, "") },  // no direct equivalent
                        { CodePart.Constant,             LanguagePart.Tmpl(CodePart.Constant, "static final", "static final {1} {0} = {4}") },
                        { CodePart.CompanionObject,      LanguagePart.Kw(CodePart.CompanionObject, "") },  // static inner class

                        // Access levels
                        { CodePart.AccessPublic,         LanguagePart.Kw(CodePart.AccessPublic, "public") },
                        { CodePart.AccessPrivate,        LanguagePart.Kw(CodePart.AccessPrivate, "private") },
                        { CodePart.AccessProtected,      LanguagePart.Kw(CodePart.AccessProtected, "protected") },
                        { CodePart.AccessInternal,      LanguagePart.Kw(CodePart.AccessInternal, "") },  // package-private

                        // Modifiers
                        { CodePart.StaticModifier,       LanguagePart.Kw(CodePart.StaticModifier, "static") },
                        { CodePart.AbstractModifier,     LanguagePart.Kw(CodePart.AbstractModifier, "abstract") },
                        { CodePart.VirtualModifier,      LanguagePart.Kw(CodePart.VirtualModifier, "") },  // default
                        { CodePart.OverrideModifier,    LanguagePart.Tmpl(CodePart.OverrideModifier, "@Override", "@Override") },
                        { CodePart.SealedModifier,      LanguagePart.Kw(CodePart.SealedModifier, "final") },
                        { CodePart.AsyncModifier,       LanguagePart.Tmpl(CodePart.AsyncModifier, "", "CompletableFuture") },
                        { CodePart.ConstModifier,        LanguagePart.Kw(CodePart.ConstModifier, "final") },
                        { CodePart.MutableModifier,      LanguagePart.Kw(CodePart.MutableModifier, "") },  // no mutable
                        { CodePart.VolatileModifier,    LanguagePart.Kw(CodePart.VolatileModifier, "volatile") },
                        { CodePart.ReadOnlyModifier,    LanguagePart.Kw(CodePart.ReadOnlyModifier, "final") },
                        { CodePart.ExplicitModifier,    LanguagePart.Kw(CodePart.ExplicitModifier, "") },  // no equivalent
                        { CodePart.NoexceptModifier,    LanguagePart.Kw(CodePart.NoexceptModifier, "") },  // no equivalent
                        { CodePart.InlineModifier,      LanguagePart.Kw(CodePart.InlineModifier, "") },  // no equivalent

                        // Statements
                        { CodePart.IfStatement,          LanguagePart.Tmpl(CodePart.IfStatement, "if", "if ({0})") },
                        { CodePart.ElseIfClause,         LanguagePart.Tmpl(CodePart.ElseIfClause, "else if", "else if ({0})") },
                        { CodePart.ElseClause,           LanguagePart.Kw(CodePart.ElseClause, "else") },
                        { CodePart.SwitchStatement,      LanguagePart.Tmpl(CodePart.SwitchStatement, "switch", "switch ({0})") },
                        { CodePart.MatchStatement,       LanguagePart.Tmpl(CodePart.MatchStatement, "switch", "switch ({0})") },
                        { CodePart.ForStatement,          LanguagePart.Tmpl(CodePart.ForStatement, "for", "for ({0})") },
                        { CodePart.ForEachStatement,     LanguagePart.Tmpl(CodePart.ForEachStatement, "for", "for ({0} : {1})") },
                        { CodePart.WhileStatement,       LanguagePart.Tmpl(CodePart.WhileStatement, "while", "while ({0})") },
                        { CodePart.DoWhileStatement,     LanguagePart.Tmpl(CodePart.DoWhileStatement, "do", "do ... while ({0})") },
                        { CodePart.TryStatement,         LanguagePart.Kw(CodePart.TryStatement, "try") },
                        { CodePart.CatchClause,          LanguagePart.Tmpl(CodePart.CatchClause, "catch", "catch ({0})") },
                        { CodePart.FinallyClause,        LanguagePart.Kw(CodePart.FinallyClause, "finally") },
                        { CodePart.ReturnStatement,      LanguagePart.Kw(CodePart.ReturnStatement, "return") },
                        { CodePart.ThrowStatement,       LanguagePart.Kw(CodePart.ThrowStatement, "throw") },
                        { CodePart.YieldStatement,       LanguagePart.Kw(CodePart.YieldStatement, "yield") },
                        { CodePart.BreakStatement,       LanguagePart.Kw(CodePart.BreakStatement, "break") },
                        { CodePart.ContinueStatement,    LanguagePart.Kw(CodePart.ContinueStatement, "continue") },
                        { CodePart.BlockStatement,       LanguagePart.Kw(CodePart.BlockStatement, "") },
                        { CodePart.UsingStatement,       LanguagePart.Tmpl(CodePart.UsingStatement, "try", "try ({0})") },
                        { CodePart.LockStatement,        LanguagePart.Tmpl(CodePart.LockStatement, "synchronized", "synchronized ({0})") },
                        { CodePart.CheckedStatement,    LanguagePart.Kw(CodePart.CheckedStatement, "") },  // no equivalent

                        // Expressions
                        { CodePart.Literal,              LanguagePart.Kw(CodePart.Literal, "") },
                        { CodePart.Identifier,           LanguagePart.Kw(CodePart.Identifier, "") },
                        { CodePart.BinaryExpression,     LanguagePart.Kw(CodePart.BinaryExpression, "") },
                        { CodePart.UnaryExpression,      LanguagePart.Kw(CodePart.UnaryExpression, "") },
                        { CodePart.AssignmentExpression, LanguagePart.Kw(CodePart.AssignmentExpression, "") },
                        { CodePart.MemberAccessExpression, LanguagePart.Kw(CodePart.MemberAccessExpression, ".") },
                        { CodePart.InvocationExpression, LanguagePart.Kw(CodePart.InvocationExpression, "") },
                        { CodePart.ObjectCreationExpression, LanguagePart.Tmpl(CodePart.ObjectCreationExpression, "new", "new {0}({2})") },
                        { CodePart.CastExpression,       LanguagePart.Tmpl(CodePart.CastExpression, "", "({0}) {1}") },
                        { CodePart.IsTypeExpression,     LanguagePart.Tmpl(CodePart.IsTypeExpression, "instanceof", "{1} instanceof {0}") },
                        { CodePart.ParenthesizedExpression, LanguagePart.Kw(CodePart.ParenthesizedExpression, "") },
                        { CodePart.TernaryExpression,    LanguagePart.Tmpl(CodePart.TernaryExpression, "", "{0} ? {1} : {2}") },
                        { CodePart.RangeExpression,      LanguagePart.Kw(CodePart.RangeExpression, "") },  // IntStream.range
                        { CodePart.LambdaExpression,     LanguagePart.Tmpl(CodePart.LambdaExpression, "->", "({0}) -> {1}") },
                        { CodePart.AwaitExpression,      LanguagePart.Tmpl(CodePart.AwaitExpression, "", ".thenApply()") },
                        { CodePart.ArrayCreationExpression, LanguagePart.Tmpl(CodePart.ArrayCreationExpression, "new", "new {0}[{1}]") },
                        { CodePart.TupleExpression,      LanguagePart.Kw(CodePart.TupleExpression, "") },  // no tuples

                        // Type decorations
                        { CodePart.PointerDecoration,    LanguagePart.Kw(CodePart.PointerDecoration, "") },  // no pointers
                        { CodePart.ReferenceDecoration,  LanguagePart.Kw(CodePart.ReferenceDecoration, "") },  // no references
                        { CodePart.NakedDecoration,      LanguagePart.Kw(CodePart.NakedDecoration, "") },

                        // Special
                        { CodePart.FriendDeclaration,    LanguagePart.Kw(CodePart.FriendDeclaration, "") },  // no equivalent
                        { CodePart.ConstructorInitializer, LanguagePart.Tmpl(CodePart.ConstructorInitializer, "super", "super({1})") },
                        { CodePart.DestructorCall,       LanguagePart.Kw(CodePart.DestructorCall, "") },  // no dtor
                        { CodePart.NewObject,            LanguagePart.Tmpl(CodePart.NewObject, "new", "new {0}({1})") },
                        { CodePart.NullLiteral,          LanguagePart.Kw(CodePart.NullLiteral, "null") },
                        { CodePart.ThisReference,        LanguagePart.Kw(CodePart.ThisReference, "this") },
                        { CodePart.BaseReference,        LanguagePart.Kw(CodePart.BaseReference, "super") },
                        { CodePart.StringInterpolation, LanguagePart.Tmpl(CodePart.StringInterpolation, "", "String.format({0})") },
                        { CodePart.ArrayIndexer,         LanguagePart.Tmpl(CodePart.ArrayIndexer, "[]", "{0}[{1}]") },
                    }
                }
            },

            // ═══════════════════════════════════════════════════════════════
            //  TypeScript
            // ═══════════════════════════════════════════════════════════════
            {
                Language.TypeScript, new LanguageProfile
                {
                    Name                 = Language.TypeScript,
                    FileExtension        = ".ts",
                    LineComment          = "//",
                    BlockCommentStart    = "/*",
                    BlockCommentEnd      = "*/",
                    StringDelimiter      = "\"",
                    CharDelimiter        = "'",
                    StatementTerminator  = ";",
                    NamespaceSeparator  = ".",
                    MemberAccessDot      = ".",
                    MemberAccessArrow    = ".",
                    MemberAccessScope    = ".",
                    InheritanceMarker    = "extends",
                    AccessPublic         = "public",
                    AccessPrivate        = "private",
                    AccessProtected      = "protected",
                    NullLiteral          = "null",
                    ThisReference        = "this",
                    BaseReference        = "super",
                    Parts = new Dictionary<CodePart, LanguagePart>
                    {
                        // Structural
                        { CodePart.CompilationUnit,     LanguagePart.Kw(CodePart.CompilationUnit, "") },
                        { CodePart.Namespace,            LanguagePart.Kw(CodePart.Namespace, "namespace") },
                        { CodePart.UsingDirective,       LanguagePart.Kw(CodePart.UsingDirective, "import") },
                        { CodePart.FileScopeDirective,   LanguagePart.Kw(CodePart.FileScopeDirective, "import") },

                        // Type declarations
                        { CodePart.Class,                LanguagePart.Tmpl(CodePart.Class, "class", "class {0}") },
                        { CodePart.Struct,               LanguagePart.Kw(CodePart.Struct, "") },  // no struct
                        { CodePart.Interface,            LanguagePart.Tmpl(CodePart.Interface, "interface", "interface {0}") },
                        { CodePart.Enum,                 LanguagePart.Tmpl(CodePart.Enum, "enum", "enum {0}") },
                        { CodePart.SumType,              LanguagePart.Tmpl(CodePart.SumType, "type", "type {0} = ...") },
                        { CodePart.Mixin,                LanguagePart.Kw(CodePart.Mixin, "") },  // no native mixin
                        { CodePart.TypeAlias,            LanguagePart.Tmpl(CodePart.TypeAlias, "type", "type {0} = {1}") },
                        { CodePart.Delegate,             LanguagePart.Tmpl(CodePart.Delegate, "type", "type {0} = ({2}) => {1}") },

                        // Members
                        { CodePart.Field,                LanguagePart.Tmpl(CodePart.Field, "", "{0}: {1}") },
                        { CodePart.Property,             LanguagePart.Tmpl(CodePart.Property, "get", "get {0}(): {1}") },
                        { CodePart.Method,               LanguagePart.Tmpl(CodePart.Method, "", "{0}({2}): {1}") },
                        { CodePart.Constructor,          LanguagePart.Tmpl(CodePart.Constructor, "constructor", "constructor({2})") },
                        { CodePart.Destructor,           LanguagePart.Kw(CodePart.Destructor, "") },  // no dtor
                        { CodePart.Finalizer,            LanguagePart.Kw(CodePart.Finalizer, "") },  // no finalizer
                        { CodePart.OperatorOverload,     LanguagePart.Kw(CodePart.OperatorOverload, "") },  // limited
                        { CodePart.Indexer,              LanguagePart.Tmpl(CodePart.Indexer, "", "[{2}]: {1}") },
                        { CodePart.Event,                LanguagePart.Kw(CodePart.Event, "") },  // no direct equivalent
                        { CodePart.Constant,             LanguagePart.Tmpl(CodePart.Constant, "const", "const {0}: {1} = {4}") },
                        { CodePart.CompanionObject,      LanguagePart.Kw(CodePart.CompanionObject, "") },  // static members

                        // Access levels
                        { CodePart.AccessPublic,         LanguagePart.Kw(CodePart.AccessPublic, "public") },
                        { CodePart.AccessPrivate,        LanguagePart.Kw(CodePart.AccessPrivate, "private") },
                        { CodePart.AccessProtected,      LanguagePart.Kw(CodePart.AccessProtected, "protected") },
                        { CodePart.AccessInternal,      LanguagePart.Kw(CodePart.AccessInternal, "") },  // no internal

                        // Modifiers
                        { CodePart.StaticModifier,       LanguagePart.Kw(CodePart.StaticModifier, "static") },
                        { CodePart.AbstractModifier,     LanguagePart.Kw(CodePart.AbstractModifier, "abstract") },
                        { CodePart.VirtualModifier,      LanguagePart.Kw(CodePart.VirtualModifier, "") },  // default
                        { CodePart.OverrideModifier,    LanguagePart.Kw(CodePart.OverrideModifier, "") },  // no override keyword
                        { CodePart.SealedModifier,      LanguagePart.Kw(CodePart.SealedModifier, "") },  // no sealed
                        { CodePart.AsyncModifier,       LanguagePart.Kw(CodePart.AsyncModifier, "async") },
                        { CodePart.ConstModifier,        LanguagePart.Kw(CodePart.ConstModifier, "const") },
                        { CodePart.MutableModifier,      LanguagePart.Kw(CodePart.MutableModifier, "let") },
                        { CodePart.VolatileModifier,    LanguagePart.Kw(CodePart.VolatileModifier, "") },  // no volatile
                        { CodePart.ReadOnlyModifier,    LanguagePart.Kw(CodePart.ReadOnlyModifier, "readonly") },
                        { CodePart.ExplicitModifier,    LanguagePart.Kw(CodePart.ExplicitModifier, "") },  // no equivalent
                        { CodePart.NoexceptModifier,    LanguagePart.Kw(CodePart.NoexceptModifier, "") },  // no equivalent
                        { CodePart.InlineModifier,      LanguagePart.Kw(CodePart.InlineModifier, "") },  // no equivalent

                        // Statements
                        { CodePart.IfStatement,          LanguagePart.Tmpl(CodePart.IfStatement, "if", "if ({0})") },
                        { CodePart.ElseIfClause,         LanguagePart.Tmpl(CodePart.ElseIfClause, "else if", "else if ({0})") },
                        { CodePart.ElseClause,           LanguagePart.Kw(CodePart.ElseClause, "else") },
                        { CodePart.SwitchStatement,      LanguagePart.Tmpl(CodePart.SwitchStatement, "switch", "switch ({0})") },
                        { CodePart.MatchStatement,       LanguagePart.Tmpl(CodePart.MatchStatement, "switch", "switch ({0})") },
                        { CodePart.ForStatement,          LanguagePart.Tmpl(CodePart.ForStatement, "for", "for ({0})") },
                        { CodePart.ForEachStatement,     LanguagePart.Tmpl(CodePart.ForEachStatement, "for", "for (const {0} of {1})") },
                        { CodePart.WhileStatement,       LanguagePart.Tmpl(CodePart.WhileStatement, "while", "while ({0})") },
                        { CodePart.DoWhileStatement,     LanguagePart.Tmpl(CodePart.DoWhileStatement, "do", "do ... while ({0})") },
                        { CodePart.TryStatement,         LanguagePart.Kw(CodePart.TryStatement, "try") },
                        { CodePart.CatchClause,          LanguagePart.Tmpl(CodePart.CatchClause, "catch", "catch ({0})") },
                        { CodePart.FinallyClause,        LanguagePart.Kw(CodePart.FinallyClause, "finally") },
                        { CodePart.ReturnStatement,      LanguagePart.Kw(CodePart.ReturnStatement, "return") },
                        { CodePart.ThrowStatement,       LanguagePart.Kw(CodePart.ThrowStatement, "throw") },
                        { CodePart.YieldStatement,       LanguagePart.Kw(CodePart.YieldStatement, "yield") },
                        { CodePart.BreakStatement,       LanguagePart.Kw(CodePart.BreakStatement, "break") },
                        { CodePart.ContinueStatement,    LanguagePart.Kw(CodePart.ContinueStatement, "continue") },
                        { CodePart.BlockStatement,       LanguagePart.Kw(CodePart.BlockStatement, "") },
                        { CodePart.UsingStatement,       LanguagePart.Tmpl(CodePart.UsingStatement, "using", "using ({0})") },
                        { CodePart.LockStatement,        LanguagePart.Kw(CodePart.LockStatement, "") },  // no lock
                        { CodePart.CheckedStatement,    LanguagePart.Kw(CodePart.CheckedStatement, "") },  // no equivalent

                        // Expressions
                        { CodePart.Literal,              LanguagePart.Kw(CodePart.Literal, "") },
                        { CodePart.Identifier,           LanguagePart.Kw(CodePart.Identifier, "") },
                        { CodePart.BinaryExpression,     LanguagePart.Kw(CodePart.BinaryExpression, "") },
                        { CodePart.UnaryExpression,      LanguagePart.Kw(CodePart.UnaryExpression, "") },
                        { CodePart.AssignmentExpression, LanguagePart.Kw(CodePart.AssignmentExpression, "") },
                        { CodePart.MemberAccessExpression, LanguagePart.Kw(CodePart.MemberAccessExpression, ".") },
                        { CodePart.InvocationExpression, LanguagePart.Kw(CodePart.InvocationExpression, "") },
                        { CodePart.ObjectCreationExpression, LanguagePart.Tmpl(CodePart.ObjectCreationExpression, "new", "new {0}({2})") },
                        { CodePart.CastExpression,       LanguagePart.Tmpl(CodePart.CastExpression, "as", "{1} as {0}") },
                        { CodePart.IsTypeExpression,     LanguagePart.Tmpl(CodePart.IsTypeExpression, "instanceof", "{1} instanceof {0}") },
                        { CodePart.ParenthesizedExpression, LanguagePart.Kw(CodePart.ParenthesizedExpression, "") },
                        { CodePart.TernaryExpression,    LanguagePart.Tmpl(CodePart.TernaryExpression, "", "{0} ? {1} : {2}") },
                        { CodePart.RangeExpression,      LanguagePart.Kw(CodePart.RangeExpression, "") },  // no native range
                        { CodePart.LambdaExpression,     LanguagePart.Tmpl(CodePart.LambdaExpression, "=>", "({0}) => {1}") },
                        { CodePart.AwaitExpression,      LanguagePart.Kw(CodePart.AwaitExpression, "await") },
                        { CodePart.ArrayCreationExpression, LanguagePart.Tmpl(CodePart.ArrayCreationExpression, "", "[{0}]") },
                        { CodePart.TupleExpression,      LanguagePart.Tmpl(CodePart.TupleExpression, "", "[{0}]") },

                        // Type decorations
                        { CodePart.PointerDecoration,    LanguagePart.Kw(CodePart.PointerDecoration, "") },  // no pointers
                        { CodePart.ReferenceDecoration,  LanguagePart.Kw(CodePart.ReferenceDecoration, "") },  // no references
                        { CodePart.NakedDecoration,      LanguagePart.Kw(CodePart.NakedDecoration, "") },

                        // Special
                        { CodePart.FriendDeclaration,    LanguagePart.Kw(CodePart.FriendDeclaration, "") },  // no equivalent
                        { CodePart.ConstructorInitializer, LanguagePart.Tmpl(CodePart.ConstructorInitializer, "super", "super({1})") },
                        { CodePart.DestructorCall,       LanguagePart.Kw(CodePart.DestructorCall, "") },  // no dtor
                        { CodePart.NewObject,            LanguagePart.Tmpl(CodePart.NewObject, "new", "new {0}({1})") },
                        { CodePart.NullLiteral,          LanguagePart.Kw(CodePart.NullLiteral, "null") },
                        { CodePart.ThisReference,        LanguagePart.Kw(CodePart.ThisReference, "this") },
                        { CodePart.BaseReference,        LanguagePart.Kw(CodePart.BaseReference, "super") },
                        { CodePart.StringInterpolation, LanguagePart.Tmpl(CodePart.StringInterpolation, "`", "`{0}`") },
                        { CodePart.ArrayIndexer,         LanguagePart.Tmpl(CodePart.ArrayIndexer, "[]", "{0}[{1}]") },
                    }
                }
            },

            // ═══════════════════════════════════════════════════════════════
            //  Go
            // ═══════════════════════════════════════════════════════════════
            {
                Language.Go, new LanguageProfile
                {
                    Name                 = Language.Go,
                    FileExtension        = ".go",
                    LineComment          = "//",
                    BlockCommentStart    = "/*",
                    BlockCommentEnd      = "*/",
                    StringDelimiter      = "\"",
                    CharDelimiter        = "'",
                    StatementTerminator  = "",
                    NamespaceSeparator  = ".",
                    MemberAccessDot      = ".",
                    MemberAccessArrow    = ".",
                    MemberAccessScope    = ".",
                    InheritanceMarker    = "",
                    AccessPublic         = "",  // uppercase = public
                    AccessPrivate        = "",  // lowercase = private
                    AccessProtected      = "",
                    NullLiteral          = "nil",
                    ThisReference        = "",
                    BaseReference        = "",
                    Parts = new Dictionary<CodePart, LanguagePart>
                    {
                        // Structural
                        { CodePart.CompilationUnit,     LanguagePart.Kw(CodePart.CompilationUnit, "") },
                        { CodePart.Namespace,            LanguagePart.Kw(CodePart.Namespace, "package") },
                        { CodePart.UsingDirective,       LanguagePart.Kw(CodePart.UsingDirective, "import") },
                        { CodePart.FileScopeDirective,   LanguagePart.Kw(CodePart.FileScopeDirective, "import") },

                        // Type declarations
                        { CodePart.Class,                LanguagePart.Tmpl(CodePart.Class, "struct", "type {0} struct") },
                        { CodePart.Struct,               LanguagePart.Tmpl(CodePart.Struct, "struct", "type {0} struct") },
                        { CodePart.Interface,            LanguagePart.Tmpl(CodePart.Interface, "interface", "type {0} interface") },
                        { CodePart.Enum,                 LanguagePart.Tmpl(CodePart.Enum, "iota", "type {0} int") },
                        { CodePart.SumType,              LanguagePart.Tmpl(CodePart.SumType, "interface", "type {0} interface") },
                        { CodePart.Mixin,                LanguagePart.Kw(CodePart.Mixin, "") },  // no native mixin
                        { CodePart.TypeAlias,            LanguagePart.Tmpl(CodePart.TypeAlias, "type", "type {0} = {1}") },
                        { CodePart.Delegate,             LanguagePart.Tmpl(CodePart.Delegate, "func", "type {0} = func({2}) {1}") },

                        // Members
                        { CodePart.Field,                LanguagePart.Tmpl(CodePart.Field, "", "{0} {1}") },
                        { CodePart.Property,             LanguagePart.Kw(CodePart.Property, "") },  // no properties
                        { CodePart.Method,               LanguagePart.Tmpl(CodePart.Method, "func", "func ({0}) {1}({2}) {3}") },
                        { CodePart.Constructor,          LanguagePart.Tmpl(CodePart.Constructor, "func", "func New{0}({2}) *{0}") },
                        { CodePart.Destructor,           LanguagePart.Kw(CodePart.Destructor, "") },  // no dtor, GC
                        { CodePart.Finalizer,            LanguagePart.Kw(CodePart.Finalizer, "") },  // no finalizer
                        { CodePart.OperatorOverload,     LanguagePart.Kw(CodePart.OperatorOverload, "") },  // no overloading
                        { CodePart.Indexer,              LanguagePart.Kw(CodePart.Indexer, "") },  // no indexer
                        { CodePart.Event,                LanguagePart.Kw(CodePart.Event, "") },  // no direct equivalent
                        { CodePart.Constant,             LanguagePart.Tmpl(CodePart.Constant, "const", "const {0} {1} = {4}") },
                        { CodePart.CompanionObject,      LanguagePart.Kw(CodePart.CompanionObject, "") },  // package-level funcs

                        // Access levels
                        { CodePart.AccessPublic,         LanguagePart.Kw(CodePart.AccessPublic, "") },  // uppercase
                        { CodePart.AccessPrivate,        LanguagePart.Kw(CodePart.AccessPrivate, "") },  // lowercase
                        { CodePart.AccessProtected,      LanguagePart.Kw(CodePart.AccessProtected, "") },  // no protected
                        { CodePart.AccessInternal,      LanguagePart.Kw(CodePart.AccessInternal, "") },  // no internal

                        // Modifiers
                        { CodePart.StaticModifier,       LanguagePart.Kw(CodePart.StaticModifier, "") },  // package-level
                        { CodePart.AbstractModifier,     LanguagePart.Kw(CodePart.AbstractModifier, "") },  // interface methods
                        { CodePart.VirtualModifier,      LanguagePart.Kw(CodePart.VirtualModifier, "") },  // all methods virtual
                        { CodePart.OverrideModifier,    LanguagePart.Kw(CodePart.OverrideModifier, "") },  // no override
                        { CodePart.SealedModifier,      LanguagePart.Kw(CodePart.SealedModifier, "") },  // no sealed
                        { CodePart.AsyncModifier,       LanguagePart.Kw(CodePart.AsyncModifier, "go") },
                        { CodePart.ConstModifier,        LanguagePart.Kw(CodePart.ConstModifier, "const") },
                        { CodePart.MutableModifier,      LanguagePart.Kw(CodePart.MutableModifier, "") },  // no mutable
                        { CodePart.VolatileModifier,    LanguagePart.Kw(CodePart.VolatileModifier, "") },  // no volatile
                        { CodePart.ReadOnlyModifier,    LanguagePart.Kw(CodePart.ReadOnlyModifier, "") },  // no readonly
                        { CodePart.ExplicitModifier,    LanguagePart.Kw(CodePart.ExplicitModifier, "") },  // no equivalent
                        { CodePart.NoexceptModifier,    LanguagePart.Kw(CodePart.NoexceptModifier, "") },  // no equivalent
                        { CodePart.InlineModifier,      LanguagePart.Kw(CodePart.InlineModifier, "") },  // no equivalent

                        // Statements
                        { CodePart.IfStatement,          LanguagePart.Tmpl(CodePart.IfStatement, "if", "if {0}") },
                        { CodePart.ElseIfClause,         LanguagePart.Tmpl(CodePart.ElseIfClause, "else if", "else if {0}") },
                        { CodePart.ElseClause,           LanguagePart.Kw(CodePart.ElseClause, "else") },
                        { CodePart.SwitchStatement,      LanguagePart.Tmpl(CodePart.SwitchStatement, "switch", "switch {0}") },
                        { CodePart.MatchStatement,       LanguagePart.Tmpl(CodePart.MatchStatement, "switch", "switch {0}") },
                        { CodePart.ForStatement,          LanguagePart.Tmpl(CodePart.ForStatement, "for", "for {0}") },
                        { CodePart.ForEachStatement,     LanguagePart.Tmpl(CodePart.ForEachStatement, "for", "for {0} := range {1}") },
                        { CodePart.WhileStatement,       LanguagePart.Tmpl(CodePart.WhileStatement, "for", "for {0}") },
                        { CodePart.DoWhileStatement,     LanguagePart.Tmpl(CodePart.DoWhileStatement, "for", "for {0}") },  // no do-while
                        { CodePart.TryStatement,         LanguagePart.Kw(CodePart.TryStatement, "") },  // defer + panic/recover
                        { CodePart.CatchClause,          LanguagePart.Kw(CodePart.CatchClause, "") },  // recover
                        { CodePart.FinallyClause,        LanguagePart.Kw(CodePart.FinallyClause, "defer") },
                        { CodePart.ReturnStatement,      LanguagePart.Kw(CodePart.ReturnStatement, "return") },
                        { CodePart.ThrowStatement,       LanguagePart.Kw(CodePart.ThrowStatement, "panic") },
                        { CodePart.YieldStatement,       LanguagePart.Kw(CodePart.YieldStatement, "yield") },  // 1.22+
                        { CodePart.BreakStatement,       LanguagePart.Kw(CodePart.BreakStatement, "break") },
                        { CodePart.ContinueStatement,    LanguagePart.Kw(CodePart.ContinueStatement, "continue") },
                        { CodePart.BlockStatement,       LanguagePart.Kw(CodePart.BlockStatement, "") },
                        { CodePart.UsingStatement,       LanguagePart.Kw(CodePart.UsingStatement, "") },  // defer
                        { CodePart.LockStatement,        LanguagePart.Tmpl(CodePart.LockStatement, "sync.Mutex", "mu.Lock()") },
                        { CodePart.CheckedStatement,    LanguagePart.Kw(CodePart.CheckedStatement, "") },  // no equivalent

                        // Expressions
                        { CodePart.Literal,              LanguagePart.Kw(CodePart.Literal, "") },
                        { CodePart.Identifier,           LanguagePart.Kw(CodePart.Identifier, "") },
                        { CodePart.BinaryExpression,     LanguagePart.Kw(CodePart.BinaryExpression, "") },
                        { CodePart.UnaryExpression,      LanguagePart.Kw(CodePart.UnaryExpression, "") },
                        { CodePart.AssignmentExpression, LanguagePart.Kw(CodePart.AssignmentExpression, "") },
                        { CodePart.MemberAccessExpression, LanguagePart.Kw(CodePart.MemberAccessExpression, ".") },
                        { CodePart.InvocationExpression, LanguagePart.Kw(CodePart.InvocationExpression, "") },
                        { CodePart.ObjectCreationExpression, LanguagePart.Tmpl(CodePart.ObjectCreationExpression, "", "&{0}{{}}") },
                        { CodePart.CastExpression,       LanguagePart.Tmpl(CodePart.CastExpression, "", "{0}({1})") },
                        { CodePart.IsTypeExpression,     LanguagePart.Tmpl(CodePart.IsTypeExpression, "", "_, ok := {1}.({0})") },
                        { CodePart.ParenthesizedExpression, LanguagePart.Kw(CodePart.ParenthesizedExpression, "") },
                        { CodePart.TernaryExpression,    LanguagePart.Kw(CodePart.TernaryExpression, "") },  // no ternary
                        { CodePart.RangeExpression,      LanguagePart.Kw(CodePart.RangeExpression, "") },  // no range
                        { CodePart.LambdaExpression,     LanguagePart.Tmpl(CodePart.LambdaExpression, "func", "func({0}) {1} {{ {2} }}") },
                        { CodePart.AwaitExpression,      LanguagePart.Kw(CodePart.AwaitExpression, "<-") },  // channel receive
                        { CodePart.ArrayCreationExpression, LanguagePart.Tmpl(CodePart.ArrayCreationExpression, "", "[]{0}{{}}") },
                        { CodePart.TupleExpression,      LanguagePart.Kw(CodePart.TupleExpression, "") },  // no tuples (1.18+)

                        // Type decorations
                        { CodePart.PointerDecoration,    LanguagePart.Tmpl(CodePart.PointerDecoration, "*", "*{0}") },
                        { CodePart.ReferenceDecoration,  LanguagePart.Kw(CodePart.ReferenceDecoration, "") },  // no references
                        { CodePart.NakedDecoration,      LanguagePart.Kw(CodePart.NakedDecoration, "") },

                        // Special
                        { CodePart.FriendDeclaration,    LanguagePart.Kw(CodePart.FriendDeclaration, "") },  // no equivalent
                        { CodePart.ConstructorInitializer, LanguagePart.Kw(CodePart.ConstructorInitializer, "") },
                        { CodePart.DestructorCall,       LanguagePart.Kw(CodePart.DestructorCall, "") },  // no dtor
                        { CodePart.NewObject,            LanguagePart.Tmpl(CodePart.NewObject, "", "&{0}{{}}") },
                        { CodePart.NullLiteral,          LanguagePart.Kw(CodePart.NullLiteral, "nil") },
                        { CodePart.ThisReference,        LanguagePart.Kw(CodePart.ThisReference, "") },  // receiver name
                        { CodePart.BaseReference,        LanguagePart.Kw(CodePart.BaseReference, "") },  // no base
                        { CodePart.StringInterpolation, LanguagePart.Tmpl(CodePart.StringInterpolation, "", "fmt.Sprintf({0})") },
                        { CodePart.ArrayIndexer,         LanguagePart.Tmpl(CodePart.ArrayIndexer, "[]", "{0}[{1}]") },
                    }
                }
            },

            // ═══════════════════════════════════════════════════════════════
            //  Zig
            // ═══════════════════════════════════════════════════════════════
            {
                Language.Zig, new LanguageProfile
                {
                    Name                 = Language.Zig,
                    FileExtension        = ".zig",
                    LineComment          = "//",
                    BlockCommentStart    = "/*",
                    BlockCommentEnd      = "*/",
                    StringDelimiter      = "\"",
                    CharDelimiter        = "'",
                    StatementTerminator  = "",
                    NamespaceSeparator  = ".",
                    MemberAccessDot      = ".",
                    MemberAccessArrow    = ".",
                    MemberAccessScope    = ".",
                    InheritanceMarker    = "",
                    AccessPublic         = "pub",
                    AccessPrivate        = "",
                    AccessProtected      = "",
                    NullLiteral          = "null",
                    ThisReference        = "self",
                    BaseReference        = "",
                    Parts = new Dictionary<CodePart, LanguagePart>
                    {
                        // Structural
                        { CodePart.CompilationUnit,     LanguagePart.Kw(CodePart.CompilationUnit, "") },
                        { CodePart.Namespace,            LanguagePart.Kw(CodePart.Namespace, "") },  // no namespaces
                        { CodePart.UsingDirective,       LanguagePart.Kw(CodePart.UsingDirective, "const") },
                        { CodePart.FileScopeDirective,   LanguagePart.Kw(CodePart.FileScopeDirective, "const") },

                        // Type declarations
                        { CodePart.Class,                LanguagePart.Kw(CodePart.Class, "") },  // no classes
                        { CodePart.Struct,               LanguagePart.Tmpl(CodePart.Struct, "struct", "const {0} = struct") },
                        { CodePart.Interface,            LanguagePart.Kw(CodePart.Interface, "") },  // no interfaces
                        { CodePart.Enum,                 LanguagePart.Tmpl(CodePart.Enum, "enum", "const {0} = enum") },
                        { CodePart.SumType,              LanguagePart.Tmpl(CodePart.SumType, "enum", "const {0} = enum") },
                        { CodePart.Mixin,                LanguagePart.Kw(CodePart.Mixin, "") },  // no mixin
                        { CodePart.TypeAlias,            LanguagePart.Tmpl(CodePart.TypeAlias, "const", "const {0} = {1}") },
                        { CodePart.Delegate,             LanguagePart.Tmpl(CodePart.Delegate, "fn", "const {0} = *const fn({2}) {1}") },

                        // Members
                        { CodePart.Field,                LanguagePart.Tmpl(CodePart.Field, "", "{0}: {1}") },
                        { CodePart.Property,             LanguagePart.Kw(CodePart.Property, "") },  // no properties
                        { CodePart.Method,               LanguagePart.Tmpl(CodePart.Method, "fn", "fn {0}({2}) {1}") },
                        { CodePart.Constructor,          LanguagePart.Tmpl(CodePart.Constructor, "init", "fn init({2}) Self") },
                        { CodePart.Destructor,           LanguagePart.Tmpl(CodePart.Destructor, "deinit", "fn deinit(self: *Self)") },
                        { CodePart.Finalizer,            LanguagePart.Kw(CodePart.Finalizer, "") },  // no finalizer
                        { CodePart.OperatorOverload,     LanguagePart.Kw(CodePart.OperatorOverload, "") },  // limited
                        { CodePart.Indexer,              LanguagePart.Kw(CodePart.Indexer, "") },  // no indexer
                        { CodePart.Event,                LanguagePart.Kw(CodePart.Event, "") },  // no direct equivalent
                        { CodePart.Constant,             LanguagePart.Tmpl(CodePart.Constant, "const", "const {0}: {1} = {4}") },
                        { CodePart.CompanionObject,      LanguagePart.Kw(CodePart.CompanionObject, "") },  // top-level decls

                        // Access levels
                        { CodePart.AccessPublic,         LanguagePart.Kw(CodePart.AccessPublic, "pub") },
                        { CodePart.AccessPrivate,        LanguagePart.Kw(CodePart.AccessPrivate, "") },  // default
                        { CodePart.AccessProtected,      LanguagePart.Kw(CodePart.AccessProtected, "") },  // no protected
                        { CodePart.AccessInternal,      LanguagePart.Kw(CodePart.AccessInternal, "") },  // no internal

                        // Modifiers
                        { CodePart.StaticModifier,       LanguagePart.Kw(CodePart.StaticModifier, "") },  // top-level = static
                        { CodePart.AbstractModifier,     LanguagePart.Kw(CodePart.AbstractModifier, "") },  // no abstract
                        { CodePart.VirtualModifier,      LanguagePart.Kw(CodePart.VirtualModifier, "") },  // no virtual
                        { CodePart.OverrideModifier,    LanguagePart.Kw(CodePart.OverrideModifier, "") },  // no override
                        { CodePart.SealedModifier,      LanguagePart.Kw(CodePart.SealedModifier, "") },  // no sealed
                        { CodePart.AsyncModifier,       LanguagePart.Tmpl(CodePart.AsyncModifier, "async", "async fn") },
                        { CodePart.ConstModifier,        LanguagePart.Kw(CodePart.ConstModifier, "const") },
                        { CodePart.MutableModifier,      LanguagePart.Kw(CodePart.MutableModifier, "var") },
                        { CodePart.VolatileModifier,    LanguagePart.Kw(CodePart.VolatileModifier, "") },  // no volatile
                        { CodePart.ReadOnlyModifier,    LanguagePart.Kw(CodePart.ReadOnlyModifier, "const") },
                        { CodePart.ExplicitModifier,    LanguagePart.Kw(CodePart.ExplicitModifier, "") },  // no equivalent
                        { CodePart.NoexceptModifier,    LanguagePart.Kw(CodePart.NoexceptModifier, "") },  // no equivalent
                        { CodePart.InlineModifier,      LanguagePart.Tmpl(CodePart.InlineModifier, "inline", "inline fn") },

                        // Statements
                        { CodePart.IfStatement,          LanguagePart.Tmpl(CodePart.IfStatement, "if", "if ({0})") },
                        { CodePart.ElseIfClause,         LanguagePart.Tmpl(CodePart.ElseIfClause, "else if", "else if ({0})") },
                        { CodePart.ElseClause,           LanguagePart.Kw(CodePart.ElseClause, "else") },
                        { CodePart.SwitchStatement,      LanguagePart.Tmpl(CodePart.SwitchStatement, "switch", "switch ({0})") },
                        { CodePart.MatchStatement,       LanguagePart.Tmpl(CodePart.MatchStatement, "switch", "switch ({0})") },
                        { CodePart.ForStatement,          LanguagePart.Tmpl(CodePart.ForStatement, "for", "for ({0})") },
                        { CodePart.ForEachStatement,     LanguagePart.Tmpl(CodePart.ForEachStatement, "for", "for ({0})") },
                        { CodePart.WhileStatement,       LanguagePart.Tmpl(CodePart.WhileStatement, "while", "while ({0})") },
                        { CodePart.DoWhileStatement,     LanguagePart.Tmpl(CodePart.DoWhileStatement, "while", "while ({0})") },  // no do-while
                        { CodePart.TryStatement,         LanguagePart.Kw(CodePart.TryStatement, "") },  // error unions
                        { CodePart.CatchClause,          LanguagePart.Tmpl(CodePart.CatchClause, "catch", "catch {0}") },
                        { CodePart.FinallyClause,        LanguagePart.Kw(CodePart.FinallyClause, "") },  // errdefer
                        { CodePart.ReturnStatement,      LanguagePart.Kw(CodePart.ReturnStatement, "return") },
                        { CodePart.ThrowStatement,       LanguagePart.Kw(CodePart.ThrowStatement, "return error") },
                        { CodePart.YieldStatement,       LanguagePart.Kw(CodePart.YieldStatement, "") },  // no yield
                        { CodePart.BreakStatement,       LanguagePart.Kw(CodePart.BreakStatement, "break") },
                        { CodePart.ContinueStatement,    LanguagePart.Kw(CodePart.ContinueStatement, "continue") },
                        { CodePart.BlockStatement,       LanguagePart.Kw(CodePart.BlockStatement, "") },
                        { CodePart.UsingStatement,       LanguagePart.Kw(CodePart.UsingStatement, "") },  // defer
                        { CodePart.LockStatement,        LanguagePart.Kw(CodePart.LockStatement, "") },  // std.Thread.Mutex
                        { CodePart.CheckedStatement,    LanguagePart.Kw(CodePart.CheckedStatement, "") },  // no equivalent

                        // Expressions
                        { CodePart.Literal,              LanguagePart.Kw(CodePart.Literal, "") },
                        { CodePart.Identifier,           LanguagePart.Kw(CodePart.Identifier, "") },
                        { CodePart.BinaryExpression,     LanguagePart.Kw(CodePart.BinaryExpression, "") },
                        { CodePart.UnaryExpression,      LanguagePart.Kw(CodePart.UnaryExpression, "") },
                        { CodePart.AssignmentExpression, LanguagePart.Kw(CodePart.AssignmentExpression, "") },
                        { CodePart.MemberAccessExpression, LanguagePart.Kw(CodePart.MemberAccessExpression, ".") },
                        { CodePart.InvocationExpression, LanguagePart.Kw(CodePart.InvocationExpression, "") },
                        { CodePart.ObjectCreationExpression, LanguagePart.Tmpl(CodePart.ObjectCreationExpression, "", "{0}.init({2})") },
                        { CodePart.CastExpression,       LanguagePart.Tmpl(CodePart.CastExpression, "", "@as({0}, {1})") },
                        { CodePart.IsTypeExpression,     LanguagePart.Kw(CodePart.IsTypeExpression, "") },  // no is
                        { CodePart.ParenthesizedExpression, LanguagePart.Kw(CodePart.ParenthesizedExpression, "") },
                        { CodePart.TernaryExpression,    LanguagePart.Kw(CodePart.TernaryExpression, "") },  // if/else expression
                        { CodePart.RangeExpression,      LanguagePart.Tmpl(CodePart.RangeExpression, "..", "{0}..{1}") },
                        { CodePart.LambdaExpression,     LanguagePart.Kw(CodePart.LambdaExpression, "") },  // no lambda, use struct
                        { CodePart.AwaitExpression,      LanguagePart.Kw(CodePart.AwaitExpression, "await") },
                        { CodePart.ArrayCreationExpression, LanguagePart.Tmpl(CodePart.ArrayCreationExpression, "", "&.{0}") },
                        { CodePart.TupleExpression,      LanguagePart.Kw(CodePart.TupleExpression, "") },  // no tuples

                        // Type decorations
                        { CodePart.PointerDecoration,    LanguagePart.Tmpl(CodePart.PointerDecoration, "*", "*{0}") },
                        { CodePart.ReferenceDecoration,  LanguagePart.Kw(CodePart.ReferenceDecoration, "") },  // no references
                        { CodePart.NakedDecoration,      LanguagePart.Kw(CodePart.NakedDecoration, "") },

                        // Special
                        { CodePart.FriendDeclaration,    LanguagePart.Kw(CodePart.FriendDeclaration, "") },  // no equivalent
                        { CodePart.ConstructorInitializer, LanguagePart.Kw(CodePart.ConstructorInitializer, "") },
                        { CodePart.DestructorCall,       LanguagePart.Kw(CodePart.DestructorCall, "deinit") },
                        { CodePart.NewObject,            LanguagePart.Tmpl(CodePart.NewObject, "", "try {0}.create({1})") },
                        { CodePart.NullLiteral,          LanguagePart.Kw(CodePart.NullLiteral, "null") },
                        { CodePart.ThisReference,        LanguagePart.Kw(CodePart.ThisReference, "self") },
                        { CodePart.BaseReference,        LanguagePart.Kw(CodePart.BaseReference, "") },  // no base
                        { CodePart.StringInterpolation, LanguagePart.Kw(CodePart.StringInterpolation, "") },  // std.fmt
                        { CodePart.ArrayIndexer,         LanguagePart.Tmpl(CodePart.ArrayIndexer, "[]", "{0}[{1}]") },
                    }
                }
            },
        };
    }
}