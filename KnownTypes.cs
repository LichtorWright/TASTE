using System.Collections.Generic;

namespace taste
{
    /// <summary>
    /// Canonical registry of built-in and standard-library type names that the
    /// compiler recognizes without requiring explicit stub declarations.
    /// Single source of truth — both the semantic analyzer and emitters consult this.
    /// </summary>
    public static class KnownTypes
    {
        /// <summary>
        /// C# / Db source-language built-in types.
        /// These are the types that appear in Db source code (e.g. <c>int</c>, <c>string</c>, <c>bool</c>).
        /// </summary>
        public static readonly HashSet<string> SourceBuiltIns = new()
        {
            "void", "bool", "byte", "sbyte", "char", "short", "ushort",
            "int", "uint", "long", "ulong", "float", "double", "decimal",
            "string", "object", "var", "dynamic", "nint", "nuint",
        };

        /// <summary>
        /// C++ standard-library types that may appear after stub resolution.
        /// These are recognized by the semantic analyzer so that resolved types
        /// don't trigger "unknown type" warnings.
        /// </summary>
        public static readonly HashSet<string> CppStandardTypes = new()
        {
            "auto", "size_t",
            "int8_t", "int16_t", "int32_t", "int64_t",
            "uint8_t", "uint16_t", "uint32_t", "uint64_t",
            "std::string", "std::wstring", "std::string_view",
            "std::vector", "std::list", "std::deque", "std::forward_list",
            "std::map", "std::multimap", "std::unordered_map",
            "std::set", "std::multiset", "std::unordered_set",
            "std::shared_ptr", "std::unique_ptr", "std::weak_ptr",
            "std::function", "std::tuple", "std::pair", "std::optional",
            "std::variant", "std::monostate",
            "std::array", "std::bitset",
            "std::mutex", "std::lock_guard",
            "std::queue", "std::stack", "std::priority_queue",
        };

        /// <summary>
        /// Db collection and framework types that are recognized by the compiler.
        /// These map to C++ types via <see cref="Emit.Cpp.CppTypes.TypeMap"/>.
        /// </summary>
        public static readonly HashSet<string> DbFrameworkTypes = new()
        {
            "List", "Dictionary", "HashSet", "Queue", "Stack",
            "Task", "Action", "Func",
        };

        /// <summary>
        /// All known types combined — for quick "is this type recognized?" checks.
        /// </summary>
        public static readonly HashSet<string> All = new(SourceBuiltIns);

        static KnownTypes()
        {
            All.UnionWith(CppStandardTypes);
            All.UnionWith(DbFrameworkTypes);
        }

        /// <summary>
        /// Checks whether a type name is a known built-in, standard-library, or framework type.
        /// Strips generic parameters, pointers, and references before checking.
        /// </summary>
        public static bool IsKnown(string typeName)
        {
            string baseName = typeName.Split('<', '[', '&')[0].TrimEnd('*', '&');
            return All.Contains(baseName);
        }
    }
}