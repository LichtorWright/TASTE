using System.Collections.Generic;
using System.Linq;

namespace taste.Emit.Cpp
{
    /// <summary>
    /// Contains C++ type mappings and language-specific configurations.
    /// </summary>
    public static class CppTypes
    {
        /// <summary>
        /// Gets the fundamental C# types that map directly to C++ types.
        /// Derived from <see cref="TypeMap"/> keys — no separate list to maintain.
        /// </summary>
        public static HashSet<string> BuiltInTypes { get; } = new HashSet<string>(TypeMap.Keys);
        
        /// <summary>
        /// Gets the C++ collection types that need element type extraction.
        /// </summary>
        public static HashSet<string> CollectionTypes { get; } = new HashSet<string>
        {
            "List", "Dictionary", "HashSet", "Queue", "Stack"
        };
        
        /// <summary>
        /// Gets the mapping from C# types to C++ types.
        /// </summary>
        public static Dictionary<string, string> TypeMap { get; } = new Dictionary<string, string>
        {
            // Fundamental types that match C++
            {"int", "int"},
            {"long", "long"},
            {"short", "short"},
            {"byte", "unsigned char"},
            {"uint", "unsigned int"},
            {"ulong", "unsigned long"},
            {"ushort", "unsigned short"},
            {"sbyte", "char"},
            {"float", "float"},
            {"double", "double"},
            {"decimal", "double"}, // Note: C# decimal is 128-bit, C++ double is 64-bit
            {"bool", "bool"},
            // String type
            {"string", "std::string"},
            {"void", "void"},
            // Our custom primitive types
            {"object", "Object"},
            {"String", "String"},
            {"Int32", "Int32"},
            {"Boolean", "Boolean"},
            {"Double", "Double"},
            // Collection types
            {"List", "db::List"},
            {"Dictionary", "db::Dictionary"},
            {"HashSet", "std::unordered_set"},
            {"Queue", "std::queue"},
            {"Stack", "std::stack"}
        };
        
        /// <summary>
        /// Gets the C++ namespace prefix for standard library types.
        /// </summary>
        public static string StandardNamespace { get; } = "std::";
        
        /// <summary>
        /// Gets the runtime header file for Db C++ code.
        /// </summary>
        public static string RuntimeHeader { get; } = "dbuild.h";
        
        /// <summary>
        /// Gets the namespace used in generated C++ code.
        /// </summary>
        public static string DbNamespace { get; } = "db";
    }
}
