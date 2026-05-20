using System;
using System.Collections.Generic;
using System.Linq;

namespace taste.Parse.Db
{
    /// <summary>
    /// Performs semantic analysis on the parsed CodeFile model.
    /// Validates type existence, inheritance, named constructor arguments,
    /// interface contracts, sum type variants, mixin targets, and type aliases.
    /// </summary>
    public class DbSemanticAnalyzer
    {
        private readonly CodeFile _file;
        private readonly Dictionary<string, Class> _classMap = new Dictionary<string, Class>();
        private readonly HashSet<string> _enumNames = new HashSet<string>();
        private readonly HashSet<string> _interfaceNames = new HashSet<string>();
        private readonly HashSet<string> _sumTypeNames = new HashSet<string>();
        private readonly HashSet<string> _typeAliasNames = new HashSet<string>();
        private readonly Dictionary<string, TypeAliasDeclaration> _typeAliases = new Dictionary<string, TypeAliasDeclaration>();

        public DbSemanticAnalyzer(CodeFile file)
        {
            _file = file;
            
            // Build global maps for quick lookup
            foreach (var ns in file.Namespaces)
                CollectTypes(ns);
            foreach (var cls in file.Classes)
                _classMap[cls.Name] = cls;
            foreach (var en in file.Enums)
                _enumNames.Add(en.Name);
            foreach (var iface in file.Interfaces)
                _interfaceNames.Add(iface.Name);
            foreach (var sum in file.SumTypes)
                _sumTypeNames.Add(sum.Name);
            foreach (var alias in file.TypeAliases)
            {
                _typeAliasNames.Add(alias.Name);
                _typeAliases[alias.Name] = alias;
            }
        }

        private void CollectTypes(Namespace ns)
        {
            foreach (var cls in ns.Classes) _classMap[cls.Name] = cls;
            foreach (var en in ns.Enums) _enumNames.Add(en.Name);
            foreach (var iface in ns.Interfaces) _interfaceNames.Add(iface.Name);
            foreach (var sum in ns.SumTypes) _sumTypeNames.Add(sum.Name);
            foreach (var alias in ns.TypeAliases)
            {
                _typeAliasNames.Add(alias.Name);
                _typeAliases[alias.Name] = alias;
            }
            foreach (var nested in ns.NestedNamespaces) CollectTypes(nested);
        }

        /// <summary>
        /// Validates the model and throws TranspilerException on semantic errors.
        /// </summary>
        public void Analyze()
        {
            // Synthesize classes for `with` composition expressions before any other analysis
            SynthesizeWithCompositions();

            // Validate classes
            foreach (var cls in _classMap.Values)
            {
                ResolveOverloads(cls);
                AnalyzeClass(cls);
            }

            // Validate interfaces
            foreach (var iface in _file.Interfaces)
                AnalyzeInterface(iface);
            foreach (var ns in _file.Namespaces)
                foreach (var iface in ns.Interfaces)
                    AnalyzeInterface(iface);

            // Validate sum types
            foreach (var sum in _file.SumTypes)
                AnalyzeSumType(sum);
            foreach (var ns in _file.Namespaces)
                foreach (var sum in ns.SumTypes)
                    AnalyzeSumType(sum);

            // Validate mixins
            foreach (var mixin in _file.Mixins)
                AnalyzeMixin(mixin);
            foreach (var ns in _file.Namespaces)
                foreach (var mixin in ns.Mixins)
                    AnalyzeMixin(mixin);

            // Validate type aliases
            foreach (var alias in _file.TypeAliases)
                AnalyzeTypeAlias(alias);
            foreach (var ns in _file.Namespaces)
                foreach (var alias in ns.TypeAliases)
                    AnalyzeTypeAlias(alias);
        }

        /// <summary>
        /// Walks every method body in every class looking for variables whose initializer
        /// is a `with` composition expression (represented in the AST as an
        /// <c>ObjectCreationExpression</c> with <c>Type == "$with"</c>).
        /// For each one it synthesises a new <see cref="Class"/> that inherits from all
        /// the component types, registers it in the file and the class map, and rewrites
        /// the variable's <c>Type</c> and <c>Initializer</c> so the emitter produces
        /// valid C++ aggregate-initialisation.
        /// </summary>
        private void SynthesizeWithCompositions()
        {
            var exprParser = new Parse.ExpressionParser();

            // Snapshot the class list so we don't iterate over classes we add mid-walk
            foreach (var cls in _classMap.Values.ToList())
            {
                foreach (var method in cls.Methods)
                {
                    // Build a local scope: variable name → resolved type
                    var localScope = new Dictionary<string, string>();
                    foreach (var f in cls.Fields)
                        localScope[f.Name] = f.Type;
                    foreach (var p in method.Parameters)
                        localScope[p.Name] = p.Type;

                    foreach (var v in method.Variables)
                    {
                        if (!string.IsNullOrWhiteSpace(v.Initializer) && v.Initializer.Contains("with"))
                        {
                            var expr = exprParser.ParseExpression(v.Initializer);
                            if (expr is ObjectCreationExpression withExpr && withExpr.Type == "$with")
                            {
                                // Collect the component type names for each argument
                                var componentTypes = new List<string>();
                                var componentNames = new List<string>();
                                foreach (var arg in withExpr.Arguments)
                                {
                                    if (arg is IdentifierExpression id)
                                    {
                                        componentNames.Add(id.Name);
                                        // Resolve from scope; fall back to treating the identifier as the type name
                                        componentTypes.Add(localScope.TryGetValue(id.Name, out string t) ? t : id.Name);
                                    }
                                }

                                if (componentTypes.Count > 0)
                                {
                                    var typeSet = new HashSet<string>(componentTypes);

                                    // Look for an existing class whose base classes exactly match
                                    // the required component types — no need to synthesise a new one.
                                    string resolvedName = _classMap.Values
                                        .FirstOrDefault(c =>
                                            c.BaseClasses.Count == typeSet.Count &&
                                            c.BaseClasses.All(b => typeSet.Contains(b.Name)))
                                        ?.Name;

                                    if (resolvedName == null)
                                    {
                                        // No matching class found — synthesise one
                                        resolvedName = "_Db_" + string.Join("_", componentTypes);

                                        if (!_classMap.ContainsKey(resolvedName))
                                        {
                                            // Compose a new Class using the existing model —
                                            // no new AST model types required
                                            var synthetic = new Class(resolvedName);
                                            foreach (var t in componentTypes)
                                                synthetic.BaseClasses.Add(new BaseClass(t, false));

                                            _file.Classes.Add(synthetic);
                                            _classMap[resolvedName] = synthetic;
                                        }
                                    }

                                    // Rewrite the variable so the emitter sees the concrete type
                                    // and a valid C++ brace-initialisation for the aggregate
                                    v.Type = resolvedName;
                                    v.Initializer = $"{resolvedName}{{{string.Join(", ", componentNames)}}}";
                                }
                            }
                        }

                        // Track this variable in scope for variables declared after it
                        if (!string.IsNullOrWhiteSpace(v.Type) && v.Type != "var")
                            localScope[v.Name] = v.Type;
                    }
                }
            }
        }

        private void ResolveOverloads(Class cls)
        {
            // Group methods by name, excluding constructors and destructors
            var groups = cls.Methods
                .Where(m => !m.IsConstructor && !m.IsDestructor)
                .GroupBy(m => m.Name);

            foreach (var group in groups)
            {
                if (group.Count() <= 1) continue;

                // We have overloads. We need to rename them based on their parameters.
                // The first one (usually the one with fewest params or the "base" one) stays as is.
                // Others get the suffix.
                var methods = group.ToList();
                
                // Sort by parameter count to keep the simplest one as the primary name
                methods = methods.OrderBy(m => m.Parameters.Count).ToList();

                for (int i = 1; i < methods.Count; i++)
                {
                    var m = methods[i];
                    string suffix = GenerateOverloadSuffix(m.Parameters);
                    m.Name = m.Name + suffix;
                }
            }
        }

        private string GenerateOverloadSuffix(List<Parameter> parameters)
        {
            if (parameters == null || parameters.Count == 0) return "_void";
            
            var typeNames = parameters.Select(p => p.Type).ToList();
            return "_" + string.Join("_", typeNames);
        }

        private void AnalyzeClass(Class cls)
        {
            foreach (var method in cls.Methods)
            {
                // We only care about variables declared inside methods for named-arg validation
                foreach (var v in method.Variables)
                {
                    if (v.IsNewObject && v.NamedArgs.Count > 0)
                    {
                        ValidateNamedArguments(v, cls);
                    }
                }
            }

            // Validate companion object
            if (cls.Companion != null)
            {
                foreach (var m in cls.Companion.Methods)
                {
                    // Companion methods should be static-like; no further validation needed
                    // but we could check for duplicate names with the class itself
                }
            }
        }

        private void AnalyzeInterface(InterfaceDeclaration iface)
        {
            // Validate that extended interfaces exist
            foreach (var ext in iface.Extends)
            {
                if (!_interfaceNames.Contains(ext) && !_classMap.ContainsKey(ext))
                {
                    // Warning: extended interface/type not found — could be external
                }
            }

            // Validate that abstract/virtual methods have no body (by convention)
            foreach (var m in iface.Methods)
            {
                if (m.IsAbstract && m.Body.Count > 0)
                {
                    throw new TranspilerException(
                        "E020",
                        $"Abstract method '{m.Name}' in interface '{iface.Name}' cannot have a body",
                        "Remove the method body or remove the 'abstract' modifier.",
                        0
                    );
                }
            }
        }

        private void AnalyzeSumType(SumTypeDeclaration sum)
        {
            // Validate that variant data types exist
            foreach (var variant in sum.Variants)
            {
                foreach (var param in variant.Data)
                {
                    if (!IsKnownType(param.Type))
                    {
                        // Warning: unknown type in variant — could be external
                    }
                }
            }
        }

        private void AnalyzeMixin(MixinDeclaration mixin)
        {
            // Validate that the target type exists
            if (!IsKnownType(mixin.TargetType))
            {
                // Warning: target type not found — could be external
            }

            // Validate method signatures
            foreach (var m in mixin.Methods)
            {
                foreach (var p in m.Parameters)
                {
                    if (!IsKnownType(p.Type))
                    {
                        // Warning: unknown parameter type
                    }
                }
            }
        }

        private void AnalyzeTypeAlias(TypeAliasDeclaration alias)
        {
            // Validate that the target type exists
            if (!IsKnownType(alias.TargetType))
            {
                // Warning: target type not found — could be external or a generic
            }
        }

        /// <summary>
        /// Checks if a type name is known (class, struct, enum, interface, sum type, type alias, or built-in).
        /// </summary>
        private bool IsKnownType(string typeName)
        {
            // Strip generic parameters for checking
            string baseName = typeName.Split('<', '[', '&')[0].TrimEnd('*', '&');

            if (_classMap.ContainsKey(baseName)) return true;
            if (_enumNames.Contains(baseName)) return true;
            if (_interfaceNames.Contains(baseName)) return true;
            if (_sumTypeNames.Contains(baseName)) return true;
            if (_typeAliasNames.Contains(baseName)) return true;

            return KnownTypes.IsKnown(baseName);
        }

        private void ValidateNamedArguments(Variable v, Class currentClass)
        {
            // The type being instantiated
            string typeName = v.Type; 
            // Note: In a real parser, the 'new Type()' part would be explicitly captured.
            // For now, we assume the variable type is the target type.

            if (!_classMap.TryGetValue(typeName, out var targetClass))
            {
                // If it's not in our map, it might be a built-in or external type.
                // We can't validate properties for those, so we skip.
                return;
            }

            foreach (var arg in v.NamedArgs)
            {
                if (!PropertyExistsInHierarchy(targetClass, arg.Name))
                {
                    throw new TranspilerException(
                        "E010",
                        $"Property '{arg.Name}' in constructor for {typeName} does not exist",
                        $"Check the spelling of '{arg.Name}' or ensure it is defined in {typeName} or its base classes.",
                        0 // Line number would be passed from Parser in a full impl
                    );
                }
            }
        }

        private bool PropertyExistsInHierarchy(Class cls, string propertyName)
        {
            // Check current class
            if (cls.Properties.Any(p => p.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase)))
                return true;

            // Check base classes recursively
            foreach (var baseRef in cls.BaseClasses)
            {
                if (_classMap.TryGetValue(baseRef.Name, out var baseClass))
                {
                    if (PropertyExistsInHierarchy(baseClass, propertyName))
                        return true;
                }
            }

            return false;
        }

        public bool IsClassType(string typeName)
        {
            if (_enumNames.Contains(typeName)) return false;
            if (_classMap.TryGetValue(typeName, out var cls)) return !cls.IsStruct;
            return true; // Default to class for unknown types
        }

        /// <summary>
        /// Resolves allocation strategies for all fields and local variables in the file,
        /// applying the resolution chain: member attribute → class [MemoryManagement] → project profile → RAII default.
        /// Call this after <see cref="Analyze"/> and before emission.
        /// </summary>
        public void ResolveAllocationStrategies(MemoryProfile projectProfile)
        {
            var profile = projectProfile ?? MemoryProfile.RAII;

            void ResolveClassAllocation(Class cls)
            {
                // Resolve class-level [MemoryManagement] attribute
                var memAttr = cls.Attributes.FirstOrDefault(a => a.Name == "MemoryManagement");
                if (memAttr != null)
                {
                    cls.MemoryManagement = ParseMemoryManagementAttribute(memAttr);
                }

                // Resolve class-level [Stack]/[Heap]/[Shared]/[Raw] attribute
                // (backward compatibility — [Stack] on a struct means stack allocation)
                var classAlloc = SourceAttribute.GetAllocationOverride(cls.Attributes);
                if (classAlloc != AllocationStrategy.Default)
                    cls.Allocation = classAlloc;
                else if (cls.IsStruct)
                    cls.Allocation = AllocationStrategy.Stack;  // structs default to stack

                // Resolve each field
                foreach (var f in cls.Fields)
                {
                    f.Allocation = profile.ResolveField(f.Access, f.Attributes);
                }

                // Resolve each property (backing field follows property allocation)
                foreach (var p in cls.Properties)
                {
                    // Properties don't have Allocation yet, but their backing fields
                    // will follow the same resolution when emitted
                }

                // Resolve each method's local variables
                foreach (var m in cls.Methods)
                {
                    foreach (var v in m.Variables)
                    {
                        v.Allocation = profile.ResolveLocal(v.Attributes);
                    }
                }

                // Resolve companion object members
                if (cls.Companion != null)
                {
                    foreach (var f in cls.Companion.Fields)
                        f.Allocation = profile.ResolveField(AccessModifier.Private, f.Attributes);
                    foreach (var m in cls.Companion.Methods)
                        foreach (var v in m.Variables)
                            v.Allocation = profile.ResolveLocal(v.Attributes);
                }

                // Resolve nested classes
                foreach (var nested in cls.NestedClasses)
                    ResolveClassAllocation(nested);
            }

            foreach (var cls in _file.Classes)
                ResolveClassAllocation(cls);
            foreach (var ns in _file.Namespaces)
                foreach (var cls in ns.Classes)
                    ResolveClassAllocation(cls);
        }

        /// <summary>
        /// Parses a [MemoryManagement(Public=Shared, Private=Unique, Local=Stack)] attribute
        /// into a <see cref="MemoryProfile"/>.
        /// </summary>
        private MemoryProfile ParseMemoryManagementAttribute(SourceAttribute attr)
        {
            var profile = new MemoryProfile();
            foreach (var arg in attr.Arguments)
            {
                // Parse "Public=Shared", "Private=Unique", "Local=Stack"
                var parts = arg.Split('=', 2);
                if (parts.Length == 2)
                {
                    string key = parts[0].Trim();
                    string value = parts[1].Trim();
                    var strategy = SourceAttribute.ParseAllocationStrategy(value);
                    switch (key)
                    {
                        case "Public":  profile.Public  = strategy; break;
                        case "Private": profile.Private = strategy; break;
                        case "Local":   profile.Local   = strategy; break;
                    }
                }
            }
            return profile;
        }

        /// <summary>
        /// Checks for ownership safety issues and emits warnings (not errors).
        /// - Raw pointer ([Raw] or T*) fields without explicit cleanup in the finalizer
        /// - Unique pointer fields without explicit reset in the finalizer
        /// These are warnings because a developer may intentionally leave a pointer
        /// unmanaged (e.g., pointing to memory owned by something else).
        /// </summary>
        public List<string> CheckOwnershipSafety()
        {
            var warnings = new List<string>();

            void CheckClass(Class cls)
            {
                // Collect finalizer cleanup targets (variable/field names that are explicitly freed/reset)
                var finalizerCleanups = new HashSet<string>();
                if (cls.Finalizer != null)
                {
                    foreach (var stmt in cls.Finalizer.Body)
                    {
                        // Look for reset() or delete patterns in action statements
                        if (stmt is Action action)
                        {
                            string raw = action.Raw?.Trim() ?? "";
                            // Match: varname.reset() or delete varname
                            if (raw.Contains(".reset()") || raw.Contains(".reset ("))
                            {
                                var name = raw.Split('.')[0].Trim();
                                finalizerCleanups.Add(name);
                            }
                            else if (raw.StartsWith("delete "))
                            {
                                var name = raw.Substring(7).TrimEnd(' ', ';');
                                finalizerCleanups.Add(name);
                            }
                        }
                    }
                }

                // Check each field for ownership safety
                foreach (var f in cls.Fields)
                {
                    if (f.Allocation == AllocationStrategy.Raw && !finalizerCleanups.Contains(f.Name))
                    {
                        warnings.Add(
                            $"W001: Field '{f.Name}' in '{cls.Name}' is a raw pointer (T*) but is not " +
                            $"explicitly freed in the finalizer. Consider adding cleanup in ~{cls.Name}() " +
                            $"or changing to [Unique]/[Shared].");
                    }
                    else if (f.Allocation == AllocationStrategy.Unique && !finalizerCleanups.Contains(f.Name))
                    {
                        warnings.Add(
                            $"W002: Field '{f.Name}' in '{cls.Name}' is a unique_ptr but is not explicitly " +
                            $"reset in the finalizer. Destruction order will be implicit (reverse declaration order). " +
                            $"Add {f.Name}.reset() in ~{cls.Name}() for deterministic cleanup.");
                    }
                }

                // Check nested classes
                foreach (var nested in cls.NestedClasses)
                    CheckClass(nested);
            }

            foreach (var cls in _file.Classes)
                CheckClass(cls);
            foreach (var ns in _file.Namespaces)
                foreach (var cls in ns.Classes)
                    CheckClass(cls);

            return warnings;
        }
    }
}
