using System.Collections.Generic;
using System.Linq;
using taste.Emit;
using taste.Emit.Cpp;

namespace taste.Emit.Cpp
{
    /// <summary>
    /// Emits C++17/20 source from a parsed <see cref="CodeFile"/>.
    /// Relies on <see cref="CppTypes"/> for the built-in type map.
    /// </summary>
    public class CppEmitter : Emitter
    {
        public CppEmitter() : base(LanguageMatrix.Languages[Language.Cpp]) { }

        /// <summary>
        /// Project-level memory profile. Defaults to RAII (public=Shared, private=Unique, local=Stack).
        /// Can be overridden per-class via [MemoryManagement] attribute.
        /// </summary>
        private MemoryProfile _memoryProfile = MemoryProfile.RAII;

        /// <summary>
        /// Sets the project-level memory profile. Called by the transpiler before emission.
        /// </summary>
        public void SetMemoryProfile(MemoryProfile profile) => _memoryProfile = profile ?? MemoryProfile.RAII;

        // ── Type helpers ───────────────────────────────────────────────────────

        // ── Extension method registry (UFCS dispatch) ──────────────────────────
        // Populated during pre-scan; maps Db method name → C++ expression to call.
        private readonly Dictionary<string, string> _extensionMethods =
            new Dictionary<string, string>();

        private void ScanExtensions(CodeFile file)
        {
            foreach (var ns in file.Namespaces) ScanNamespaceExtensions(ns);
            foreach (var cls in file.Classes)   ScanClassExtensions(cls);
        }

        private void ScanNamespaceExtensions(Namespace ns)
        {
            foreach (var nested in ns.NestedNamespaces) ScanNamespaceExtensions(nested);
            foreach (var cls in ns.Classes)             ScanClassExtensions(cls);
        }

        private void ScanClassExtensions(Class cls)
        {
            if (!cls.IsStatic) return;
            foreach (var m in cls.Methods)
            {
                if (!m.IsStatic || m.Parameters.Count == 0 || m.Parameters[0].Modifier != "this")
                    continue;
                var repr = m.Attributes.FirstOrDefault(a => a.Name == "Represents");
                _extensionMethods[m.Name] = repr != null ? repr.Expression : m.Name;
            }
        }

        // ── Lazy-field template guard ──────────────────────────────────────────
        private bool _lazyTemplateEmitted = false;

        private void EnsureLazyTemplate()
        {
            if (_lazyTemplateEmitted) return;
            _lazyTemplateEmitted = true;
            WriteLine("// ── db_lazy_field ──────────────────────────────────────────────────────");
            WriteLine("template<typename F>");
            WriteLine("struct db_lazy_field {");
            Indent();
            WriteLine("using value_type = std::invoke_result_t<F>;");
            WriteLine("mutable std::optional<value_type> _v;");
            WriteLine("F _factory;");
            WriteLine("explicit db_lazy_field(F f) : _factory(std::move(f)) {}");
            WriteLine("value_type& get() const { if (!_v) _v = _factory(); return *_v; }");
            WriteLine("operator value_type&()      const { return get(); }");
            WriteLine("value_type* operator->()   const { return &get(); }");
            WriteLine("value_type& operator*()    const { return get(); }");
            Dedent();
            WriteLine("};");
            WriteLine("template<typename F> db_lazy_field(F) -> db_lazy_field<std::decay_t<F>>;");
            WriteLine();
        }

        private static string MapType(string csType)
        {
            // Tuple return type: (Type1, Type2) → std::tuple<Type1, Type2>
            if (csType.StartsWith("(") && csType.EndsWith(")"))
            {
                string inner  = csType.Substring(1, csType.Length - 2);
                string mapped = string.Join(", ", inner.Split(',').Select(t => MapType(t.Trim())));
                return $"std::tuple<{mapped}>";
            }
            if (CppTypes.TypeMap.TryGetValue(csType, out var mapped2)) return mapped2;
            return csType;
        }

        /// <summary>
        /// Applies type decoration to a C++ type: T* for [Address], T&amp; for [Reference], T for [Naked].
        /// </summary>
        private static string DecorateType(string cppType, TypeDecoration decoration)
        {
            return decoration switch
            {
                TypeDecoration.Address    => $"{cppType}*",
                TypeDecoration.Reference  => $"{cppType}&",
                TypeDecoration.Naked      => cppType,
                _                         => cppType
            };
        }

        /// <summary>
        /// Extracts the TypeDecoration from a list of attributes (looks for [Address], [Reference], [Naked]).
        /// </summary>
        private static TypeDecoration GetTypeDecoration(List<SourceAttribute> attributes)
        {
            var attr = attributes.FirstOrDefault(a => a.Name == "Address" || a.Name == "Reference" || a.Name == "Naked");
            return attr?.Decoration ?? TypeDecoration.None;
        }

        /// <summary>
        /// Resolves the effective allocation strategy for a field, applying the resolution chain:
        /// member attribute → class [MemoryManagement] → project profile → RAII default.
        /// </summary>
        private AllocationStrategy ResolveFieldAllocation(Field field, Class cls)
        {
            // 1. Member-level attribute override ([Stack], [Heap], [Shared], [Raw])
            var attrOverride = SourceAttribute.GetAllocationOverride(field.Attributes);
            if (attrOverride != AllocationStrategy.Default)
                return attrOverride;

            // 2. Type decoration overrides ([Address] → Raw, [Reference] → Stack, [Naked] → Stack)
            var decoration = SourceAttribute.GetTypeDecoration(field.Attributes);
            if (decoration == TypeDecoration.Address)
                return AllocationStrategy.Raw;
            if (decoration == TypeDecoration.Reference || decoration == TypeDecoration.Naked)
                return AllocationStrategy.Stack;

            // 3. Class-level [MemoryManagement] override
            if (cls.MemoryManagement != null)
                return cls.MemoryManagement.ResolveField(field.Access, field.Attributes);

            // 4. Project-level profile (falls back to RAII default)
            return _memoryProfile.ResolveField(field.Access, field.Attributes);
        }

        /// <summary>
        /// Resolves the effective allocation strategy for a local variable.
        /// </summary>
        private AllocationStrategy ResolveLocalAllocation(Variable v, Class cls)
        {
            // 1. Variable-level attribute override
            var attrOverride = SourceAttribute.GetAllocationOverride(v.Attributes);
            if (attrOverride != AllocationStrategy.Default)
                return attrOverride;

            // 2. Class-level [MemoryManagement] override
            if (cls != null && cls.MemoryManagement != null)
                return cls.MemoryManagement.ResolveLocal(v.Attributes);

            // 3. Project-level profile
            return _memoryProfile.Local;
        }

        /// <summary>
        /// Emits a C++ type declaration for a field based on its allocation strategy.
        /// </summary>
        private string ApplyAllocationToFieldType(string cppType, AllocationStrategy allocation)
        {
            return allocation switch
            {
                AllocationStrategy.Stack   => cppType,
                AllocationStrategy.Unique   => $"std::unique_ptr<{cppType}>",
                AllocationStrategy.Shared   => $"std::shared_ptr<{cppType}>",
                AllocationStrategy.Raw      => $"{cppType}*",
                _                           => cppType  // Default: use bare type (will be resolved later)
            };
        }

        /// <summary>
        /// Emits the member access operator based on allocation strategy.
        /// </summary>
        private string MemberAccessFor(AllocationStrategy allocation)
        {
            return allocation switch
            {
                AllocationStrategy.Stack => ".",
                AllocationStrategy.Unique => "->",
                AllocationStrategy.Shared => "->",
                AllocationStrategy.Raw    => "->",
                _                         => "."
            };
        }

        private static string MapAccess(AccessModifier access) => access switch
        {
            AccessModifier.Public    => "public",
            AccessModifier.Protected => "protected",
            AccessModifier.Internal  => "public",   // internal → public in C++
            _                        => "private",
        };

        /// <summary>
        /// MapAccess using the LanguageProfile — consults Profile.AccessPublic etc.
        /// Falls back to the static MapAccess for C++-specific conventions.
        /// </summary>
        private string ProfileAccess(AccessModifier access) => access switch
        {
            AccessModifier.Public    => Profile.AccessPublic,
            AccessModifier.Protected => Profile.AccessProtected,
            AccessModifier.Internal  => Profile.AccessPublic,   // internal → public in C++
            _                        => Profile.AccessPrivate,
        };

        // ── File-level ─────────────────────────────────────────────────────────

        protected override void WriteFileHeader(CodeFile file)
        {
            ScanExtensions(file);

            // Auto-include headers based on features used in the file
            bool hasAsync = HasAsyncMethods(file);
            if (hasAsync)
            {
                WriteLine("#include <future>");
                WriteLine("#include <coroutine>");
            }

            WriteLine();
            WriteLine();
        }

        /// <summary>Scans the file for any async methods that require coroutine headers.</summary>
        private bool HasAsyncMethods(CodeFile file)
        {
            foreach (var ns in file.Namespaces)
                if (HasAsyncMethods(ns)) return true;
            foreach (var cls in file.Classes)
                if (HasAsyncMethods(cls)) return true;
            return false;
        }

        private bool HasAsyncMethods(Namespace ns)
        {
            foreach (var nested in ns.NestedNamespaces)
                if (HasAsyncMethods(nested)) return true;
            foreach (var cls in ns.Classes)
                if (HasAsyncMethods(cls)) return true;
            return false;
        }

        private bool HasAsyncMethods(Class cls)
        {
            foreach (var m in cls.Methods)
                if (m.IsAsync) return true;
            foreach (var nested in cls.NestedClasses)
                if (HasAsyncMethods(nested)) return true;
            return false;
        }

        protected override void WriteUsing(Using u)
        {
            // C# using directives have no C++ equivalent at file scope — skip.
        }

        protected override void WriteNamespace(Namespace ns)
        {
            WriteLine($"{KeywordFor(CodePart.Namespace)} {ns.Name} {{");
            Indent();
            foreach (var nested in ns.NestedNamespaces) WriteNamespace(nested);
            foreach (var d in ns.Delegates)             WriteDelegate(d);
            foreach (var e in ns.Enums)                 WriteEnum(e);
            foreach (var i in ns.Interfaces)            WriteInterface(i);
            foreach (var s in ns.SumTypes)              WriteSumType(s);
            foreach (var m in ns.Mixins)                WriteMixin(m);
            foreach (var a in ns.TypeAliases)           WriteTypeAlias(a);
            foreach (var cls in ns.Classes)             WriteClass(cls);
            Dedent();
            WriteLine($"}} // {KeywordFor(CodePart.Namespace)} {ns.Name}");
            WriteLine();
        }
        // ── Delegates & events ──────────────────────────────────────────────────

        protected override void WriteDelegate(DelegateDecl d)
        {
            // public delegate void OnDamage(int amount)
            // → using OnDamage = std::function<void(int)>;
            string retType    = MapType(d.ReturnType);
            string paramTypes = string.Join(", ", d.Parameters.Select(p => MapType(p.Type)));
            string template = TemplateFor(CodePart.Delegate);
            if (template != null)
            {
                // Template: "using {0} = std::function<{1}({2})>"
                string line = template.Replace("{0}", d.Name).Replace("{1}", retType).Replace("{2}", paramTypes);
                WriteLine(line + StatementTerminator);
            }
            else
            {
                WriteLine($"using {d.Name} = std::function<{retType}({paramTypes})>{StatementTerminator}");
            }
        }

        protected override void WriteEvent(Event ev)
        {
            if (ev.IsMulticast)
            {
                // Multicast: backing list + add/remove/invoke helpers
                WriteLine($"db::List<{ev.DelegateType}> _{char.ToLower(ev.Name[0])}{ev.Name.Substring(1)};");
                WriteLine($"void Add{ev.Name}({ev.DelegateType} handler)    {{ _{char.ToLower(ev.Name[0])}{ev.Name.Substring(1)}.Add(handler); }}");
                WriteLine($"void Remove{ev.Name}({ev.DelegateType} handler) {{ _{char.ToLower(ev.Name[0])}{ev.Name.Substring(1)}.Remove(handler); }}");
                // Invoke: forward all args through — use a variadic template helper
                WriteLine($"template<typename... Args>");
                WriteLine($"void Invoke{ev.Name}(Args&&... args) {{");
                Indent();
                WriteLine($"for (auto& h : _{char.ToLower(ev.Name[0])}{ev.Name.Substring(1)}) h(std::forward<Args>(args)...);");
                Dedent();
                WriteLine("}");
            }
            else
            {
                // Singlecast: plain std::function field
                WriteLine($"{ev.DelegateType} {ev.Name};");
            }
        }
        // ── Enums ──────────────────────────────────────────────────────────────

        protected override void WriteEnum(EnumDecl e)
        {
            string kw = KeywordFor(CodePart.Enum);
            WriteLine($"{kw} class {e.Name}");
            WriteLine("{");
            Indent();
            for (int i = 0; i < e.Members.Count; i++)
            {
                string comma = i < e.Members.Count - 1 ? "," : "";
                WriteLine(e.Members[i] + comma);
            }
            Dedent();
            WriteLine($"}}{StatementTerminator}");
            WriteLine();
        }

        // ── Classes ────────────────────────────────────────────────────────────

        protected override void WriteClass(Class cls)
        {
            // Static classes are extension-method containers: their methods are dispatched
            // via UFCS at call sites using the pre-scanned _extensionMethods table.
            // Nothing needs to be emitted for the class declaration itself.
            if (cls.IsStatic) return;

            // Class declaration line
            string keyword     = cls.IsStruct ? "struct" : "class";
            string inheritance = cls.BaseClasses.Count == 0 ? "" :
                " : " + string.Join(", ", cls.BaseClasses.Select(b =>
                    "public " + (b.IsInterface ? b.Name.Substring(1) : b.Name)));

            string typeParams = cls.TypeParams.Count == 0 ? "" :
                $"template<{string.Join(", ", cls.TypeParams.Select(t => "typename " + t))}>\n";

            string sealedStr = cls.IsSealed ? " final" : "";
            WriteLine(typeParams + $"{keyword} {cls.Name}{inheritance}{sealedStr}");
            WriteLine("{");

            // ── private ───────────────────────────────────────────────────────
            var privateConstants = cls.Constants.Where(c => c.Access == AccessModifier.Private).ToList();
            var privateMembers  = cls.Fields.Where(f => f.Access == AccessModifier.Private).ToList();
            var privatePropBacking = cls.Properties.ToList(); // backing fields always private
            var privateEvents   = cls.Events.ToList();        // event backing lists always private
            bool hasPrivate = privateConstants.Count > 0 || privateMembers.Count > 0 || privatePropBacking.Count > 0
                           || privateEvents.Count > 0 || cls.FriendClasses.Count > 0;

            if (hasPrivate)
            {
                WriteLine("private:");
                Indent();
                foreach (var fc in cls.FriendClasses)   WriteLine($"friend class {fc};");
                foreach (var c in privateConstants)    WriteConstant(c);
                foreach (var f in privateMembers)        WriteField(f);
                foreach (var p in privatePropBacking)    WritePropertyBacking(p);
                foreach (var ev in privateEvents.Where(e => e.IsMulticast))
                    WriteLine($"db::List<{ev.DelegateType}> _{char.ToLower(ev.Name[0])}{ev.Name.Substring(1)};");
                Dedent();
            }

            // ── protected ─────────────────────────────────────────────────────
            var protectedFields = cls.Fields.Where(f => f.Access == AccessModifier.Protected).ToList();
            if (protectedFields.Count > 0)
            {
                WriteLine("protected:");
                Indent();
                foreach (var f in protectedFields) WriteField(f);
                Dedent();
            }

            // ── public ────────────────────────────────────────────────────────
            var publicConstants = cls.Constants.Where(c => c.Access == AccessModifier.Public
                                                         || c.Access == AccessModifier.Internal).ToList();
            var publicFields   = cls.Fields.Where(f => f.Access == AccessModifier.Public).ToList();
            var publicProps    = cls.Properties.Where(p => p.Access == AccessModifier.Public
                                                        || p.Access == AccessModifier.Internal).ToList();
            var publicEvents   = cls.Events.Where(e => e.Access == AccessModifier.Public
                                                    || e.Access == AccessModifier.Internal).ToList();
            var publicMethods  = cls.Methods.Where(m => m.Access == AccessModifier.Public
                                                     || m.Access == AccessModifier.Internal).ToList();
            var publicOperators = cls.Operators.Where(o => o.Access == AccessModifier.Public
                                                     || o.Access == AccessModifier.Internal).ToList();
            bool hasPublic = publicConstants.Count > 0 || publicFields.Count > 0 || publicProps.Count > 0
                          || publicEvents.Count > 0  || publicMethods.Count > 0
                          || publicOperators.Count > 0;

            if (hasPublic)
            {
                WriteLine("public:");
                Indent();
                foreach (var c in publicConstants) WriteConstant(c);
                foreach (var f in publicFields)   WriteField(f);
                foreach (var p in publicProps)     WriteProperty(p);
                foreach (var ev in publicEvents)   WriteEvent(ev);
                
                // [Stack] Struct Logic: If it's a [Stack] struct, we don't emit methods inside the struct
                if (cls.IsStruct && cls.Allocation == AllocationStrategy.Stack)
                {
                    // Methods are skipped here and emitted as free functions later
                }
                else
                {
                    foreach (var m in publicMethods)   WriteMethod(m);
                }
                foreach (var o in publicOperators)    WriteOperatorOverload(o);
                Dedent();
            }

            // private methods last
            var privateMethods = cls.Methods.Where(m => m.Access == AccessModifier.Private).ToList();
            if (privateMethods.Count > 0)
            {
                WriteLine("private:");
                Indent();
                if (cls.IsStruct && cls.Allocation == AllocationStrategy.Stack)
                {
                    // Skip methods
                }
                else
                {
                    foreach (var m in privateMethods) WriteMethod(m);
                }
                Dedent();
            }

            // nested types
            foreach (var d in cls.NestedDelegates)   WriteDelegate(d);
            foreach (var nested in cls.NestedClasses) WriteClass(nested);
            foreach (var nested in cls.NestedEnums)   WriteEnum(nested);

            // finalizer (C# ~ClassName() → C++ destructor)
            if (cls.Finalizer != null)
            {
                WriteLine($"~{cls.Name}()");
                WriteMethodBody(cls.Finalizer);
            }

            // companion object (Kotlin-style static container)
            if (cls.Companion != null)
            {
                string companionName = cls.Companion.Name ?? "Companion";
                WriteLine($"struct {companionName}");
                WriteLine("{");
                Indent();
                foreach (var c in cls.Companion.Constants) WriteConstant(c);
                foreach (var f in cls.Companion.Fields)    WriteField(f);
                foreach (var p in cls.Companion.Properties) WriteProperty(p);
                foreach (var m in cls.Companion.Methods)    WriteMethod(m);
                Dedent();
                WriteLine("};");
            }

            WriteLine("};");
            WriteLine();

            // ── [Stack] Struct Method Extraction ──────────────────────────────
            if (cls.IsStruct && cls.Allocation == AllocationStrategy.Stack)
            {
                // Emit all methods as free functions: inline void Class_Method(Class& _this, ...)
                foreach (var m in cls.Methods)
                {
                    WriteStackStructMethod(cls, m);
                }
            }
        }

        private void WriteStackStructMethod(Class cls, Method m)
        {
            string paramList = string.Join(", ", m.Parameters.Select(p =>
            {
                string t = MapType(p.Type);
                if (p.IsParams)
                {
                    string elemType = t.EndsWith("[]") ? t.Substring(0, t.Length - 2) : t;
                    t = $"std::initializer_list<{elemType}>";
                }
                else if (p.Modifier == "out" || p.Modifier == "ref")
                    t = $"{t}&";
                else if (p.Modifier == "in")
                    t = $"const {t}&";
                else if (t.StartsWith("std::string") || t.StartsWith("db::") || t.StartsWith("std::unordered"))
                    t = $"const {t}&";
                return $"{t} {p.Name}";
            }));

            string retType = m.IsConstructor || m.IsDestructor ? "" : MapType(m.ReturnType) + " ";
            
            // The "this" pointer for stack structs
            string thisParam = $"{MapType(cls.Name)}& _this";
            string fullParams = string.IsNullOrEmpty(paramList) ? thisParam : $"{thisParam}, {paramList}";

            WriteLine($"inline {retType}{cls.Name}_{m.Name}({fullParams})");
            WriteMethodBody(m);
        }

        // ── Constants, fields & properties ─────────────────────────────────────

        protected override void WriteConstant(Constant c)
        {
            // const int Max = 100 → static constexpr int Max = 100;
            string cppType = MapType(c.Type);
            var decoration = GetTypeDecoration(c.Attributes);
            cppType = DecorateType(cppType, decoration);
            string kw = KeywordFor(CodePart.Constant);
            WriteLine($"static {kw} {cppType} {c.Name} = {c.Value}{StatementTerminator}");
        }

        protected override void WriteField(Field field)
        {
            if (field.IsLazy)
            {
                EnsureLazyTemplate();
                string expr = field.Initializer ?? "{}";
                // Always use CTAD — the factory lambda's return type drives T.
                WriteLine($"db_lazy_field {field.Name}{{[this]{{ return {expr}; }}}};");
                return;
            }

            string cppType = MapType(field.Type);

            // ── Memory management: apply allocation strategy ──────────────────
            // Resolve the allocation strategy for this field based on the
            // resolution chain: member attribute → class [MemoryManagement] → project profile.
            // Note: We need the enclosing class to resolve properly, but WriteField
            // doesn't receive it. The Allocation field is pre-resolved by the
            // semantic analyzer. If still Default, apply scope-based defaults here.
            AllocationStrategy alloc = field.Allocation;
            if (alloc == AllocationStrategy.Default)
            {
                // Fallback: use scope-based default from project profile
                alloc = _memoryProfile.ResolveField(field.Access, field.Attributes);
            }

            // Type decoration ([Address], [Reference], [Naked]) overrides allocation
            var decoration = GetTypeDecoration(field.Attributes);
            if (decoration == TypeDecoration.Address)
                alloc = AllocationStrategy.Raw;
            else if (decoration == TypeDecoration.Reference || decoration == TypeDecoration.Naked)
                alloc = AllocationStrategy.Stack;

            // Apply allocation wrapping to the type
            cppType = ApplyAllocationToFieldType(cppType, alloc);
            // Type decoration is applied AFTER allocation wrapping for pointer/reference
            // (e.g., [Address] on a Shared field → std::shared_ptr<T>* is wrong,
            //  but [Address] alone → T* is correct, handled above by forcing Raw)
            if (decoration != TypeDecoration.None && alloc != AllocationStrategy.Raw && alloc != AllocationStrategy.Stack)
                cppType = DecorateType(cppType, decoration);

            string prefix  = "";
            if (field.IsStatic)   prefix += "static ";
            if (field.IsReadonly) prefix += "const ";  // readonly → const (assigned only in ctor)
            // [Mutable] attribute → mutable qualifier
            if (field.Attributes.Any(a => a.Name == "Mutable"))
                prefix += "mutable ";

            // Initializer: smart pointers use make_unique/make_shared, stack uses direct init
            string init = "";
            if (field.Initializer != null)
            {
                if (alloc == AllocationStrategy.Unique)
                    init = $" = std::make_unique<{MapType(field.Type)}>({field.Initializer.TrimStart('(', ' ').TrimEnd(')', ' ')})";
                else if (alloc == AllocationStrategy.Shared)
                    init = $" = std::make_shared<{MapType(field.Type)}>({field.Initializer.TrimStart('(', ' ').TrimEnd(')', ' ')})";
                else
                    init = $" = {field.Initializer}";
            }
            WriteLine($"{prefix}{cppType} {field.Name}{init};");
        }

        /// <summary>Writes the private backing field for a property.</summary>
        private void WritePropertyBacking(Property prop)
        {
            // Indexers don't need a backing field
            if (prop.IsIndexer) return;
            
            string cppType  = MapType(prop.Type);
            string backing  = "_" + char.ToLower(prop.Name[0]) + prop.Name.Substring(1);
            WriteLine($"{cppType} {backing};");
        }

        protected override void WriteProperty(Property prop)
        {
            string cppType  = MapType(prop.Type);
            var decoration = GetTypeDecoration(prop.Attributes);
            cppType = DecorateType(cppType, decoration);

            // Indexer: emit as operator[]
            if (prop.IsIndexer)
            {
                string paramStr = string.Join(", ", prop.IndexerParameters.Select(p => $"{MapType(p.Type)} {p.Name}"));
                string indexParam = prop.IndexerParameters.FirstOrDefault()?.Name ?? "index";
                SourceAttribute? idxRepresentsAttr = prop.Attributes.FirstOrDefault(a => a.Name == "Represents");
                string? idxRepresented = idxRepresentsAttr?.Arguments.FirstOrDefault();
                
                if (prop.HasGetter)
                    WriteLine($"{cppType}& operator[]({paramStr}) {{ return (*this)[{indexParam}]; }}");
                if (prop.HasSetter)
                    WriteLine($"const {cppType}& operator[]({paramStr}) const {{ return (*this)[{indexParam}]; }}");
                return;
            }

            SourceAttribute? representsAttr = prop.Attributes.FirstOrDefault(a => a.Name == "Represents");
            string? represented = representsAttr?.Arguments.FirstOrDefault();
            MemberAccess access = representsAttr?.Access ?? MemberAccess.Dot;

            // Expression-bodied property: public int Count => expr;
            // Emits as: int Count() const { return expr; }
            if (prop.IsExpressionBodied && prop.Initializer != null)
            {
                string virt = prop.IsVirtual ? "virtual " : "";
                string ovr = prop.IsOverride ? " override" : "";
                string stat = prop.IsStatic ? "static " : "";
                string cst = prop.Attributes.Any(a => a.Name == "Const") ? " const" : "";
                WriteLine($"{stat}{virt}{cppType} {prop.Name}() const{ovr}{cst} {{ return {prop.Initializer}; }}");
                return;
            }

            if (represented != null && prop.HasGetter && !prop.HasSetter)
            {
                string virt = prop.IsVirtual ? "virtual " : "";
                string ovr = prop.IsOverride ? " override" : "";
                string stat = prop.IsStatic ? "static " : "";
                // [Const] attribute → const qualifier on getter.
                // Getter-only [Represents] properties already emit const, so skip duplicate.
                bool hasImplicitConst = represented != null && prop.HasGetter && !prop.HasSetter;
                string cst = (!hasImplicitConst && prop.Attributes.Any(a => a.Name == "Const")) ? " const" : "";

                // Build the C++ expression based on access pattern
                string cppExpr = BuildRepresentedExpression(represented, access, prop.IsStatic);

                WriteLine($"{stat}{virt}{cppType} {prop.Name}() const{ovr}{cst} {{ return {cppExpr}; }}");
            }
            else if (represented != null && prop.HasSetter && !prop.HasGetter)
            {
                string virt = prop.IsVirtual ? "virtual " : "";
                string ovr = prop.IsOverride ? " override" : "";
                string stat = prop.IsStatic ? "static " : "";
                // [Noexcept] attribute → noexcept specifier
                string noex = prop.Attributes.Any(a => a.Name == "Noexcept") ? " noexcept" : "";

                string cppExpr = BuildRepresentedExpression(represented, access, prop.IsStatic);

                WriteLine($"{stat}{virt}void set{prop.Name}({cppType} value){ovr}{noex} {{ {cppExpr} = value; }}");
            }
            else
            {
                string backing  = "_" + char.ToLower(prop.Name[0]) + prop.Name.Substring(1);
                string prefix = "";
                if (prop.IsStatic) prefix += "static ";
                WriteLine($"{prefix}{cppType} {backing};");
                string virt = prop.IsVirtual ? "virtual " : "";
                string ovr = prop.IsOverride ? " override" : "";
                string stat = prop.IsStatic ? "static " : "";
                if (prop.HasGetter)
                    WriteLine($"{stat}{virt}{cppType} get{prop.Name}() const{ovr} {{ return {backing}; }}");
                if (prop.HasSetter)
                    WriteLine($"{stat}{virt}void set{prop.Name}({cppType} value){ovr} {{ this->{backing} = value; }}");
            }
        }

        /// <summary>
        /// Builds a C++ expression from a [Represents] string and MemberAccess pattern.
        /// - Colons (::): Emit the full qualified path as-is (e.g., "std::numeric_limits<int>::max()")
        /// - Dot (.): Instance member access → "value.member" (e.g., "size()" → "value.size()")
        /// - Arrow (->): Pointer access → "value->member" (e.g., "size()" → "value->size()")
        /// - DotAsterisk (.*): Pointer-to-member → "value.*member"
        /// - ArrowAsterisk (->*): Pointer-to-member-arrow → "value->*member"
        /// </summary>
        private string BuildRepresentedExpression(string represented, MemberAccess access, bool isStatic)
        {
            switch (access)
            {
                case MemberAccess.Colons:
                    // Static/qualified access — emit as-is
                    return represented;

                case MemberAccess.Arrow:
                    // Pointer/smart pointer access
                    return $"value->{represented}";

                case MemberAccess.DotAsterisk:
                    // Pointer-to-member with dot
                    return $"value.*{represented}";

                case MemberAccess.ArrowAsterisk:
                    // Pointer-to-member with arrow
                    return $"value->*{represented}";

                case MemberAccess.Bracket:
                    // Subscript operator
                    return $"value[{represented}]";

                case MemberAccess.QuestionMarkDot:
                    // Null-conditional dot
                    return $"value?.{represented}";

                case MemberAccess.QuestionMarkBracket:
                    // Null-conditional bracket
                    return $"value?[{represented}]";

                case MemberAccess.Colon:
                    // Single colon (slicing/dictionaries)
                    return $"value:{represented}";

                case MemberAccess.Dot:
                default:
                    // Instance member access
                    return $"value.{represented}";
            }
        }

        protected override void WriteIndexer(Indexer idx)
        {
            string elemType = MapType(idx.ElementType);
            string paramStr = string.Join(", ", idx.Parameters.Select(p => $"{MapType(p.Type)} {p.Name}"));
            if (idx.HasGetter)
            {
                // const accessor
                WriteLine($"const {elemType}& operator[]({paramStr}) const");
                if (idx.GetBody.Count > 0) WriteBlock(idx.GetBody);
                else { WriteLine("{"); Indent(); WriteLine("// TODO: getter body"); Dedent(); WriteLine("}"); }
            }
            if (idx.HasSetter)
            {
                // mutable accessor
                WriteLine($"{elemType}& operator[]({paramStr})");
                if (idx.SetBody.Count > 0) WriteBlock(idx.SetBody);
                else { WriteLine("{"); Indent(); WriteLine("// TODO: setter body"); Dedent(); WriteLine("}"); }
            }
        }

        protected override void WriteOperatorOverload(OperatorOverload op)
        {
            string retType  = DecorateType(MapType(op.ReturnType), GetTypeDecoration(op.Attributes));
            string paramStr = string.Join(", ", op.Parameters.Select(p => {
                string t = MapType(p.Type);
                if (p.Modifier == "out" || p.Modifier == "ref")
                    t = $"{t}&";
                else if (p.Modifier == "in")
                    t = $"const {t}&";
                return $"{t} {p.Name}";
            }));
            WriteLine($"friend {retType} operator{op.Operator}({paramStr})");
            WriteBlock(op.Body);
        }

        // ── Methods ────────────────────────────────────────────────────────────

        protected override void WriteMethod(Method method)
        {
            string paramList = string.Join(", ", method.Parameters.Select(p =>
            {
                string t = MapType(p.Type);
                if (p.IsParams)
                {
                    // params T[] arr → std::initializer_list<T> arr
                    // Strip array brackets from the type if present
                    string elemType = t.EndsWith("[]") ? t.Substring(0, t.Length - 2) : t;
                    t = $"std::initializer_list<{elemType}>";
                }
                else if (p.Modifier == "out" || p.Modifier == "ref")
                    t = $"{t}&";
                else if (p.Modifier == "in")
                    t = $"const {t}&";
                else if (t.StartsWith("std::string") || t.StartsWith("db::") || t.StartsWith("std::unordered"))
                    t = $"const {t}&";
                string defaultVal = p.Default != null ? $" = {p.Default}" : "";
                return $"{t} {p.Name}{defaultVal}";
            }));

            if (method.TypeParams.Count > 0)
                WriteLine($"template<{string.Join(", ", method.TypeParams.Select(t => "typename " + t))}>");

            // Build modifier prefix
            string prefix = "";
            if (method.IsStatic)   prefix += "static ";
            if (method.IsVirtual || method.IsAbstract) prefix += "virtual ";
            // [New] → name hiding is implicit in C++; no keyword needed
            if (method.IsNew) prefix += "";
            
            // [Explicit] attribute → explicit constructor
            if (method.IsConstructor && method.Attributes.Any(a => a.Name == "Explicit"))
                prefix += "explicit ";
            // Check for [Inline] attribute
            if (method.Attributes.Any(a => a.Name == "Inline"))
            {
                prefix += "inline ";
            }

            // Build const/noexcept suffix
            string suffix = "";
            if (method.IsOverride) suffix += " override";
            if (method.IsSealed)   suffix += " final";
            if (method.IsAbstract) suffix += " = 0";
            // [Const] attribute → const method qualifier
            if (method.Attributes.Any(a => a.Name == "Const"))
                suffix += " const";
            // [Noexcept] attribute → noexcept specifier
            if (method.Attributes.Any(a => a.Name == "Noexcept"))
                suffix += " noexcept";

            // Async methods → C++20 coroutines: wrap return type in std::future<>,
            // use co_return instead of return, and co_await for awaits.
            string retType;
            if (method.IsAsync && !method.IsConstructor && !method.IsDestructor)
            {
                string innerType = DecorateType(MapType(method.ReturnType), GetTypeDecoration(method.Attributes));
                retType = method.ReturnType == "void"
                    ? "std::future<void> "
                    : $"std::future<{innerType}> ";
            }
            else
            {
                retType = method.IsConstructor || method.IsDestructor ? "" : DecorateType(MapType(method.ReturnType), GetTypeDecoration(method.Attributes)) + " ";
            }
            string nameToken = method.IsDestructor ? $"~{method.Name}" : method.Name;

            string signature = $"{prefix}{retType}{nameToken}({paramList}){suffix}";

            // Constructor initializer list
            if (method.IsConstructor && method.CtorInitializers.Count > 0)
            {
                string initList = string.Join(", ",
                    method.CtorInitializers.Select(ci => $"{ci.MemberName}({ci.Expression})"));
                signature += $"\n    : {initList}";
            }

            // Pure virtual / extern declarations: no body
            if (method.IsAbstract || method.IsExtern)
            {
                WriteLine(signature + ";");
                return;
            }

            WriteLine(signature);
            WriteMethodBody(method);
        }

        private void WriteMethodBody(Method method)
        {
            WriteLine("{");
            Indent();
            if (method.IsAsync)
                WriteLine("// [async] Use co_return instead of return, and co_await for awaiting.");
            foreach (var v in method.Variables) WriteVariable(v);
            foreach (var s in method.Body)      WriteStatement(s);
            Dedent();
            WriteLine("}");
        }

        protected override void WriteParameter(Parameter param)
        {
            // Parameters are inlined in WriteMethod; this is here for completeness.
            Write($"{MapType(param.Type)} {param.Name}");
        }

        // ── Method body ────────────────────────────────────────────────────────

        protected override void WriteVariable(Variable v)
        {
            string cppType = MapType(v.Type);

            // ── Memory management: resolve allocation strategy ───────────────
            AllocationStrategy alloc = v.Allocation;
            if (alloc == AllocationStrategy.Default)
                alloc = _memoryProfile.Local;  // Method-level locals default to stack

            if (v.IsNewObject)
            {
                string args = v.ConstructorArgs ?? "";

                // Handle Named Parameter Initialization (Fluent Setters)
                if (v.NamedArgs.Count > 0)
                {
                    // Fluent setters always use shared_ptr for now (setter chains need ->)
                    WriteLine($"auto {v.Name} = std::make_shared<{cppType}>({args});");
                    string setterChain = string.Join("->", v.NamedArgs.Select(a => $"set{a.Name}({a.Value})"));
                    WriteLine($"{v.Name}->{setterChain};");
                }
                else
                {
                    // Emit based on allocation strategy
                    switch (alloc)
                    {
                        case AllocationStrategy.Stack:
                            // Stack allocation: Type name(args);
                            WriteLine($"{cppType} {v.Name}({args});");
                            break;
                        case AllocationStrategy.Unique:
                            // Unique ownership: auto name = std::make_unique<Type>(args);
                            WriteLine($"auto {v.Name} = std::make_unique<{cppType}>({args});");
                            break;
                        case AllocationStrategy.Shared:
                            // Shared ownership: auto name = std::make_shared<Type>(args);
                            WriteLine($"auto {v.Name} = std::make_shared<{cppType}>({args});");
                            break;
                        case AllocationStrategy.Raw:
                            // Raw pointer: Type* name = new Type(args);
                            WriteLine($"{cppType}* {v.Name} = new {cppType}({args});");
                            break;
                        default:
                            // Default: stack allocation (RAII-first)
                            WriteLine($"{cppType} {v.Name}({args});");
                            break;
                    }
                }
            }
            else if (v.Initializer != null)
            {
                // Non-new-object variable with initializer
                string decoratedType = ApplyAllocationToFieldType(cppType, alloc);
                WriteLine($"{decoratedType} {v.Name} = {v.Initializer};");
            }
            else
            {
                // Declaration without initializer
                string decoratedType = ApplyAllocationToFieldType(cppType, alloc);
                WriteLine($"{decoratedType} {v.Name};");
            }
        }

        protected override void WriteAction(Action action)
        {
            // Raw line already contains the transpiled C++ expression.
            string line = action.Raw.TrimEnd(';');
            WriteLine(line + ";");
        }

        protected override void WriteCondition(Condition cond)
        {
            switch (cond.Kind)
            {
                case ConditionKind.If:
                    WriteLine($"{KeywordFor(CodePart.IfStatement)} ({cond.Expression})");
                    WriteBlock(cond.Body);
                    if (cond.ElseBody.Count > 0)
                    {
                        // A single Condition(ElseIf,...) in ElseBody → else if
                        if (cond.ElseBody.Count == 1 && cond.ElseBody[0] is Condition elseIf
                            && (elseIf.Kind == ConditionKind.ElseIf || elseIf.Kind == ConditionKind.If))
                        {
                            WriteLine($"{KeywordFor(CodePart.ElseClause)}");
                            WriteCondition(elseIf);
                        }
                        else
                        {
                            WriteLine($"{KeywordFor(CodePart.ElseClause)}");
                            WriteBlock(cond.ElseBody);
                        }
                    }
                    break;

                case ConditionKind.Unless:
                    // unless (expr) { } ≡ if (!(expr)) { }
                    WriteLine($"{KeywordFor(CodePart.IfStatement)} (!({cond.Expression}))");
                    WriteBlock(cond.Body);
                    if (cond.ElseBody.Count > 0)
                    {
                        WriteLine($"{KeywordFor(CodePart.ElseClause)}");
                        WriteBlock(cond.ElseBody);
                    }
                    break;

                case ConditionKind.ElseIf:
                    WriteLine($"{KeywordFor(CodePart.ElseClause)} {KeywordFor(CodePart.IfStatement)} ({cond.Expression})");
                    WriteBlock(cond.Body);
                    if (cond.ElseBody.Count > 0)
                    {
                        if (cond.ElseBody.Count == 1 && cond.ElseBody[0] is Condition elseIf
                            && (elseIf.Kind == ConditionKind.ElseIf || elseIf.Kind == ConditionKind.If))
                        {
                            WriteCondition(elseIf);
                        }
                        else
                        {
                            WriteLine($"{KeywordFor(CodePart.ElseClause)}");
                            WriteBlock(cond.ElseBody);
                        }
                    }
                    break;

                case ConditionKind.Else:
                    WriteLine($"{KeywordFor(CodePart.ElseClause)}");
                    WriteBlock(cond.Body);
                    break;

                case ConditionKind.Inline:
                    // Ternary: condition ? trueExpr : falseExpr
                    // Not all target languages support this — emit as a comment + if/else fallback
                    WriteLine($"// [inline ternary] {cond.Expression}");
                    WriteLine($"{KeywordFor(CodePart.IfStatement)} ({cond.Expression})");
                    WriteBlock(cond.Body);
                    if (cond.ElseBody.Count > 0)
                    {
                        WriteLine($"{KeywordFor(CodePart.ElseClause)}");
                        WriteBlock(cond.ElseBody);
                    }
                    break;

                case ConditionKind.Switch:
                    WriteLine($"{KeywordFor(CodePart.SwitchStatement)} ({cond.Expression})");
                    WriteBlock(cond.Body);
                    break;

                case ConditionKind.Match:
                    // Match is dispatched via WriteStatement → WriteMatch(MatchStatement),
                    // but if a Condition node has Kind=Match, emit as a switch with a comment.
                    WriteLine($"// [match] {cond.Expression}");
                    WriteLine($"{KeywordFor(CodePart.MatchStatement)} ({cond.Expression})");
                    WriteBlock(cond.Body);
                    break;
            }
        }

        protected override void WriteLoop(Loop loop)
        {
            switch (loop.Kind)
            {
                case LoopKind.While:
                    WriteLine($"{KeywordFor(CodePart.WhileStatement)} ({loop.Expression})");
                    WriteBlock(loop.Body);
                    break;

                case LoopKind.DoWhile:
                    WriteLine($"{KeywordFor(CodePart.DoWhileStatement)}");
                    WriteBlock(loop.Body);
                    WriteLine($"while ({loop.Expression}){StatementTerminator}");
                    break;

                case LoopKind.For:
                    WriteLine($"{KeywordFor(CodePart.ForStatement)} ({loop.Expression})");
                    WriteBlock(loop.Body);
                    break;

                case LoopKind.ForEach:
                    if (loop.IterationVariable != null && loop.Collection != null)
                    {
                        string cppType = MapType(loop.IterationVariable.Type);
                        WriteLine($"for (const {cppType}& {loop.IterationVariable.Name} : {loop.Collection})");
                    }
                    WriteBlock(loop.Body);
                    break;

                case LoopKind.Repeat:
                    {
                        string varName = loop.IterationVariable?.Name ?? "_i";
                        string varType = loop.IterationVariable != null ? MapType(loop.IterationVariable.Type) : "int";
                        string expr    = loop.Expression;

                        if (expr.Contains("->"))
                        {
                            // repeat i: start -> end (step)  →  for (T i = start; i < end; i += step)
                            var p     = expr.Split(new[] { "->" }, 2, StringSplitOptions.None);
                            string start = p[0];
                            int ci = p[1].IndexOf(',');
                            string end  = ci >= 0 ? p[1].Substring(0, ci) : p[1];
                            string step = ci >= 0 ? p[1].Substring(ci + 1) : null;
                            string incr = step == null ? $"++{varName}" : $"{varName} += {step}";
                            WriteLine($"for ({varType} {varName} = {start}; {varName} < {end}; {incr})");
                        }
                        else if (expr.Contains("<-"))
                        {
                            // repeat i: start <- end (step)  →  for (T i = start; i > end; i -= step)
                            var p     = expr.Split(new[] { "<-" }, 2, StringSplitOptions.None);
                            string start = p[0];
                            int ci = p[1].IndexOf(',');
                            string end  = ci >= 0 ? p[1].Substring(0, ci) : p[1];
                            string step = ci >= 0 ? p[1].Substring(ci + 1) : null;
                            string decr = step == null ? $"--{varName}" : $"{varName} -= {step}";
                            WriteLine($"for ({varType} {varName} = {start}; {varName} > {end}; {decr})");
                        }
                        else
                        {
                            // repeat N / repeat i: N  →  for (T i = 0; i < N; ++i)
                            WriteLine($"for ({varType} {varName} = 0; {varName} < {expr}; ++{varName})");
                        }
                        WriteBlock(loop.Body);
                    }
                    break;

                case LoopKind.RepeatUntil:
                    // repeat until (cond) { } → while (!(cond))
                    WriteLine($"while (!({loop.Expression}))");
                    WriteBlock(loop.Body);
                    break;
            }
        }

        private void WriteBlock(System.Collections.Generic.List<Statement> stmts)
        {
            WriteLine("{");
            Indent();
            foreach (var s in stmts) WriteStatement(s);
            Dedent();
            WriteLine("}");
        }

        protected override void WriteInlineCpp(InlineNativeBlock block)
        {
            foreach (var line in block.Lines)
                WriteLine(line);
        }

        // ── Exception & control-flow statements ───────────────────────────────

        protected override void WriteTryCatch(TryCatchBlock block)
        {
            WriteLine($"{KeywordFor(CodePart.TryStatement)}");
            WriteBlock(block.TryBody);

            foreach (var clause in block.Catches)
            {
                if (clause.ExceptionType == null)
                    WriteLine($"{KeywordFor(CodePart.CatchClause)} (...)");
                else if (clause.VariableName != null)
                    WriteLine($"{KeywordFor(CodePart.CatchClause)} ({MapType(clause.ExceptionType)}& {clause.VariableName})");
                else
                    WriteLine($"{KeywordFor(CodePart.CatchClause)} (const {MapType(clause.ExceptionType)}&)");

                WriteBlock(clause.Body);
            }

            if (block.FinallyBody.Count > 0)
            {
                // C++ has no finally — emit a scope guard that runs at scope exit.
                // Uses the common scope-guard idiom: struct with destructor that runs the finally body.
                WriteLine("{  // [finally]");
                Indent();
                WriteLine("struct __finally_guard { ~__finally_guard() {");
                Indent();
                foreach (var s in block.FinallyBody) WriteStatement(s);
                Dedent();
                WriteLine("} };");
                WriteLine("__finally_guard __sg;");
                Dedent();
                WriteLine("}");
            }
        }

        protected override void WriteUsingBlock(UsingBlock block)
        {
            // Scoped block — RAII: destructor fires at closing brace
            WriteLine("{");
            Indent();
            WriteVariable(block.Resource);
            foreach (var s in block.Body) WriteStatement(s);
            Dedent();
            WriteLine("}");
        }

        protected override void WriteLockBlock(LockBlock block)
        {
            WriteLine("{");
            Indent();
            WriteLine($"std::lock_guard<std::mutex> _lock({block.LockExpression});");
            foreach (var s in block.Body) WriteStatement(s);
            Dedent();
            WriteLine("}");
        }

        protected override void WriteThrow(ThrowStatement stmt)
        {
            string kw = KeywordFor(CodePart.ThrowStatement);
            if (stmt.ExceptionType == null)
                WriteLine($"{kw}{StatementTerminator}");  // bare rethrow
            else if (stmt.Arguments != null)
                WriteLine($"{kw} {MapType(stmt.ExceptionType)}({stmt.Arguments}){StatementTerminator}");
            else
                WriteLine($"{kw} {MapType(stmt.ExceptionType)}(){StatementTerminator}");
        }

        protected override void WriteYield(YieldStatement stmt)
        {
            string kw = KeywordFor(CodePart.YieldStatement);
            if (stmt.IsBreak)
                WriteLine($"co_return{StatementTerminator}");
            else
                WriteLine($"{kw} {stmt.Expression}{StatementTerminator}");
        }

        protected override void WriteCheckedBlock(CheckedBlock block)
        {
            string label = block.IsChecked ? "checked" : "unchecked";
            WriteLine($"// {label} arithmetic");
            WriteBlock(block.Body);
        }

        // ── New statement types ─────────────────────────────────────────────────

        protected override void WriteBlockStatement(BlockStatement block)
        {
            WriteBlock(block.Statements);
        }

        protected override void WriteDoWhile(DoWhileStatement stmt)
        {
            WriteLine($"{KeywordFor(CodePart.DoWhileStatement)}");
            WriteBlock(stmt.Body);
            WriteLine($"while ({stmt.Condition}){StatementTerminator}");
        }

        protected override void WriteReturn(ReturnStatement stmt)
        {
            string kw = KeywordFor(CodePart.ReturnStatement);
            if (stmt.Expression != null)
                WriteLine($"{kw} {stmt.Expression}{StatementTerminator}");
            else
                WriteLine($"{kw}{StatementTerminator}");
        }

        protected override void WriteBreak(BreakStatement stmt)
        {
            WriteLine($"{KeywordFor(CodePart.BreakStatement)}{StatementTerminator}");
        }

        protected override void WriteContinue(ContinueStatement stmt)
        {
            WriteLine($"{KeywordFor(CodePart.ContinueStatement)}{StatementTerminator}");
        }

        protected override void WritePostfixConditional(PostfixConditional stmt)
        {
            // expr if (cond) : alt;  →  if (cond)   { expr; } else { alt; }
            // expr unless (cond) : alt;  →  if (!(cond)) { expr; } else { alt; }
            string guardExpr = stmt.IsUnless ? $"!({stmt.Condition})" : stmt.Condition;
            string primary   = stmt.Primary.TrimEnd(';', ' ');
            WriteLine($"if ({guardExpr})");
            WriteLine("{");
            Indent();
            WriteLine($"{primary};");
            Dedent();
            if (stmt.Alt != null)
            {
                string alt = stmt.Alt.TrimEnd(';', ' ');
                WriteLine("} else {");
                Indent();
                WriteLine($"{alt};");
                Dedent();
            }
            WriteLine("}");
        }

        private int _deferCounter = 0;

        protected override void WriteDefer(DeferStatement stmt)
        {
            // Self-contained RAII scope guard — C++17, zero external dependencies.
            string id = $"_db_defer_{_deferCounter++}";
            string guard = $"[](auto _f){{ struct _S{{ decltype(_f) fn; ~_S(){{fn();}} }}; return _S{{_f}}; }}";

            bool multiLine = stmt.Body.Contains('\n');
            if (!multiLine)
            {
                WriteLine($"auto {id} = {guard}([&]{{ {stmt.Body} }});");
            }
            else
            {
                WriteLine($"auto {id} = {guard}([&]{{");
                Indent();
                foreach (string bodyLine in stmt.Body.Split('\n'))
                {
                    string trimmed = bodyLine.TrimEnd('\r').Trim();
                    if (!string.IsNullOrWhiteSpace(trimmed))
                        WriteLine(trimmed);
                }
                Dedent();
                WriteLine("});");
            }
        }

        protected override void WriteMove(MoveStatement stmt)
        {
            if (stmt.IsDeclaration)
                WriteLine($"auto {stmt.Target} = std::move({stmt.Source});");
            else
                WriteLine($"{stmt.Target} = std::move({stmt.Source});");
        }

        protected override void WriteSwap(SwapStatement stmt)
        {
            WriteLine($"std::swap({stmt.Left}, {stmt.Right});");
        }

        protected override void WriteLog(LogStatement stmt)
        {
            // log stream for MessageType.Level payload;
            // Emits: stream.Write(MessageType::Level, "message", payload);
            // With source context injected as a formatted prefix.
            // Debug-level logs are wrapped in #ifndef NDEBUG so they compile out in release.
            string stream = stmt.Stream;
            string level = stmt.MessageType.Replace(".", "::");
            string payload = stmt.Payload;
            string file = stmt.SourceFile ?? "__FILE__";
            string line = stmt.SourceLine > 0 ? stmt.SourceLine.ToString() : "__LINE__";

            bool isDebug = stmt.MessageType.EndsWith(".Debug") || stmt.MessageType == "Debug";

            if (isDebug)
                WriteLine("#ifndef NDEBUG");

            if (string.IsNullOrEmpty(payload))
            {
                // No payload — just the stream and level
                WriteLine($"{stream}.Write({level}, \"[{file}:{line}]\");");
            }
            else
            {
                WriteLine($"{stream}.Write({level}, \"[{file}:{line}] \" + {payload}.ToString());");
            }

            if (isDebug)
                WriteLine("#endif");
        }

        protected override void WriteMatch(MatchStatement stmt)
        {
            WriteLine($"switch ({stmt.Expression})");
            WriteLine("{");
            Indent();
            foreach (var arm in stmt.Arms)
            {
                string pattern = arm.Pattern;
                if (pattern == "_")
                    WriteLine("default:");
                else
                    WriteLine($"case {pattern}:");

                Indent();
                if (arm.Guard != null)
                    WriteLine($"if ({arm.Guard})");

                foreach (var s in arm.Body)
                    WriteStatement(s);

                WriteLine("break;");
                Dedent();
            }
            Dedent();
            WriteLine("}");
        }

        // ── New top-level declarations ───────────────────────────────────────────

        protected override void WriteInterface(InterfaceDeclaration iface)
        {
            string access = MapAccess(iface.Access);
            WriteLine($"{access}:");
            Indent();
            string typeParams = iface.TypeParams.Count > 0
                ? $"<{string.Join(", ", iface.TypeParams)}>"
                : "";
            string inheritance = iface.Extends.Count > 0
                ? " : " + string.Join(", ", iface.Extends)
                : "";

            WriteLine($"class {iface.Name}{typeParams}{inheritance}");
            WriteLine("{");
            Indent();
            WriteLine("public:");
            Indent();
            foreach (var m in iface.Methods)
            {
                string paramList = string.Join(", ", m.Parameters.Select(p =>
                {
                    string t = MapType(p.Type);
                    if (p.IsParams)
                    {
                        string elemType = t.EndsWith("[]") ? t.Substring(0, t.Length - 2) : t;
                        t = $"std::initializer_list<{elemType}>";
                    }
                    else if (p.Modifier == "out" || p.Modifier == "ref")
                        t = $"{t}&";
                    else if (p.Modifier == "in")
                        t = $"const {t}&";
                    else if (t.StartsWith("std::string") || t.StartsWith("db::") || t.StartsWith("std::unordered"))
                        t = $"const {t}&";
                    return $"{t} {p.Name}";
                }));
                string retType = m.IsConstructor || m.IsDestructor ? "" : MapType(m.ReturnType) + " ";
                string virt = m.IsVirtual || m.IsAbstract ? "virtual " : "";
                string suffix = m.IsAbstract ? " = 0" : (m.IsOverride ? " override" : "");
                if (m.Attributes.Any(a => a.Name == "Const")) suffix += " const";
                if (m.Attributes.Any(a => a.Name == "Noexcept")) suffix += " noexcept";
                WriteLine($"{virt}{retType}{m.Name}({paramList}){suffix};");
            }
            foreach (var p in iface.Properties)
                WriteProperty(p);
            Dedent();
            Dedent();
            WriteLine("};");
            WriteLine();
        }

        protected override void WriteSumType(SumTypeDeclaration sum)
        {
            // Emit as std::variant with named accessors
            string access = MapAccess(sum.Access);
            WriteLine($"{access}:");

            if (sum.TypeParams.Count > 0)
                WriteLine($"template<{string.Join(", ", sum.TypeParams.Select(t => "typename " + t))}>");

            // Build the variant type list
            var variantTypes = sum.Variants.Select(v =>
            {
                if (v.Data.Count == 0)
                    return $"std::monostate"; // empty variant
                if (v.Data.Count == 1)
                    return MapType(v.Data[0].Type);
                return $"std::tuple<{string.Join(", ", v.Data.Select(d => MapType(d.Type)))}>";
            }).ToList();

            WriteLine($"using {sum.Name} = std::variant<{string.Join(", ", variantTypes)}>;");

            // Emit visitor helper struct
            WriteLine($"struct {sum.Name}_visitor {{");
            Indent();
            foreach (var v in sum.Variants)
            {
                if (v.Data.Count == 0)
                    WriteLine($"void operator()(std::monostate) const {{ /* {v.Name} — no data */ }}");
                else if (v.Data.Count == 1)
                    WriteLine($"void operator()({MapType(v.Data[0].Type)} val) const {{ /* {v.Name} */ }}");
                else
                    WriteLine($"void operator()(std::tuple<{string.Join(", ", v.Data.Select(d => MapType(d.Type)))}>) const {{ /* {v.Name} */ }}");
            }
            Dedent();
            WriteLine("};");
            WriteLine();
        }

        protected override void WriteMixin(MixinDeclaration mixin)
        {
            // C++ doesn't have mixins — emit as free functions in a namespace
            string access = MapAccess(mixin.Access);
            WriteLine($"// [mixin] Extension methods for {mixin.TargetType}");
            WriteLine($"namespace {mixin.TargetType}_Extensions {{");
            Indent();
            foreach (var m in mixin.Methods)
            {
                // Emit as free functions taking the target type as first param
                string paramList = string.Join(", ", m.Parameters.Select(p =>
                {
                    string t = MapType(p.Type);
                    if (p.Modifier == "out" || p.Modifier == "ref")
                        t = $"{t}&";
                    else if (p.Modifier == "in")
                        t = $"const {t}&";
                    else if (t.StartsWith("std::string") || t.StartsWith("db::") || t.StartsWith("std::unordered"))
                        t = $"const {t}&";
                    return $"{t} {p.Name}";
                }));
                string thisParam = $"{MapType(mixin.TargetType)}& _self";
                string fullParams = string.IsNullOrEmpty(paramList) ? thisParam : $"{thisParam}, {paramList}";
                string retType = m.IsConstructor || m.IsDestructor ? "" : MapType(m.ReturnType) + " ";
                string prefix = m.IsStatic ? "static " : "";
                if (m.Attributes.Any(a => a.Name == "Inline")) prefix += "inline ";
                WriteLine($"{prefix}{retType}{m.Name}({fullParams})");
                WriteMethodBody(m);
            }
            foreach (var p in mixin.Properties)
                WriteProperty(p);
            Dedent();
            WriteLine("}}");
            WriteLine();
        }

        protected override void WriteTypeAlias(TypeAliasDeclaration alias)
        {
            string access = ProfileAccess(alias.Access);
            string template = TemplateFor(CodePart.TypeAlias);
            if (template != null)
            {
                // Template placeholders: {0}=name, {1}=target type
                string line = template.Replace("{0}", alias.Name).Replace("{1}", MapType(alias.TargetType));
                WriteLine($"{access}:");
                WriteLine(line + StatementTerminator);
            }
            else
            {
                WriteLine($"{access}:");
                WriteLine($"using {alias.Name} = {MapType(alias.TargetType)}{StatementTerminator}");
            }
        }

        protected override void WriteFileScopeDirective(FileScopeDirective directive)
        {
            string includeKw = KeywordFor(CodePart.FileScopeDirective);
            switch (directive.Kind)
            {
                case "include":
                    if (directive.IsSystem)
                        WriteLine($"{includeKw} <{directive.Target}>");
                    else
                        WriteLine($"{includeKw} \"{directive.Target}\"");
                    break;
                case "pragma":
                    WriteLine($"#pragma {directive.Target}");
                    break;
                case "define":
                    WriteLine($"#define {directive.Target}");
                    break;
                default:
                    WriteLine($"// [file-scope] {directive.Kind}: {directive.Target}");
                    break;
            }
        }

        // ── Expression emission ─────────────────────────────────────────────────

        /// <summary>
        /// Emits an expression as a single-line string (no trailing newline).
        /// Used for embedding expressions inside larger constructs like assignments,
        /// conditions, and return values.
        /// </summary>
        private string Expr(Expression expr)
        {
            switch (expr)
            {
                case LiteralExpression lit:       return EmitLiteral(lit);
                case IdentifierExpression id:     return id.Name;
                case BinaryExpression bin:         return $"{Expr(bin.Left)} {bin.Operator} {Expr(bin.Right)}";
                case UnaryExpression un:
                    return un.IsPrefix
                        ? $"{un.Operator}{Expr(un.Operand)}"
                        : $"{Expr(un.Operand)}{un.Operator}";
                case AssignmentExpression assign: return $"{Expr(assign.Target)} {assign.Operator} {Expr(assign.Value)}";
                case MemberAccessExpression mem:   return EmitMemberAccess(mem);
                case InvocationExpression inv:     return EmitInvocation(inv);
                case ObjectCreationExpression obj:  return EmitObjectCreation(obj);
                case CastExpression cast:
                    return cast.IsImplicit
                        ? $"static_cast<{MapType(cast.TargetType)}>({Expr(cast.Inner)})"
                        : $"dynamic_cast<{MapType(cast.TargetType)}*>(&{Expr(cast.Inner)}) != nullptr";
                case IsTypeExpression isType:
                    return $"dynamic_cast<{MapType(isType.TargetType)}*>(&{Expr(isType.Inner)}) != nullptr";
                case ParenthesizedExpression paren: return $"({Expr(paren.Inner)})";
                case TernaryExpression tern:
                    return $"{Expr(tern.Condition)} ? {Expr(tern.TrueExpr)} : {Expr(tern.FalseExpr)}";
                case RangeExpression range:        return EmitRange(range);
                case LambdaExpression lambda:      return EmitLambda(lambda);
                case AwaitExpression awaitExpr:    return $"{KeywordFor(CodePart.AwaitExpression)} {Expr(awaitExpr.Inner)}";
                case ArrayCreationExpression arr:  return EmitArrayCreation(arr);
                case TupleExpression tuple:
                    return $"std::make_tuple({string.Join(", ", tuple.Elements.Select(e => Expr(e)))})";
                default: return $"/* unknown expression */";
            }
        }

        private string EmitLiteral(LiteralExpression lit)
        {
            // Map C# literals to C++ equivalents
            if (lit.Value == "null") return Profile.NullLiteral;
            if (lit.Value == "true") return "true";
            if (lit.Value == "false") return "false";

            // If a type hint is provided, use it for disambiguation
            if (lit.LiteralType != null)
            {
                switch (lit.LiteralType)
                {
                    case "float":
                    case "double":
                        // Ensure floating-point literals have proper suffix
                        return lit.Value;
                    case "long":
                        return lit.Value + "L";
                    case "unsigned":
                    case "uint":
                        return lit.Value + "U";
                }
            }

            return lit.Value;
        }

        private string EmitMemberAccess(MemberAccessExpression mem)
        {
            string target = Expr(mem.Target);
            switch (mem.Access)
            {
                case MemberAccess.Dot:              return $"{target}.{mem.Member}";
                case MemberAccess.Arrow:            return $"{target}->{mem.Member}";
                case MemberAccess.Colons:            return $"{target}::{mem.Member}";
                case MemberAccess.Colon:             return $"{target}:{mem.Member}";
                case MemberAccess.DotAsterisk:       return $"{target}.*{mem.Member}";
                case MemberAccess.ArrowAsterisk:     return $"{target}->*{mem.Member}";
                case MemberAccess.QuestionMarkDot:   return $"{target}?.{mem.Member}";
                case MemberAccess.QuestionMarkBracket: return $"{target}?[{mem.Member}]";
                case MemberAccess.Bracket:          return $"{target}[{mem.Member}]";
                default:                             return $"{target}.{mem.Member}";
            }
        }

        private string EmitInvocation(InvocationExpression inv)
        {
            // Extension method UFCS: list.Where(pred) → std::ranges::copy_if(list, pred)
            if (inv.Callee is MemberAccessExpression extMem &&
                _extensionMethods.TryGetValue(extMem.Member, out string cppExpr))
            {
                string target  = Expr(extMem.Target);
                string rest    = string.Join(", ", inv.Arguments.Select(a => EmitArg(a)));
                string allArgs = rest.Length > 0 ? $"{target}, {rest}" : target;
                return $"{cppExpr}({allArgs})";
            }

            // Db intrinsic keywords → std:: equivalents
            if (inv.Callee is IdentifierExpression ident)
            {
                string args = string.Join(", ", inv.Arguments.Select(a => EmitArg(a)));
                switch (ident.Name)
                {
                    case "move":    return $"std::move({args})";
                    case "swap":    return $"std::swap({args})";
                    case "forward": return inv.Arguments.Count == 1
                        ? $"std::forward<decltype({args})>({args})"
                        : $"std::forward({args})";
                }
            }
            string callee = Expr(inv.Callee);
            string callArgs = string.Join(", ", inv.Arguments.Select(a => EmitArg(a)));
            return $"{callee}({callArgs})";
        }

        /// <summary>
        /// Emits a call-site argument, prefixing out/ref/in modifiers for C++.
        /// </summary>
        private string EmitArg(Argument arg)
        {
            string expr = Expr(arg.Value);
            return !string.IsNullOrEmpty(arg.Modifier) ? $"{arg.Modifier} {expr}" : expr;
        }

        private string EmitObjectCreation(ObjectCreationExpression obj)
        {
            string args = string.Join(", ", obj.Arguments.Select(a => Expr(a)));

            if (obj.Type == "$with")
            {
                // Emit brace initialization to seamlessly trigger C++ multiple inheritance constructors
                return $"{{ {args} }}";
            }

            string cppType = MapType(obj.Type);

            // Emit based on allocation strategy
            switch (obj.Allocation)
            {
                case AllocationStrategy.Stack:
                    // Stack allocation: Type(args) or Type{}
                    return string.IsNullOrEmpty(args) ? $"{cppType}{{}}" : $"{cppType}({args})";

                case AllocationStrategy.Unique:
                    // Unique ownership: std::make_unique<Type>(args)
                    return $"std::make_unique<{cppType}>({args})";

                case AllocationStrategy.Shared:
                    // Shared ownership: std::make_shared<Type>(args)
                    return $"std::make_shared<{cppType}>({args})";

                case AllocationStrategy.Raw:
                    // Raw pointer: new Type(args)
                    return $"new {cppType}({args})";

                default:
                    // Default: stack allocation (RAII-first)
                    return string.IsNullOrEmpty(args) ? $"{cppType}{{}}" : $"{cppType}({args})";
            }
        }

        private string EmitRange(RangeExpression range)
        {
            // C++ doesn't have native ranges — emit as a comment or use a helper
            string start = range.Start != null ? Expr(range.Start) : "";
            string end = range.End != null ? Expr(range.End) : "";
            string sep = range.IsInclusive ? "..=" : "..";
            return $"/* range {start}{sep}{end} */";
        }

        private string EmitLambda(LambdaExpression lambda)
        {
            string paramList = string.Join(", ", lambda.Parameters.Select(p =>
            {
                string t = MapType(p.Type);
                if (p.Modifier == "out" || p.Modifier == "ref")
                    t = $"{t}&";
                else if (p.Modifier == "in")
                    t = $"const {t}&";
                else if (t.StartsWith("std::string") || t.StartsWith("db::") || t.StartsWith("std::unordered"))
                    t = $"const {t}&";
                return $"{t} {p.Name}";
            }));

            if (lambda.ExpressionBody != null)
            {
                // Expression lambda: [captures](params) -> retType { return expr; }
                string retType = lambda.ReturnType != null ? MapType(lambda.ReturnType) : "auto";
                return $"[{paramList}] -> {retType} {{ return {Expr(lambda.ExpressionBody)}; }}";
            }
            else
            {
                // Statement lambda — can't inline, emit as comment placeholder
                return $"[{paramList}] {{ /* lambda body */ }}";
            }
        }

        private string EmitArrayCreation(ArrayCreationExpression arr)
        {
            string elemType = arr.ElementType != null ? MapType(arr.ElementType) : "auto";
            if (arr.Elements.Count > 0)
            {
                // Initializer list: {1, 2, 3}
                string elems = string.Join(", ", arr.Elements.Select(e => Expr(e)));
                if (arr.Size != null)
                    return $"std::array<{elemType}, {Expr(arr.Size)}>{{{{ {elems} }}}}";
                else
                    return $"{{ {elems} }}";
            }
            else if (arr.Size != null)
            {
                // Sized but empty: std::array<Type, N>{} or new Type[N]
                return $"std::vector<{elemType}>({Expr(arr.Size)})";
            }
            else
            {
                return $"std::vector<{elemType}>{{}}";
            }
        }

        // ── Expression Write* overrides (write to output stream) ────────────────

        protected override void WriteLiteralExpression(LiteralExpression expr)
        {
            Write(EmitLiteral(expr));
        }

        protected override void WriteIdentifierExpression(IdentifierExpression expr)
        {
            Write(expr.Name);
        }

        protected override void WriteBinaryExpression(BinaryExpression expr)
        {
            Write($"{Expr(expr.Left)} {expr.Operator} {Expr(expr.Right)}");
        }

        protected override void WriteUnaryExpression(UnaryExpression expr)
        {
            if (expr.IsPrefix)
                Write($"{expr.Operator}{Expr(expr.Operand)}");
            else
                Write($"{Expr(expr.Operand)}{expr.Operator}");
        }

        protected override void WriteAssignmentExpression(AssignmentExpression expr)
        {
            Write($"{Expr(expr.Target)} {expr.Operator} {Expr(expr.Value)}");
        }

        protected override void WriteMemberAccessExpression(MemberAccessExpression expr)
        {
            Write(EmitMemberAccess(expr));
        }

        protected override void WriteInvocationExpression(InvocationExpression expr)
        {
            Write(EmitInvocation(expr));
        }

        protected override void WriteObjectCreationExpression(ObjectCreationExpression expr)
        {
            Write(EmitObjectCreation(expr));
        }

        protected override void WriteCastExpression(CastExpression expr)
        {
            if (expr.IsImplicit)
                Write($"static_cast<{MapType(expr.TargetType)}>({Expr(expr.Inner)})");
            else
                Write($"dynamic_cast<{MapType(expr.TargetType)}*>(&{Expr(expr.Inner)}) != nullptr");
        }

        protected override void WriteIsTypeExpression(IsTypeExpression expr)
        {
            Write($"dynamic_cast<{MapType(expr.TargetType)}*>(&{Expr(expr.Inner)}) != nullptr");
        }

        protected override void WriteParenthesizedExpression(ParenthesizedExpression expr)
        {
            Write($"({Expr(expr.Inner)})");
        }

        protected override void WriteTernaryExpression(TernaryExpression expr)
        {
            Write($"{Expr(expr.Condition)} ? {Expr(expr.TrueExpr)} : {Expr(expr.FalseExpr)}");
        }

        protected override void WriteRangeExpression(RangeExpression expr)
        {
            Write(EmitRange(expr));
        }

        protected override void WriteLambdaExpression(LambdaExpression expr)
        {
            Write(EmitLambda(expr));
        }

        protected override void WriteAwaitExpression(AwaitExpression expr)
        {
            Write($"{KeywordFor(CodePart.AwaitExpression)} {Expr(expr.Inner)}");
        }

        protected override void WriteArrayCreationExpression(ArrayCreationExpression expr)
        {
            Write(EmitArrayCreation(expr));
        }

        protected override void WriteTupleExpression(TupleExpression expr)
        {
            Write($"std::make_tuple({string.Join(", ", expr.Elements.Select(e => Expr(e)))})");
        }
    }
}
