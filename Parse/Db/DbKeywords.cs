using System.Collections.Generic;

namespace taste.Parse
{
    /// <summary>
    /// The complete keyword registry for the Db language.
    /// Every token the parser recognizes as a structural keyword is defined here.
    /// This is the single source of truth for what each piece of Db syntax *means* —
    /// the parser consults this table to decode source text into AST nodes.
    /// </summary>
    public static class DbKeywords
    {
        // ── Type declaration keywords ─────────────────────────────────────────

        /// <summary>class — declares a reference type</summary>
        public const string Class = "class";

        /// <summary>struct — declares a value type</summary>
        public const string Struct = "struct";

        /// <summary>interface — declares a contract type</summary>
        public const string Interface = "interface";

        /// <summary>enum — declares a simple enumeration</summary>
        public const string Enum = "enum";

        /// <summary>sum — declares a discriminated union (sum type)</summary>
        public const string Sum = "sum";

        /// <summary>type — declares a type alias</summary>
        public const string Type = "type";

        /// <summary>delegate — declares a function type</summary>
        public const string Delegate = "delegate";

        /// <summary>stub — declares an IntelliSense-only stub declaration</summary>
        public const string Stub = "stub";

        // ── Access modifier keywords ───────────────────────────────────────────

        /// <summary>public — visible everywhere</summary>
        public const string Public = "public";

        /// <summary>private — visible only in declaring type</summary>
        public const string Private = "private";

        /// <summary>protected — visible in declaring type and derived types</summary>
        public const string Protected = "protected";

        /// <summary>internal — visible within the same assembly</summary>
        public const string Internal = "internal";

        // ── Modifier keywords ──────────────────────────────────────────────────

        /// <summary>static — belongs to the type, not an instance</summary>
        public const string Static = "static";

        /// <summary>sealed — prevents further inheritance/override</summary>
        public const string Sealed = "sealed";

        /// <summary>abstract — must be overridden in derived types</summary>
        public const string Abstract = "abstract";

        /// <summary>virtual — can be overridden in derived types</summary>
        public const string Virtual = "virtual";

        /// <summary>override — replaces a virtual/abstract member</summary>
        public const string Override = "override";

        /// <summary>new — hides a base member (name hiding)</summary>
        public const string New = "new";

        /// <summary>async — marks a method as asynchronous (coroutine)</summary>
        public const string Async = "async";

        /// <summary>readonly — can only be assigned in the constructor</summary>
        public const string Readonly = "readonly";

        /// <summary>partial — spans multiple source files</summary>
        public const string Partial = "partial";

        // ── Parameter modifier keywords ────────────────────────────────────────

        /// <summary>ref — pass by reference (caller must initialize)</summary>
        public const string Ref = "ref";

        /// <summary>out — pass by reference (callee initializes)</summary>
        public const string Out = "out";

        /// <summary>in — readonly reference parameter, or iteration target in foreach</summary>
        public const string In = "in";

        /// <summary>params — variadic parameter (last param, array type)</summary>
        public const string Params = "params";

        /// <summary>this — extension method receiver (first param only)</summary>
        public const string This = "this";

        /// <summary>var — implicit type declaration for local variables</summary>
        public const string Var = "var";

        // ── Control flow keywords ──────────────────────────────────────────────

        /// <summary>if — conditional branch</summary>
        public const string If = "if";

        /// <summary>else — alternative branch</summary>
        public const string Else = "else";

        /// <summary>unless — inverted conditional (if not)</summary>
        public const string Unless = "unless";

        /// <summary>while — pre-tested loop</summary>
        public const string While = "while";

        /// <summary>do — post-tested loop (do..while)</summary>
        public const string Do = "do";

        /// <summary>for — counter-based loop</summary>
        public const string For = "for";

        /// <summary>foreach — iteration loop</summary>
        public const string Foreach = "foreach";

        /// <summary>repeat — counted or conditional loop (Db-specific)</summary>
        public const string Repeat = "repeat";

        /// <summary>until — loop condition for repeat-until (Db-specific)</summary>
        public const string Until = "until";

        /// <summary>switch — multi-branch selection</summary>
        public const string Switch = "switch";

        /// <summary>try — exception handling block</summary>
        public const string Try = "try";

        /// <summary>catch — exception handler clause</summary>
        public const string Catch = "catch";

        /// <summary>finally — always-executed cleanup clause</summary>
        public const string Finally = "finally";

        /// <summary>return — exit method with value</summary>
        public const string Return = "return";

        /// <summary>break — exit loop early</summary>
        public const string Break = "break";

        /// <summary>continue — skip to next iteration</summary>
        public const string Continue = "continue";

        /// <summary>throw — raise an exception</summary>
        public const string Throw = "throw";

        /// <summary>yield — produce a value for iteration</summary>
        public const string Yield = "yield";

        // ── Db-specific operator keywords ──────────────────────────────────────

        /// <summary>defer — schedule cleanup at scope exit (Db-specific)</summary>
        public const string Defer = "defer";

        /// <summary>log — emit a log message to a stream (Db-specific)</summary>
        public const string Log = "log";

        /// <summary>swap — exchange two values (Db-specific)</summary>
        public const string Swap = "swap";

        /// <summary>using — RAII scope (deterministic disposal)</summary>
        public const string Using = "using";

        /// <summary>lock — mutual exclusion block</summary>
        public const string Lock = "lock";

        /// <summary>checked — overflow-checking context</summary>
        public const string Checked = "checked";

        // ── Db-specific operator symbols ───────────────────────────────────────

        /// <summary>&lt;- — move semantics operator (transfers ownership)</summary>
        public const string MoveArrow = "<-";

        /// <summary>&lt;-&gt; — swap operator (exchanges two values)</summary>
        public const string SwapArrow = "<->";

        /// <summary>` — single-line defer operator (backtick)</summary>
        public const string DeferTick = "`";

        /// <summary>`` — block defer operator (double backtick)</summary>
        public const string DeferBlockTick = "``";

        // ── Namespace / import keywords ────────────────────────────────────────

        /// <summary>namespace — declares a namespace scope</summary>
        public const string Namespace = "namespace";

        /// <summary>using — imports a namespace or header</summary>
        public const string UsingKeyword = "using";

        // ── Lookup tables ──────────────────────────────────────────────────────

        /// <summary>All access modifier keywords.</summary>
        public static readonly HashSet<string> AccessModifiers = new()
        {
            Public, Private, Protected, Internal
        };

        /// <summary>All method modifier keywords.</summary>
        public static readonly HashSet<string> MethodModifiers = new()
        {
            Static, Sealed, Abstract, Virtual, Override, New, Async
        };

        /// <summary>All class-level modifier keywords.</summary>
        public static readonly HashSet<string> ClassModifiers = new()
        {
            Static, Sealed, Abstract, Partial
        };

        /// <summary>All parameter modifier keywords.</summary>
        public static readonly HashSet<string> ParameterModifiers = new()
        {
            Ref, Out, In, Params, This
        };

        /// <summary>All control flow keywords that start a block.</summary>
        public static readonly HashSet<string> BlockKeywords = new()
        {
            If, Unless, While, Do, For, Foreach, Repeat, Switch, Try, Using, Lock, Checked
        };

        /// <summary>
        /// Checks whether a trimmed line starts with a given keyword followed by a space or paren.
        /// This avoids false matches like "interfaceX" matching "interface".
        /// </summary>
        public static bool StartsWithKeyword(string line, string keyword)
        {
            if (!line.StartsWith(keyword)) return false;
            if (line.Length == keyword.Length) return true;
            char next = line[keyword.Length];
            return next == ' ' || next == '(' || next == '{' || next == '\t';
        }
    }
}