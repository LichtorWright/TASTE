using System;
using System.Collections.Generic;

namespace taste
{
    // â”€â”€ Enumerations â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public enum AccessModifier { Public, Private, Protected, Internal }
    /// <summary>
    /// Memory allocation strategy for a type or scope.
    /// Determines how the C++ emitter wraps (or doesn't wrap) the type.
    /// </summary>
    public enum AllocationStrategy
    {
        /// <summary>Follows the memory policy chain (member → class → project XML → RAII default).</summary>
        Default,
        /// <summary>Plain value on the stack. Destructor runs at scope exit. Zero overhead.</summary>
        Stack,
        /// <summary>std::unique_ptr — sole ownership, deterministic destruction. No reference count.</summary>
        Unique,
        /// <summary>std::shared_ptr — shared ownership, reference-counted. Last reference cleans up.</summary>
        Shared,
        /// <summary>Raw pointer — manual management. Compiler enforces explicit cleanup in finalizer.</summary>
        Raw,
    }

    /// <summary>
    /// Project-level memory policy that determines default allocation strategies
    /// by scope visibility. Can be loaded from XML config or overridden per-class
    /// with [MemoryManagement].
    /// Resolution chain: member attribute → class [MemoryManagement] → project XML → built-in default.
    /// </summary>
    public class MemoryProfile
    {
        /// <summary>Default allocation for public members. Default: Shared (shared_ptr).</summary>
        public AllocationStrategy Public  { get; set; } = AllocationStrategy.Shared;
        /// <summary>Default allocation for private members. Default: Unique (unique_ptr).</summary>
        public AllocationStrategy Private { get; set; } = AllocationStrategy.Unique;
        /// <summary>Default allocation for method-level locals. Default: Stack (plain value).</summary>
        public AllocationStrategy Local   { get; set; } = AllocationStrategy.Stack;

        /// <summary>
        /// Built-in RAII profile: public=Shared, private=Unique, local=Stack.
        /// This is the default if no project XML config is provided.
        /// </summary>
        public static MemoryProfile RAII => new MemoryProfile();

        /// <summary>
        /// Safe profile: everything is shared_ptr. Matches the old Db behavior.
        /// </summary>
        public static MemoryProfile Safe => new MemoryProfile
        {
            Public  = AllocationStrategy.Shared,
            Private = AllocationStrategy.Shared,
            Local   = AllocationStrategy.Shared,
        };

        /// <summary>
        /// Manual profile: raw pointers for fields, stack for locals.
        /// For embedded / kernel / hot paths. Compiler enforces explicit cleanup.
        /// </summary>
        public static MemoryProfile Manual => new MemoryProfile
        {
            Public  = AllocationStrategy.Raw,
            Private = AllocationStrategy.Raw,
            Local   = AllocationStrategy.Stack,
        };

        // ── XML serialization ─────────────────────────────────────────────────

        /// <summary>
        /// Saves this memory profile to an XML file.
        /// The XML format is:
        /// <code>
        /// &lt;MemoryProfile&gt;
        ///   &lt;Public&gt;Shared&lt;/Public&gt;
        ///   &lt;Private&gt;Unique&lt;/Private&gt;
        ///   &lt;Local&gt;Stack&lt;/Local&gt;
        /// &lt;/MemoryProfile&gt;
        /// </code>
        /// </summary>
        public void SaveToXml(string filePath)
        {
            var serializer = new System.Xml.Serialization.XmlSerializer(typeof(MemoryProfile));
            using var writer = new System.IO.StreamWriter(filePath);
            serializer.Serialize(writer, this);
        }

        /// <summary>
        /// Loads a memory profile from an XML file.
        /// Returns the RAII default if the file doesn't exist or is invalid.
        /// </summary>
        public static MemoryProfile LoadFromXml(string filePath)
        {
            if (!System.IO.File.Exists(filePath))
                return RAII;

            try
            {
                var serializer = new System.Xml.Serialization.XmlSerializer(typeof(MemoryProfile));
                using var reader = new System.IO.StreamReader(filePath);
                return (MemoryProfile?)serializer.Deserialize(reader) ?? RAII;
            }
            catch (System.Xml.XmlException)
            {
                // Invalid XML — fall back to RAII default
                return RAII;
            }
        }

        /// <summary>
        /// Resolves the effective allocation strategy for a field,
        /// considering the field's own attributes, the class's [MemoryManagement],
        /// and the project-level profile.
        /// </summary>
        public AllocationStrategy ResolveField(AccessModifier access, List<SourceAttribute> attributes)
        {
            // 1. Member-level attribute override
            var attrOverride = SourceAttribute.GetAllocationOverride(attributes);
            if (attrOverride != AllocationStrategy.Default)
                return attrOverride;

            // 2. Type decoration overrides ([Address] → Raw, [Reference] → Stack, [Naked] → Stack)
            var decoration = SourceAttribute.GetTypeDecoration(attributes);
            if (decoration == TypeDecoration.Address)
                return AllocationStrategy.Raw;
            if (decoration == TypeDecoration.Reference || decoration == TypeDecoration.Naked)
                return AllocationStrategy.Stack;

            // 3. Scope-based default from profile
            return access == AccessModifier.Public ? Public
                 : access == AccessModifier.Private ? Private
                 : Private; // Protected/Internal → private default
        }

        /// <summary>
        /// Resolves the effective allocation strategy for a local variable.
        /// </summary>
        public AllocationStrategy ResolveLocal(List<SourceAttribute> attributes)
        {
            var attrOverride = SourceAttribute.GetAllocationOverride(attributes);
            if (attrOverride != AllocationStrategy.Default)
                return attrOverride;
            return Local;
        }
    }
    public enum ActionKind     { Assignment, MethodCall, Return, Break, Continue, Throw, Yield, Other }
    /// <summary>
    /// Kinds of conditional constructs.
    /// </summary>
    public enum ConditionKind
    {
        If,       // if (expr) { ... }
        Unless,   // unless (expr) { ... }  ≡  if (!expr) { ... }
        Else,     // else { ... }
        ElseIf,   // else if (expr) { ... }
        Switch,   // switch (expr) { case ... }
        Inline,   // condition ? trueExpr : falseExpr (not all languages support this)
        Match     // match (expr) { pattern => body }
    }

    /// <summary>
    /// Kinds of looping constructs.
    /// </summary>
    public enum LoopKind
    {
        While,       // while (expr) { ... }
        DoWhile,     // do { ... } while (expr);
        For,         // for (init; cond; step) { ... }
        ForEach,     // foreach (var x in collection) { ... }
        Repeat,      // repeat N { ... }            → for (int _i = 0; _i < N; ++_i)
        RepeatUntil  // repeat until (cond) { ... } → while (!(cond))
    }

    /// <summary>
    /// Mutability qualifiers for fields and variables.
    /// Rust <c>mut</c>, C++ <c>const</c> / <c>mutable</c>.
    /// </summary>
    [Flags]
    public enum MutabilityModifier
    {
        None     = 0,
        Const    = 1 << 0,   // C++ const, C# readonly
        Mutable  = 1 << 1,   // C++ mutable (can change in const methods)
        Volatile = 1 << 2,   // C++ volatile, C# volatile
        ReadOnly = 1 << 3,   // C# readonly (assigned only in constructor)
    }

    /// <summary>
    /// How a represented member is accessed in C++.
    /// Used by the emitter to determine the correct access operator.
    /// </summary>
    public enum MemberAccess
    {
        Dot,                  // .
        Arrow,                // ->
        Colon,                // : (often used for slicing/dictionaries, or keeping it generic)
        Colons,               // ::
        DotAsterisk,          // .*
        ArrowAsterisk,        // ->*
        QuestionMarkDot,      // ?.
        QuestionMarkBracket,  // ?[]
        Bracket,              // []
        None,                 // no accessor — free functions, constructors, top-level constants
    }

    /// <summary>
    /// Type decoration applied by [Address], [Reference], or [Naked] attributes.
    /// Controls how a type is emitted in C++: T*, T&amp;, or plain T.
    /// </summary>
    public enum TypeDecoration
    {
        None,       // Default — no special decoration
        Address,    // [Address]  → T*
        Reference,  // [Reference] → T&
        Naked,      // [Naked]    → T (strip any default decoration)
    }

    /// <summary>Modifiers that can appear on a method, constructor, or destructor.</summary>
    [Flags]
    public enum MethodModifier
    {
        None     = 0,
        Static   = 1 << 0,
        Virtual  = 1 << 1,
        Abstract = 1 << 2,
        Override = 1 << 3,
        Sealed   = 1 << 4,
        Async    = 1 << 5,
        Extern   = 1 << 6,
        New      = 1 << 7,   // hides base member
        Partial  = 1 << 8,   // partial method
    }

    // â”€â”€ Attributes â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// A source-level attribute such as [Stack], [Heap], [Shared], [Raw], [Address], [Reference], [Naked],
    /// [Const], [Explicit], [Noexcept], [Mutable], [MemoryManagement], or [Friend(Player)].
    /// Arguments are raw strings (unparsed); most attributes have zero or one.
    /// For [Represents], the Access property indicates how the member is accessed in C++.
    /// For [Address]/[Reference]/[Naked], the Decoration property indicates type decoration.
    /// For [Stack]/[Heap]/[Shared]/[Raw], the allocation strategy is resolved by the semantic analyzer.
    /// For [MemoryManagement], arguments are key=value pairs like "Public=Shared,Private=Unique,Local=Stack".
    /// </summary>
    public class SourceAttribute
    {
        public string          Name       { get; }
        public List<string>    Arguments  { get; } = new List<string>();
        /// <summary>
        /// For [Represents] attributes, the destination-language expression (first argument).
        /// E.g. [Represents("std::string", ...)] → Expression = "std::string".
        /// Null when the attribute has no arguments.
        /// </summary>
        public string?        Expression => Arguments.Count > 0 ? Arguments[0] : null;
        /// <summary>
        /// For [Represents] attributes, indicates the C++ access pattern.
        /// E.g. Dot for obj.size(), Arrow for ptr->size(), Colons for std::string::npos.
        /// </summary>
        public MemberAccess    Access     { get; set; } = MemberAccess.Dot;
        /// <summary>
        /// For [Address]/[Reference]/[Naked] attributes, indicates type decoration.
        /// [Address] → T*, [Reference] → T&amp;, [Naked] → T (no decoration).
        /// </summary>
        public TypeDecoration  Decoration { get; set; } = TypeDecoration.None;

        public SourceAttribute(string name) { Name = name; }

        /// <summary>Extracts the TypeDecoration from a list of attributes.</summary>
        public static TypeDecoration GetTypeDecoration(List<SourceAttribute> attributes)
        {
            var attr = attributes?.FirstOrDefault(a => a.Decoration != TypeDecoration.None);
            return attr?.Decoration ?? TypeDecoration.None;
        }

        /// <summary>Extracts the AllocationStrategy override from a list of attributes.</summary>
        public static AllocationStrategy GetAllocationOverride(List<SourceAttribute> attributes)
        {
            if (attributes == null) return AllocationStrategy.Default;
            foreach (var attr in attributes)
            {
                switch (attr.Name)
                {
                    case "Stack":  return AllocationStrategy.Stack;
                    case "Heap":   return AllocationStrategy.Unique;
                    case "Shared": return AllocationStrategy.Shared;
                    case "Raw":    return AllocationStrategy.Raw;
                }
            }
            return AllocationStrategy.Default;
        }

        /// <summary>
        /// Parses an allocation strategy from a raw string value (e.g. "Stack", "Heap", "Shared", "Raw").
        /// Used by <see cref="DbSemanticAnalyzer"/> when parsing [MemoryManagement] key=value pairs.
        /// </summary>
        public static AllocationStrategy ParseAllocationStrategy(string value)
        {
            return value switch
            {
                "Stack"  => AllocationStrategy.Stack,
                "Heap"   => AllocationStrategy.Unique,
                "Unique" => AllocationStrategy.Unique,
                "Shared" => AllocationStrategy.Shared,
                "Raw"    => AllocationStrategy.Raw,
                _        => AllocationStrategy.Default
            };
        }
    }

    // â”€â”€ File & namespace â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>Represents a parsed .db source file.</summary>
    public class CodeFile
    {
        public string Path { get; }
        public List<FileScopeDirective> FileScopeDirectives { get; } = new List<FileScopeDirective>();
        public List<Using>               Usings     { get; } = new List<Using>();
        public List<Namespace>           Namespaces { get; } = new List<Namespace>();
        public List<Class>               Classes    { get; } = new List<Class>();
        public List<InterfaceDeclaration> Interfaces { get; } = new List<InterfaceDeclaration>();
        public List<SumTypeDeclaration>  SumTypes   { get; } = new List<SumTypeDeclaration>();
        public List<MixinDeclaration>    Mixins     { get; } = new List<MixinDeclaration>();
        public List<TypeAliasDeclaration> TypeAliases { get; } = new List<TypeAliasDeclaration>();
        public List<EnumDecl>            Enums      { get; } = new List<EnumDecl>();
        public List<DelegateDecl>        Delegates  { get; } = new List<DelegateDecl>();

        public CodeFile(string path) { Path = path; }
    }

    public class Using
    {
        public string Name { get; }
        public Using(string name) { Name = name; }
    }

    /// <summary>
    /// A file-scope directive such as C++ <c>#include</c> or Rust <c>mod</c>.
    /// These are emitted at the top of the output file, before any namespace.
    /// </summary>
    public class FileScopeDirective
    {
        /// <summary>The directive kind: "include", "mod", "using", etc.</summary>
        public string Kind { get; }
        /// <summary>The target: header name, module name, etc.</summary>
        public string Target { get; }
        /// <summary>True for system includes (&lt;header&gt;), false for local includes ("header").</summary>
        public bool IsSystem { get; set; }

        public FileScopeDirective(string kind, string target) { Kind = kind; Target = target; }
    }

    public class Namespace
    {
        public string Name { get; }
        public List<Namespace>          NestedNamespaces { get; } = new List<Namespace>();
        public List<Class>              Classes          { get; } = new List<Class>();
        public List<InterfaceDeclaration> Interfaces      { get; } = new List<InterfaceDeclaration>();
        public List<SumTypeDeclaration>  SumTypes        { get; } = new List<SumTypeDeclaration>();
        public List<MixinDeclaration>    Mixins          { get; } = new List<MixinDeclaration>();
        public List<TypeAliasDeclaration> TypeAliases    { get; } = new List<TypeAliasDeclaration>();
        public List<EnumDecl>            Enums           { get; } = new List<EnumDecl>();
        public List<DelegateDecl>        Delegates        { get; } = new List<DelegateDecl>();

        public Namespace(string name) { Name = name; }
    }

    // â”€â”€ Type declarations â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>A class or struct declaration.</summary>
    public class Class
    {
        public string         Name       { get; }
        public AccessModifier Access     { get; set; }
        public bool           IsStruct   { get; set; }
        /// <summary>
        /// Per-class memory management override. When set, overrides the project-level
        /// MemoryProfile for this class. Null means use the project default.
        /// </summary>
        public MemoryProfile? MemoryManagement { get; set; }
        /// <summary>True when the class itself is declared abstract.</summary>
        public bool           IsAbstract { get; set; }
        /// <summary>True when the class is declared sealed (no further inheritance).</summary>
        public bool           IsSealed   { get; set; }
        /// <summary>True when declared static (no instances, only static members).</summary>
        public bool           IsStatic   { get; set; }
        /// <summary>
        /// Resolved allocation strategy for this class.
        /// Set by the semantic analyzer after applying the resolution chain:
        /// class attribute → project XML → RAII default.
        /// Structs default to Stack; classes default to Shared.
        /// </summary>
        public AllocationStrategy Allocation { get; set; } = AllocationStrategy.Default;

        public List<SourceAttribute>  Attributes      { get; } = new List<SourceAttribute>();
        public List<string>       FriendClasses   { get; } = new List<string>();
        public List<BaseClass>    BaseClasses     { get; } = new List<BaseClass>();
        public List<string>       TypeParams      { get; } = new List<string>();

        public List<Constant>     Constants       { get; } = new List<Constant>();
        public List<Field>        Fields          { get; } = new List<Field>();
        public List<Property>     Properties      { get; } = new List<Property>();
        public List<Indexer>      Indexers        { get; } = new List<Indexer>();
        public List<OperatorOverload> Operators   { get; } = new List<OperatorOverload>();
        public List<Event>        Events          { get; } = new List<Event>();
        public List<Method>       Methods         { get; } = new List<Method>();
        /// <summary>Finalizer: C# <c>~ClassName()</c> — non-deterministic cleanup (GC).</summary>
        public Method             Finalizer       { get; set; }
        /// <summary>Kotlin-style companion object: a named static container inside a class.</summary>
        public CompanionObject    Companion       { get; set; }
        public List<Class>        NestedClasses   { get; } = new List<Class>();
        public List<EnumDecl>     NestedEnums     { get; } = new List<EnumDecl>();
        public List<DelegateDecl> NestedDelegates { get; } = new List<DelegateDecl>();

        public Class(string name) { Name = name; }
    }

    /// <summary>A base class or interface reference in a class declaration.</summary>
    public class BaseClass
    {
        public string Name        { get; }
        public bool   IsInterface { get; }

        public BaseClass(string name, bool isInterface = false)
        {
            Name        = name;
            IsInterface = isInterface;
        }
    }

    // ── Interface declarations ───────────────────────────────────────────────

    /// <summary>
    /// An interface declaration: <c>public interface IComparable { ... }</c>
    /// Maps to C++ abstract class with pure virtual methods, Swift protocol, Rust trait.
    /// </summary>
    public class InterfaceDeclaration
    {
        public string            Name       { get; }
        public AccessModifier    Access     { get; set; }
        public List<string>      TypeParams { get; } = new List<string>();
        public List<string>      Extends    { get; } = new List<string>();
        public List<Method>      Methods    { get; } = new List<Method>();
        public List<Property>    Properties { get; } = new List<Property>();
        public List<Indexer>     Indexers   { get; } = new List<Indexer>();
        public List<Event>       Events     { get; } = new List<Event>();
        public List<SourceAttribute> Attributes { get; } = new List<SourceAttribute>();

        public InterfaceDeclaration(string name) { Name = name; }
    }

    // ── Sum type declarations ────────────────────────────────────────────────

    /// <summary>
    /// A sum type (discriminated union): Rust <c>enum</c> / Swift <c>enum</c>.
    /// Each variant can carry data. Emits as a C++ <c>std::variant</c> or tagged union.
    /// </summary>
    public class SumTypeDeclaration
    {
        public string            Name       { get; }
        public AccessModifier    Access     { get; set; }
        public List<string>      TypeParams { get; } = new List<string>();
        public List<SumVariant>  Variants   { get; } = new List<SumVariant>();
        public List<SourceAttribute> Attributes { get; } = new List<SourceAttribute>();

        public SumTypeDeclaration(string name) { Name = name; }
    }

    /// <summary>One variant of a <see cref="SumTypeDeclaration"/>.</summary>
    public class SumVariant
    {
        public string         Name  { get; }
        /// <summary>Associated data fields (tuple-style or named).</summary>
        public List<Parameter> Data { get; } = new List<Parameter>();

        public SumVariant(string name) { Name = name; }
    }

    // ── Mixin declarations ───────────────────────────────────────────────────

    /// <summary>
    /// A mixin declaration: Rust <c>impl</c> block, Kotlin extension, Swift extension.
    /// Allows adding methods to an existing type without inheritance.
    /// </summary>
    public class MixinDeclaration
    {
        /// <summary>The type this mixin extends (e.g. "String" in <c>impl String { ... }</c>).</summary>
        public string            TargetType { get; }
        public AccessModifier    Access     { get; set; }
        public List<Method>      Methods    { get; } = new List<Method>();
        public List<Property>    Properties { get; } = new List<Property>();
        public List<SourceAttribute> Attributes { get; } = new List<SourceAttribute>();

        public MixinDeclaration(string targetType) { TargetType = targetType; }
    }

    // ── Type alias declarations ──────────────────────────────────────────────

    /// <summary>
    /// A type alias: C++ <c>typedef</c>/<c>using</c>, Rust <c>type</c>, Swift <c>typealias</c>.
    /// <c>public type Alias = OriginalType;</c>
    /// Emits as: <c>using Alias = OriginalType;</c>
    /// </summary>
    public class TypeAliasDeclaration
    {
        public string            Name       { get; }
        public string            TargetType { get; }
        public AccessModifier    Access     { get; set; }
        public List<SourceAttribute> Attributes { get; } = new List<SourceAttribute>();

        public TypeAliasDeclaration(string name, string targetType) { Name = name; TargetType = targetType; }
    }

    // â”€â”€ Delegates & events â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// A delegate type declaration: <c>public delegate ReturnType Name(params);</c>
    /// Emits as: <c>using Name = std::function&lt;ReturnType(ParamTypes)&gt;;</c>
    /// </summary>
    public class DelegateDecl
    {
        public string            Name       { get; }
        public string            ReturnType { get; set; }
        public AccessModifier    Access     { get; set; }
        public List<Parameter>   Parameters { get; } = new List<Parameter>();
        public List<SourceAttribute> Attributes { get; } = new List<SourceAttribute>();

        public DelegateDecl(string name, string returnType) { Name = name; ReturnType = returnType; }
    }

    /// <summary>
    /// An event member: <c>public event OnDamage DamageTaken;</c>
    /// Multicast (default) â†’ backing list + Add/Remove/Invoke helpers.
    /// Singlecast â†’ plain <c>std::function</c> field.
    /// </summary>
    public class Event
    {
        public string            Name         { get; }
        public string            DelegateType { get; }
        public AccessModifier    Access       { get; set; }
        public bool              IsStatic     { get; set; }
        public bool              IsVirtual    { get; set; }
        public bool              IsOverride  { get; set; }
        public bool              IsMulticast  { get; set; } = true;
        public List<SourceAttribute> Attributes   { get; } = new List<SourceAttribute>();

        public Event(string name, string delegateType) { Name = name; DelegateType = delegateType; }
    }

    public class EnumDecl
    {
        public string            Name       { get; }
        public AccessModifier    Access     { get; set; }
        public string?           UnderlyingType { get; set; }
        public List<SourceAttribute> Attributes { get; } = new List<SourceAttribute>();
        public List<string>      Members    { get; } = new List<string>();

        public EnumDecl(string name) { Name = name; }
    }

    // â”€â”€ Class members â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// A compile-time constant: <c>const int Max = 100;</c>
    /// Emits as: <c>static constexpr int Max = 100;</c>
    /// </summary>
    public class Constant
    {
        public string            Name        { get; }
        public string            Type        { get; set; }
        public string            Value       { get; }
        public AccessModifier    Access      { get; set; }
        public List<SourceAttribute> Attributes  { get; } = new List<SourceAttribute>();

        public Constant(string name, string type, string value) { Name = name; Type = type; Value = value; }
    }

    /// <summary>A plain member variable (not a property).</summary>
    public class Field
    {
        public string            Name        { get; }
        public string            Type        { get; set; }
        public AccessModifier    Access      { get; set; }
        public string?           Initializer { get; set; }
        public bool              IsStatic    { get; set; }
        /// <summary>readonly in C# â†’ can only be assigned in constructor; emits const in C++.</summary>
        public bool              IsReadonly  { get; set; }
        /// <summary>True if this is a fixed-size buffer (unsafe context): <c>fixed int buffer[10];</c></summary>
        public bool              IsFixed     { get; set; }
        /// <summary>For fixed buffers, the size expression: <c>fixed int buffer[10];</c> → "10".</summary>
        public string?           FixedSize   { get; set; }        /// <summary>Mutability qualifiers: const, mutable, volatile, readonly.</summary>
        public MutabilityModifier Mutability { get; set; }
        /// <summary>True if this field is lazily initialised on first access (backtick syntax).</summary>
        public bool              IsLazy      { get; set; }
        /// <summary>Resolved allocation strategy for this field.
        /// Set by the semantic analyzer after applying the resolution chain:
        /// member attribute → class [MemoryManagement] → project XML → RAII default.
        /// </summary>
        public AllocationStrategy Allocation { get; set; } = AllocationStrategy.Default;
        public List<SourceAttribute> Attributes  { get; } = new List<SourceAttribute>();

        public Field(string name, string type) { Name = name; Type = type; }
    }

    /// <summary>A C# auto-property (get; set;). Emits a backing field + accessor pair.</summary>
    public class Property
    {
        public string            Name       { get; }
        public string            Type       { get; set; }
        public AccessModifier    Access     { get; set; }
        public bool              HasGetter  { get; set; }
        public bool              HasSetter  { get; set; }
        public bool              IsStatic   { get; set; }
        public bool              IsVirtual  { get; set; }
        public bool              IsOverride { get; set; }
        public bool              IsAbstract { get; set; }
        public bool              IsSealed   { get; set; }
        /// <summary>True if this is an indexer (this[...] in C#).</summary>
        public bool              IsIndexer  { get; set; }
        /// <summary>Parameters for indexer (e.g., "int index").</summary>
        public List<Parameter>   IndexerParameters { get; } = new List<Parameter>();
        /// <summary>Expression-bodied property: <c>=> expr</c>. The expression is stored in Initializer.</summary>
        public bool              IsExpressionBodied { get; set; }
        /// <summary>For expression-bodied properties, the expression after <c>=></c>.</summary>
        public string?           Initializer { get; set; }
        /// <summary>Mutability qualifiers (e.g. const getter).</summary>
        public MutabilityModifier Mutability { get; set; }
        public List<SourceAttribute> Attributes { get; } = new List<SourceAttribute>();

        public Property(string name, string type) { Name = name; Type = type; }
    }

    /// <summary>
    /// An indexer: <c>public int this[int i] { get; set; }</c>
    /// Emits as: <c>T&amp; operator[](int i)</c> / <c>const T&amp; operator[](int i) const</c>
    /// </summary>
    public class Indexer
    {
        public string            ElementType { get; }
        public AccessModifier    Access      { get; set; }
        public bool              HasGetter   { get; set; }
        public bool              HasSetter   { get; set; }
        public List<Parameter>   Parameters  { get; } = new List<Parameter>();
        public List<Statement>   GetBody     { get; } = new List<Statement>();
        public List<Statement>   SetBody     { get; } = new List<Statement>();
        public List<SourceAttribute> Attributes  { get; } = new List<SourceAttribute>();

        public Indexer(string elementType) { ElementType = elementType; }
    }

    /// <summary>
    /// An operator overload: <c>public static Player operator+(Player a, Player b)</c>
    /// Emits as: <c>friend ClassName operator+(ClassName a, ClassName b)</c>
    /// </summary>
    public class OperatorOverload
    {
        /// <summary>The operator token, e.g. "+", "==", "!=", "&lt;", "[]".</summary>
        public string            Operator   { get; }
        public string            ReturnType { get; set; }
        public AccessModifier    Access     { get; set; }
        public List<Parameter>   Parameters { get; } = new List<Parameter>();
        public List<Statement>   Body       { get; } = new List<Statement>();
        public List<SourceAttribute> Attributes { get; } = new List<SourceAttribute>();

        public OperatorOverload(string op, string returnType) { Operator = op; ReturnType = returnType; }
    }

    public class Method
    {
        public string          Name          { get; set; }
        public string          ReturnType    { get; set; }
        public AccessModifier  Access        { get; set; }
        public MethodModifier  Modifiers     { get; set; }
        /// <summary>True for constructors (no return type in source).</summary>
        public bool            IsConstructor { get; set; }
        /// <summary>True for destructors (~ClassName).</summary>
        public bool            IsDestructor  { get; set; }
        /// <summary>True for explicit interface implementations (IMyInterface.Method).</summary>
        public bool            IsExplicitInterfaceImplementation => Name.Contains(".");

        public List<string>          TypeParams       { get; } = new List<string>();
        public List<SourceAttribute>     Attributes       { get; } = new List<SourceAttribute>();
        public List<Parameter>       Parameters       { get; } = new List<Parameter>();
        public List<CtorInitializer> CtorInitializers { get; } = new List<CtorInitializer>();
        public List<Variable>        Variables        { get; } = new List<Variable>();
        public List<Statement>       Body             { get; } = new List<Statement>();

        public Method(string name, string returnType = "void")
        {
            Name       = name;
            ReturnType = returnType;
        }

        // Convenience modifier checks
        public bool IsStatic   => Modifiers.HasFlag(MethodModifier.Static);
        public bool IsVirtual  => Modifiers.HasFlag(MethodModifier.Virtual);
        public bool IsAbstract => Modifiers.HasFlag(MethodModifier.Abstract);
        public bool IsOverride => Modifiers.HasFlag(MethodModifier.Override);
        public bool IsSealed   => Modifiers.HasFlag(MethodModifier.Sealed);
        public bool IsAsync    => Modifiers.HasFlag(MethodModifier.Async);
        public bool IsExtern   => Modifiers.HasFlag(MethodModifier.Extern);
        /// <summary>True when the method hides a base class member (C# 'new' keyword). Name hiding is implicit in C++.</summary>
        public bool IsNew      => Modifiers.HasFlag(MethodModifier.New);
        /// <summary>True when the method is a partial method (no body, implementation may be elsewhere).</summary>
        public bool IsPartial  => Modifiers.HasFlag(MethodModifier.Partial);

        /// <summary>Mutability qualifiers for this method (e.g. const, volatile).</summary>
        public MutabilityModifier Mutability { get; set; }
    }

    /// <summary>One entry in a C++ constructor initializer list: <c>field(expr)</c>.</summary>
    public class CtorInitializer
    {
        public string MemberName { get; }
        public string Expression { get; }

        public CtorInitializer(string memberName, string expression)
        {
            MemberName = memberName;
            Expression = expression;
        }
    }

    /// <summary>
    /// A companion object: Kotlin <c>companion object { ... }</c>.
    /// Contains static members that belong to the enclosing class.
    /// Emits as a nested struct with static members in C++.
    /// </summary>
    public class CompanionObject
    {
        /// <summary>Optional name for the companion object (null for anonymous).</summary>
        public string?            Name       { get; set; }
        public List<Constant>     Constants  { get; } = new List<Constant>();
        public List<Field>        Fields     { get; } = new List<Field>();
        public List<Property>     Properties { get; } = new List<Property>();
        public List<Method>       Methods    { get; } = new List<Method>();
        public List<SourceAttribute>  Attributes { get; } = new List<SourceAttribute>();
    }

    public class Parameter
    {
        public string Name    { get; }
        public string Type    { get; set; }
        public string Modifier { get; set; } = ""; // "ref", "out", "in", or empty
        /// <summary>True for <c>params T[] arr</c> — maps to variadic in C++.</summary>
        public bool   IsParams { get; set; }
        /// <summary>Default value expression, if any.</summary>
        public string? Default { get; set; }

        public Parameter(string name, string type) { Name = name; Type = type; }
    }

    public class NamedArgument
    {
        public string Name { get; }
        public string Value { get; }
        public NamedArgument(string name, string value) { Name = name; Value = value; }
    }

    // â”€â”€ Method body â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>A local variable declaration inside a method body.</summary>
    public class Variable
    {
        public string  Name           { get; }
        public string  Type           { get; set; }
        public string? Initializer    { get; set; }
        public bool    IsNewObject     { get; set; }
        public string? ConstructorArgs { get; set; }
        public List<NamedArgument> NamedArgs { get; } = new List<NamedArgument>();
        /// <summary>True when declared with <c>var</c> (type inferred).</summary>
        public bool    IsInferred      { get; set; }
        /// <summary>Mutability qualifiers for local variables (e.g. const).</summary>
        public MutabilityModifier Mutability { get; set; }
        /// <summary>Parameter modifier: ref, out, in, params.</summary>
        public string? Modifier { get; set; }
        /// <summary>Source-level attributes on this variable (e.g. [Stack], [Heap], [Shared], [Raw]).</summary>
        public List<SourceAttribute> Attributes { get; } = new List<SourceAttribute>();
        /// <summary>Resolved allocation strategy for this local variable.
        /// Set by the semantic analyzer after applying the resolution chain:
        /// member attribute → class [MemoryManagement] → project XML → RAII default.
        /// </summary>
        public AllocationStrategy Allocation { get; set; } = AllocationStrategy.Default;

        public Variable(string name, string type) { Name = name; Type = type; }
    }

    /// <summary>Base for every statement that can appear in a method body.</summary>
    public abstract class Statement { }

    /// <summary>
    /// A single-line imperative statement: assignment, call, return, break, continue,
    /// throw, yield, or any other raw expression statement.
    /// </summary>
    public class Action : Statement
    {
        public ActionKind Kind { get; }
        /// <summary>The raw C# source text of this statement (before transpilation).</summary>
        public string Raw { get; set; }

        public Action(ActionKind kind, string raw) { Kind = kind; Raw = raw; }
    }

    /// <summary>
    /// Verbatim native-code passthrough block: <c>cpp { ... }</c>, <c>rust { ... }</c>, etc.
    /// Lines are emitted as-is without transpilation. The language tag determines
    /// which emitter handles the block; other emitters skip it.
    /// </summary>
    public class InlineNativeBlock : Statement
    {
        public List<string> Lines { get; } = new List<string>();
    }

    /// <summary>
    /// A conditional construct: if/else, switch, match.
    /// else-if chains: store a Condition(ElseIf) in ElseBody.
    /// </summary>
    public class Condition : Statement
    {
        public ConditionKind   Kind       { get; }
        public string          Expression { get; set; } = "";

        public List<Statement> Body     { get; } = new List<Statement>();
        public List<Statement> ElseBody { get; } = new List<Statement>();

        public Condition(ConditionKind kind, string expression = "") { Kind = kind; Expression = expression; }
    }

    /// <summary>
    /// A looping construct: while, do-while, for, foreach.
    /// foreach: use IterationVariable + Collection.
    /// </summary>
    public class Loop : Statement
    {
        public LoopKind         Kind       { get; }
        public string           Expression { get; set; } = "";

        // foreach-specific
        public Variable?        IterationVariable { get; set; }
        public string?          Collection        { get; set; }

        // for-specific (decomposed from for(init; cond; incr))
        /// <summary>For-loop initializer (e.g. "int i = 0"). Null if not a for-loop or not decomposed.</summary>
        public string?          ForInitializer { get; set; }
        /// <summary>For-loop increment (e.g. "++i"). Null if not a for-loop or not decomposed.</summary>
        public string?          ForIncrement   { get; set; }

        public List<Statement> Body { get; } = new List<Statement>();

        public Loop(LoopKind kind, string expression = "") { Kind = kind; Expression = expression; }
    }

    /// <summary>
    /// A try/catch/finally block.
    /// </summary>
    public class TryCatchBlock : Statement
    {
        public List<Statement>  TryBody     { get; } = new List<Statement>();
        public List<CatchClause> Catches    { get; } = new List<CatchClause>();
        public List<Statement>  FinallyBody { get; } = new List<Statement>();
    }

    /// <summary>One catch clause inside a <see cref="TryCatchBlock"/>.</summary>
    public class CatchClause
    {
        /// <summary>Exception type, e.g. "InvalidOperationException". Null = bare catch.</summary>
        public string?         ExceptionType { get; set; }
        /// <summary>Bound variable name, e.g. "ex". Null when catch has no variable.</summary>
        public string?         VariableName  { get; set; }
        /// <summary>Optional when-filter: <c>catch (Ex e) when (e.Code == 42)</c>.</summary>
        public string?         WhenFilter    { get; set; }
        public List<Statement> Body          { get; } = new List<Statement>();
    }

    /// <summary>
    /// A C# using statement (resource acquisition):
    /// <c>using (var r = GetResource()) { ... }</c>
    /// Emits as a scoped block; the resource variable goes out of scope at '}'.
    /// In C++ the destructor handles cleanup (RAII).
    /// </summary>
    public class UsingBlock : Statement
    {
        public Variable        Resource { get; }
        public List<Statement> Body     { get; } = new List<Statement>();

        public UsingBlock(Variable resource) { Resource = resource; }
    }

    /// <summary>
    /// A lock statement: <c>lock (obj) { ... }</c>
    /// Emits as: <c>{ std::lock_guard&lt;std::mutex&gt; _lock(obj); ... }</c>
    /// </summary>
    public class LockBlock : Statement
    {
        /// <summary>The expression being locked, e.g. "_mutex" or "this".</summary>
        public string          LockExpression { get; }
        public List<Statement> Body           { get; } = new List<Statement>();

        public LockBlock(string lockExpression) { LockExpression = lockExpression; }
    }

    /// <summary>
    /// A throw statement: <c>throw new InvalidOperationException("msg");</c>
    /// Emits as: <c>throw InvalidOperationException("msg");</c> (no 'new' in C++).
    /// </summary>
    public class ThrowStatement : Statement
    {
        /// <summary>The exception type being thrown. Null for a bare rethrow (<c>throw;</c>).</summary>
        public string?         ExceptionType { get; set; }
        /// <summary>Constructor arguments for the exception, if any.</summary>
        public string?         Arguments     { get; set; }

        public ThrowStatement() { }
        public ThrowStatement(string exceptionType, string? arguments = null)
        {
            ExceptionType = exceptionType;
            Arguments     = arguments;
        }
    }

    /// <summary>
    /// A yield statement inside an iterator method.
    /// <c>yield return expr</c> or <c>yield break</c>.
    /// Maps to C++23 coroutine <c>co_yield</c> / <c>co_return</c>.
    /// </summary>
    public class YieldStatement : Statement
    {
        public bool    IsBreak    { get; }
        /// <summary>The yielded expression (null when IsBreak is true).</summary>
        public string? Expression { get; set; }

        public YieldStatement(bool isBreak = false) { IsBreak = isBreak; }
    }

    /// <summary>
    /// A checked or unchecked block: <c>checked { x = a + b; }</c>
    /// C++ has no overflow checking; checked blocks emit as plain scopes with a comment.
    /// </summary>
    public class CheckedBlock : Statement
    {
        public bool            IsChecked { get; }
        public List<Statement> Body      { get; } = new List<Statement>();

        public CheckedBlock(bool isChecked) { IsChecked = isChecked; }
    }

    /// <summary>
    /// An unsafe block: <c>unsafe { ... }</c>.
    /// In C++, all code is "unsafe" — this is a no-op block that just emits its body.
    /// </summary>
    public class UnsafeBlock : Statement
    {
        public List<Statement> Body { get; } = new List<Statement>();
    }

    /// <summary>
    /// A fixed statement: <c>fixed (int* p = &amp;arr[0]) { ... }</c>.
    /// Pins a managed object in memory for the duration of the block.
    /// In C++, this is a no-op — pointers are already stable.
    /// </summary>
    public class FixedStatement : Statement
    {
        public string            Declaration { get; }
        public List<Statement>   Body        { get; } = new List<Statement>();

        public FixedStatement(string declaration) { Declaration = declaration; }
    }

    // ── Additional statement nodes ───────────────────────────────────────────

    /// <summary>
    /// An explicit block statement: <c>{ ... }</c>.
    /// Creates a new scope. Useful for RAII patterns in C++.
    /// </summary>
    public class BlockStatement : Statement
    {
        public List<Statement> Statements { get; } = new List<Statement>();
    }

    /// <summary>
    /// A do-while loop: <c>do { ... } while (condition);</c>
    /// </summary>
    public class DoWhileStatement : Statement
    {
        public string          Condition  { get; set; } = "";
        public List<Statement> Body       { get; } = new List<Statement>();

        public DoWhileStatement() { }
        public DoWhileStatement(string condition) { Condition = condition; }
    }

    /// <summary>
    /// A return statement: <c>return expr;</c> or bare <c>return;</c>.
    /// </summary>
    public class ReturnStatement : Statement
    {
        /// <summary>The raw expression text (fallback for emission).</summary>
        public string? Expression { get; set; }
        /// <summary>The parsed expression AST node (null if not yet parsed).</summary>
        public Expression? ParsedExpression { get; set; }

        public ReturnStatement() { }
        public ReturnStatement(string expression) { Expression = expression; }
        public ReturnStatement(Expression expression) { ParsedExpression = expression; Expression = expression?.ToString(); }
    }

    /// <summary>A break statement: <c>break;</c></summary>
    public class BreakStatement : Statement { }

    /// <summary>A continue statement: <c>continue;</c></summary>
    public class ContinueStatement : Statement { }

    /// <summary>
    /// Defers a statement to run at end of the enclosing scope.
    /// Syntax: <c>`expr;</c> or <c>defer expr;</c>
    /// Compiles to a self-contained C++17 RAII scope guard.
    /// </summary>
    public class DeferStatement : Statement
    {
        /// <summary>The statement text to execute on scope exit (including trailing semicolon).</summary>
        public string Body { get; set; }
        public DeferStatement(string body) { Body = body; }
    }

    /// <summary>
    /// Move-initializes or move-assigns a variable.
    /// Syntax: <c>var b &lt;- a;</c> (declaration) or <c>b &lt;- a;</c> (assignment).
    /// Compiles to <c>auto b = std::move(a);</c> or <c>b = std::move(a);</c>.
    /// </summary>
    public class MoveStatement : Statement
    {
        public string Target        { get; set; }
        public string Source        { get; set; }
        public bool   IsDeclaration { get; set; }
        public MoveStatement(string target, string source, bool isDeclaration = false)
        { Target = target; Source = source; IsDeclaration = isDeclaration; }
    }

    /// <summary>
    /// Swaps two values in place.
    /// Syntax: <c>b &lt;-&gt; a;</c> or <c>swap(b, a);</c>
    /// Compiles to <c>std::swap(b, a);</c>.
    /// </summary>
    public class SwapStatement : Statement
    {
        public string Left  { get; set; }
        public string Right { get; set; }
        public SwapStatement(string left, string right) { Left = left; Right = right; }
    }

    /// <summary>
    /// Postfix conditional: <c>expr if (cond) : alt;</c> or <c>expr unless (cond) : alt;</c>.
    /// Compiles to <c>if (cond) { primary; } else { alt; }</c> (or negated for unless).
    /// </summary>
    public class PostfixConditional : Statement
    {
        /// <summary>The primary action text (return, call, etc.) executed when the condition holds.</summary>
        public string Primary   { get; set; } = "";
        public string Condition { get; set; } = "";
        /// <summary>When true, condition is negated: execute Primary unless Condition.</summary>
        public bool   IsUnless  { get; set; }
        /// <summary>Optional alternative action; null means no else branch.</summary>
        public string? Alt      { get; set; }
    }

    /// <summary>
    /// A match statement (Rust <c>match</c> / Swift <c>switch</c> with pattern matching).
    /// Unlike a C# switch, each arm can destructure and have guard conditions.
    /// </summary>
    public class MatchStatement : Statement
    {
        /// <summary>The expression being matched.</summary>
        public string          Expression { get; set; } = "";
        public List<MatchArm>  Arms      { get; } = new List<MatchArm>();
    }

    /// <summary>One arm of a <see cref="MatchStatement"/>.</summary>
    public class MatchArm
    {
        /// <summary>The pattern to match (raw text for now).</summary>
        public string          Pattern   { get; set; } = "";
        /// <summary>Optional guard condition: <c>if guard_expr</c>.</summary>
        public string?         Guard     { get; set; }
        public List<Statement> Body      { get; } = new List<Statement>();
    }

    /// <summary>
    /// A log statement: <c>log stream for MessageType.Level payload;</c>
    /// Db-specific structured logging. The compiler translates this into
    /// a call to the stream's Write method, injecting file/line context.
    /// In release builds, Debug-level logs are compiled out entirely.
    /// </summary>
    public class LogStatement : Statement
    {
        /// <summary>The stream expression (where the log goes). e.g. "console", "fileLog".</summary>
        public string Stream { get; set; } = "";
        /// <summary>The message type / channel. e.g. "MessageType.Debug", "MessageType.Error".</summary>
        public string MessageType { get; set; } = "";
        /// <summary>The payload expression (what to log). e.g. "new Exception()", "\"Server started\"".</summary>
        public string Payload { get; set; } = "";
        /// <summary>Source file for context injection (set by parser).</summary>
        public string? SourceFile { get; set; }
        /// <summary>Source line for context injection (set by parser).</summary>
        public int SourceLine { get; set; }

        public LogStatement() { }
        public LogStatement(string stream, string messageType, string payload)
        {
            Stream = stream;
            MessageType = messageType;
            Payload = payload;
        }
    }

    // ── Expression nodes ─────────────────────────────────────────────────────

    /// <summary>Base for every expression that can appear in a statement or initializer.</summary>
    public abstract class Expression { }

    /// <summary>A literal value: number, string, boolean, null, character.</summary>
    public class LiteralExpression : Expression
    {
        /// <summary>The literal value as it appears in source (e.g. "42", "\"hello\"", "true", "null").</summary>
        public string Value { get; set; } = "";
        /// <summary>Optional type hint for disambiguation (e.g. "int", "float", "string").</summary>
        public string? LiteralType { get; set; }

        public LiteralExpression() { }
        public LiteralExpression(string value) { Value = value; }
    }

    /// <summary>A variable or type name reference: <c>x</c>, <c>MyClass</c>, <c>std::string</c>.</summary>
    public class IdentifierExpression : Expression
    {
        public string Name { get; set; } = "";

        public IdentifierExpression() { }
        public IdentifierExpression(string name) { Name = name; }
    }

    /// <summary>A binary operation: <c>a + b</c>, <c>a && b</c>, <c>a == b</c>, etc.</summary>
    public class BinaryExpression : Expression
    {
        public Expression Left     { get; set; }
        public string     Operator { get; set; } = "";
        public Expression Right    { get; set; }

        public BinaryExpression() { Left = new LiteralExpression(); Right = new LiteralExpression(); }
        public BinaryExpression(Expression left, string op, Expression right)
        {
            Left = left; Operator = op; Right = right;
        }
    }

    /// <summary>A unary operation: <c>!a</c>, <c>-a</c>, <c>a++</c>, <c>a--</c>, <c>~a</c>.</summary>
    public class UnaryExpression : Expression
    {
        public Expression Operand  { get; set; }
        public string     Operator { get; set; } = "";
        /// <summary>True for prefix operators (!a, -a, ~a), false for postfix (a++, a--).</summary>
        public bool       IsPrefix { get; set; } = true;

        public UnaryExpression() { Operand = new LiteralExpression(); }
        public UnaryExpression(string op, Expression operand, bool isPrefix = true)
        {
            Operator = op; Operand = operand; IsPrefix = isPrefix;
        }
    }

    /// <summary>An assignment: <c>a = b</c>, <c>a += b</c>, <c>a ??= b</c>, etc.</summary>
    public class AssignmentExpression : Expression
    {
        public Expression Target   { get; set; }
        public string     Operator { get; set; } = "=";
        public Expression Value    { get; set; }

        public AssignmentExpression() { Target = new IdentifierExpression(); Value = new LiteralExpression(); }
        public AssignmentExpression(Expression target, string op, Expression value)
        {
            Target = target; Operator = op; Value = value;
        }
    }

    /// <summary>
    /// A member access expression using the <see cref="MemberAccess"/> enum.
    /// E.g. <c>obj.field</c> (Dot), <c>ptr->field</c> (Arrow), <c>Type::member</c> (Colons).
    /// </summary>
    public class MemberAccessExpression : Expression
    {
        public Expression  Target  { get; set; }
        public string      Member  { get; set; } = "";
        public MemberAccess Access { get; set; } = MemberAccess.Dot;

        public MemberAccessExpression() { Target = new IdentifierExpression(); }
        public MemberAccessExpression(Expression target, string member, MemberAccess access = MemberAccess.Dot)
        {
            Target = target; Member = member; Access = access;
        }
    }

    /// <summary>
    /// An argument in a method call, optionally prefixed with a modifier
    /// (<c>out</c>, <c>ref</c>, <c>in</c>).
    /// </summary>
    public class Argument
    {
        /// <summary>The modifier: "out", "ref", "in", or "" for none.</summary>
        public string     Modifier { get; set; } = "";
        /// <summary>The expression value passed as the argument.</summary>
        public Expression Value    { get; set; } = new IdentifierExpression();

        public Argument() { }
        public Argument(Expression value, string modifier = "") { Value = value; Modifier = modifier; }
    }

    /// <summary>A function/method invocation: <c>func(args)</c>, <c>obj.method(args)</c>.</summary>
    public class InvocationExpression : Expression
    {
        /// <summary>The expression being called (can be an identifier, member access, etc.).</summary>
        public Expression        Callee     { get; set; }
        public List<Argument>    Arguments  { get; } = new List<Argument>();

        public InvocationExpression() { Callee = new IdentifierExpression(); }
        public InvocationExpression(Expression callee) { Callee = callee; }
    }

    /// <summary>
    /// Object creation: <c>new Type(args)</c> or stack allocation.
    /// Maps to C++ constructor calls, <c>std::make_shared</c>, or <c>std::make_unique</c>.
    /// The <see cref="Allocation"/> property determines the C++ emission strategy.
    /// </summary>
    public class ObjectCreationExpression : Expression
    {
        public string            Type       { get; set; } = "";
        public List<Expression>  Arguments  { get; } = new List<Expression>();
        /// <summary>
        /// Allocation strategy for this object creation.
        /// Set by the semantic analyzer based on the memory policy resolution chain.
        /// </summary>
        public AllocationStrategy Allocation { get; set; } = AllocationStrategy.Default;
        /// <summary>Legacy: True for stack allocation (plain constructor call). Prefer <see cref="Allocation"/>.</summary>
        public bool IsStackAlloc { get => Allocation == AllocationStrategy.Stack; set { if (value) Allocation = AllocationStrategy.Stack; } }
        /// <summary>Named constructor arguments (for C#-style <c>new Foo { X = 1 }</c>).</summary>
        public List<NamedArgument> NamedArgs { get; } = new List<NamedArgument>();

        public ObjectCreationExpression() { }
        public ObjectCreationExpression(string type) { Type = type; }
    }

    /// <summary>
    /// A type cast: <c>(Type)expr</c> or <c>expr as Type</c>.
    /// <see cref="IsImplicit"/> true for C-style casts, false for <c>as</c>-style casts.
    /// </summary>
    public class CastExpression : Expression
    {
        public string     TargetType  { get; set; } = "";
        public Expression Inner       { get; set; }
        /// <summary>True for C-style / C++ static_cast, false for dynamic_cast (as).</summary>
        public bool       IsImplicit  { get; set; }

        public CastExpression() { Inner = new LiteralExpression(); }
        public CastExpression(string targetType, Expression inner, bool isImplicit = true)
        {
            TargetType = targetType; Inner = inner; IsImplicit = isImplicit;
        }
    }

    /// <summary>
    /// A type check: <c>expr is Type</c> (C#) / <c>typeof(expr) == Type</c> (JS).
    /// </summary>
    public class IsTypeExpression : Expression
    {
        public Expression Inner     { get; set; }
        public string     TargetType { get; set; } = "";

        public IsTypeExpression() { Inner = new LiteralExpression(); }
        public IsTypeExpression(Expression inner, string targetType)
        {
            Inner = inner; TargetType = targetType;
        }
    }

    /// <summary>A parenthesized expression: <c>(a + b)</c>.</summary>
    public class ParenthesizedExpression : Expression
    {
        public Expression Inner { get; set; }

        public ParenthesizedExpression() { Inner = new LiteralExpression(); }
        public ParenthesizedExpression(Expression inner) { Inner = inner; }
    }

    /// <summary>A ternary conditional: <c>condition ? trueExpr : falseExpr</c>.</summary>
    public class TernaryExpression : Expression
    {
        public Expression Condition   { get; set; }
        public Expression TrueExpr    { get; set; }
        public Expression FalseExpr   { get; set; }

        public TernaryExpression()
        {
            Condition = new LiteralExpression();
            TrueExpr = new LiteralExpression();
            FalseExpr = new LiteralExpression();
        }
        public TernaryExpression(Expression cond, Expression trueExpr, Expression falseExpr)
        {
            Condition = cond; TrueExpr = trueExpr; FalseExpr = falseExpr;
        }
    }

    /// <summary>A range expression: <c>start..end</c> (Rust/Swift).</summary>
    public class RangeExpression : Expression
    {
        /// <summary>Null for open-ended ranges like <c>..end</c>.</summary>
        public Expression? Start     { get; set; }
        /// <summary>Null for open-ended ranges like <c>start..</c>.</summary>
        public Expression? End       { get; set; }
        /// <summary>True if the range is inclusive on the end (<c>..=</c> in Rust).</summary>
        public bool        IsInclusive { get; set; }

        public RangeExpression() { }
    }

    /// <summary>A lambda / anonymous function: <c>(args) => expr</c> or <c>(args) => { stmts }</c>.</summary>
    public class LambdaExpression : Expression
    {
        public List<Parameter>  Parameters { get; } = new List<Parameter>();
        /// <summary>Single expression body (expression lambda).</summary>
        public Expression?     ExpressionBody { get; set; }
        /// <summary>Statement body (statement lambda). Null for expression lambdas.</summary>
        public List<Statement> StatementBody  { get; } = new List<Statement>();
        /// <summary>Return type hint (null if inferred).</summary>
        public string?         ReturnType     { get; set; }

        public LambdaExpression() { }
    }

    /// <summary>An await expression: <c>await expr</c>. Maps to C++20 coroutines <c>co_await</c>.</summary>
    public class AwaitExpression : Expression
    {
        public Expression Inner { get; set; }

        public AwaitExpression() { Inner = new LiteralExpression(); }
        public AwaitExpression(Expression inner) { Inner = inner; }
    }

    /// <summary>An array creation expression: <c>[1, 2, 3]</c> or <c>new T[n]</c>.</summary>
    public class ArrayCreationExpression : Expression
    {
        /// <summary>Element expressions for initializer lists.</summary>
        public List<Expression> Elements { get; } = new List<Expression>();
        /// <summary>The element type (null if inferred).</summary>
        public string?          ElementType { get; set; }
        /// <summary>For sized arrays: the size expression (null for initializer lists).</summary>
        public Expression?      Size        { get; set; }

        public ArrayCreationExpression() { }
    }

    /// <summary>A tuple expression: <c>(1, "hello")</c>.</summary>
    public class TupleExpression : Expression
    {
        public List<Expression> Elements { get; } = new List<Expression>();

        public TupleExpression() { }
    }
}
