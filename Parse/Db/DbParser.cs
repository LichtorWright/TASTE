using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using taste.Emit;
using taste.Parse;

namespace taste.Parse.Db
{
    /// <summary>
    /// Parses .db source files into the CodeFile model.
    /// "Db" (D-flat) is a structural mirror of C#, but with a different "tone" (syntax).
    /// Inherits from <see cref="Parser"/> to share the language-agnostic parsing contract.
    /// </summary>
    public class DbParser : Parser
    {
        private string _filePath;
        private string[] _lines;
        private int _currentLine;
        private readonly ExpressionParser _exprParser = new ExpressionParser();

        public DbParser(string filePath)
            : base(LanguageMatrix.Languages[Language.Db])
        {
            _filePath = filePath;
        }

        /// <summary>
        /// Implements <see cref="Parser.ParseFile"/> — reads the .db file and
        /// produces a <see cref="CodeFile"/> AST.
        /// </summary>
        public override CodeFile ParseFile(string source, string filePath)
        {
            _filePath = filePath;
            _lines = source.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            _currentLine = 0;
            return ParseFromLines();
        }

        /// <summary>
        /// Convenience overload: parse from a file path (reads the file from disk).
        /// </summary>
        public CodeFile Parse()
        {
            _lines = File.ReadAllLines(_filePath);
            _currentLine = 0;
            return ParseFromLines();
        }

        /// <summary>
        /// Core parsing logic — works from the already-populated _lines array.
        /// </summary>
        private CodeFile ParseFromLines()
        {

            var file = new CodeFile(_filePath);

            while (!IsEndOfFile())
            {
                try
                {
                string line = PeekLine()?.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//"))
                {
                    ConsumeLine();
                    continue;
                }

                // Check if the next line is an attribute that precedes a class/struct
                if (line.StartsWith("["))
                {
                    int savedPos = _currentLine;
                    ConsumeLine(); // consume the attribute line
                    string nextLine = PeekLine()?.Trim();
                    _currentLine = savedPos; // restore position

                    if (nextLine != null && IsClassOrStructLine(nextLine))
                    {
                        var cls = ParseClass();
                        if (cls != null) file.Classes.Add(cls);
                        continue;
                    }
                }

                if (line.StartsWith(DbKeywords.UsingKeyword + " ") && line.EndsWith(";"))
                {
                    string name = ConsumeLine().Trim().Substring(6, line.Length - 7);
                    file.Usings.Add(new Using(name));
                }
                else if (StartsWithKeyword(line, DbKeywords.Namespace))
                {
                    file.Namespaces.Add(ParseNamespace());
                }
                else if (IsClassOrStructLine(line))
                {
                    var cls = ParseClass();
                    if (cls != null) file.Classes.Add(cls);
                }
                else if (StartsWithAccessAndKeyword(line, DbKeywords.Enum))
                {
                    file.Enums.Add(ParseEnum());
                }
                else if (StartsWithAccessAndKeyword(line, DbKeywords.Interface))
                {
                    file.Interfaces.Add(ParseInterface());
                }
                else if (StartsWithAccessAndKeyword(line, DbKeywords.Sum) || (StartsWithAccessAndKeyword(line, DbKeywords.Enum) && line.Contains("|")))
                {
                    file.SumTypes.Add(ParseSumType());
                }
                else if (StartsWithAccessAndKeyword(line, DbKeywords.Type))
                {
                    file.TypeAliases.Add(ParseTypeAlias());
                }
                else if (line.StartsWith("#include") || line.StartsWith("#pragma") || line.StartsWith("#define"))
                {
                    file.FileScopeDirectives.Add(ParseFileScopeDirective());
                }
                else if (StartsWithAccessAndKeyword(line, DbKeywords.Delegate))
                {
                    file.Delegates.Add(ParseDelegate());
                }
                else if (StartsWithAccessAndKeyword(line, "with"))
                {
                    file.Mixins.Add(ParseMixin());
                }
                else
                {
                    ConsumeLine(); // Skip unknown
                }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[DbParser] Skipping top-level construct due to parse error: {ex.Message}");
                    ConsumeLine();
                }
            }

            return file;
        }

        private Namespace ParseNamespace()
        {
            string line = ConsumeLine().Trim();
            var match = Regex.Match(line, @"namespace\s+([a-zA-Z0-9_.]+)\s*\{");
            if (!match.Success) throw new Exception($"Invalid namespace declaration: {line}");

            var ns = new Namespace(match.Groups[1].Value);
            
            while (!IsEndOfFile())
            {
                try
                {
                string innerLine = PeekLine()?.Trim();
                if (string.IsNullOrWhiteSpace(innerLine) || innerLine.StartsWith("//"))
                {
                    ConsumeLine();
                    continue;
                }

                if (innerLine == "}")
                {
                    ConsumeLine();
                    break;
                }

                // Check if the next line is an attribute that precedes a class/struct
                if (innerLine.StartsWith("["))
                {
                    // Peek ahead to see if a class/struct follows
                    int savedPos = _currentLine;
                    ConsumeLine(); // consume the attribute line
                    string nextLine = PeekLine()?.Trim();
                    _currentLine = savedPos; // restore position

                    if (nextLine != null && IsClassOrStructLine(nextLine))
                    {
                        var cls = ParseClass();
                        if (cls != null) ns.Classes.Add(cls);
                        continue;
                    }
                }

                if (StartsWithKeyword(innerLine, DbKeywords.Namespace))
                {
                    ns.NestedNamespaces.Add(ParseNamespace());
                }
                else if (IsClassOrStructLine(innerLine))
                {
                    var cls = ParseClass();
                    if (cls != null) ns.Classes.Add(cls);
                }
                else if (StartsWithAccessAndKeyword(innerLine, DbKeywords.Interface))
                {
                    ns.Interfaces.Add(ParseInterface());
                }
                else if (StartsWithAccessAndKeyword(innerLine, DbKeywords.Sum) || (StartsWithAccessAndKeyword(innerLine, DbKeywords.Enum) && innerLine.Contains("|")))
                {
                    ns.SumTypes.Add(ParseSumType());
                }
                else if (StartsWithAccessAndKeyword(innerLine, DbKeywords.Type))
                {
                    ns.TypeAliases.Add(ParseTypeAlias());
                }
                else if (StartsWithAccessAndKeyword(innerLine, DbKeywords.Enum))
                {
                    ns.Enums.Add(ParseEnum());
                }
                else if (StartsWithAccessAndKeyword(innerLine, DbKeywords.Delegate))
                {
                    ns.Delegates.Add(ParseDelegate());
                }
                else if (StartsWithAccessAndKeyword(innerLine, "with"))
                {
                    ns.Mixins.Add(ParseMixin());
                }
                else
                {
                    ConsumeLine();
                }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[DbParser] Skipping namespace member due to parse error: {ex.Message}");
                    ConsumeLine();
                }
            }
            return ns;
        }

        private Class ParseClass()
        {
            // Collect any attributes preceding the class declaration
            var classAttrs = CollectAttributes();
            
            string line = ConsumeLine().Trim();
            var match = Regex.Match(line, @"^\s*(public|private|protected|internal)?\s*(static\s+)?(sealed\s+)?(abstract\s+)?(readonly\s+)?(partial\s+)?(class|struct|stub)\s+(\w+)(?:<[^>]+>)?\s*(:\s*[^{]+)?\s*(\{)?");
            if (!match.Success) throw new Exception($"Invalid class declaration: {line}");

            // Stubs are IntelliSense-only declarations; skip them entirely at the reader level.
            if (match.Groups[7].Value == "stub")
            {
                if (match.Groups[10].Value == "{")
                {
                    int depth = 1;
                    while (!IsEndOfFile() && depth > 0)
                    {
                        string l = ConsumeLine();
                        depth += CountBraces(l);
                    }
                }
                return null;
            }

            var cls = new Class(match.Groups[8].Value)
            {
                IsStruct  = match.Groups[7].Value == "struct",
                IsStatic  = match.Groups[2].Success && match.Groups[2].Value.Trim() == "static",
                IsSealed  = match.Groups[3].Success,
                IsAbstract = match.Groups[4].Success
            };

            // Apply class-level attributes (e.g., [Represents("std::string")])
            foreach (var attr in classAttrs)
                cls.Attributes.Add(attr);

            // Handle inheritance
            if (!string.IsNullOrEmpty(match.Groups[9].Value))
            {
                string basesRaw = match.Groups[9].Value.TrimStart(':').Trim();
                foreach (var baseName in basesRaw.Split(','))
                {
                    string trimmedBase = baseName.Trim();
                    bool isInterface = trimmedBase.StartsWith("I") && trimmedBase.Length > 1 && char.IsUpper(trimmedBase[1]);
                    cls.BaseClasses.Add(new BaseClass(trimmedBase, isInterface));
                }
            }

            // If the opening brace wasn't on the same line, consume lines until we find it
            if (match.Groups[10].Value != "{")
            {
                while (!IsEndOfFile())
                {
                    string nextLine = PeekLine()?.Trim();
                    if (nextLine == "{")
                    {
                        ConsumeLine();
                        break;
                    }
                    if (nextLine == null) break;
                    ConsumeLine(); // skip blank lines or comments between class name and {
                }
            }

            while (!IsEndOfFile())
                {
                    try
                    {
                    // Collect any attribute lines preceding the next member
                    var memberAttrs = CollectAttributes();

                    string memberLine = PeekLine()?.Trim();
                    if (string.IsNullOrWhiteSpace(memberLine) || memberLine.StartsWith("//"))
                    {
                        ConsumeLine();
                        continue;
                    }

                    if (memberLine == "}")
                    {
                        ConsumeLine();
                        break;
                    }

                    // 1. Constants: const int Max = 100; or public static const int Max = 100;
                    var constMatch = Regex.Match(memberLine, @"^\s*(public|private|protected|internal)?\s*(static\s+)?const\s+(\w+)\s+(\w+)\s*=\s*([^;]+);\s*$");
                    if (constMatch.Success)
                    {
                        var c = new Constant(constMatch.Groups[4].Value, constMatch.Groups[3].Value, constMatch.Groups[5].Value.Trim());
                        c.Access = ParseAccess(constMatch.Groups[1].Value);
                        foreach (var attr in memberAttrs) c.Attributes.Add(attr);
                        cls.Constants.Add(c);
                        ConsumeLine();
                        continue;
                    }

                    // 2a. Lazy fields:  `public SomeType result = expr;
                    //                   `private SomeType result = expr;
                    // The backtick leads the whole declaration (before access modifier).
                    // Inferred types (var) are not permitted — type must be explicit.
                    var lazyMatch = Regex.Match(memberLine,
                        @"^\s*`(public|private|protected|internal)?\s*(var|\w+(?:<[^>]+>)?)\s+(\w+)\s*=\s*(.+);\s*$");
                    if (lazyMatch.Success)
                    {
                        string lazyType = lazyMatch.Groups[2].Value;
                        if (lazyType == "var")
                            throw new Exception(
                                $"[Db] Assignment deferrence cannot occur on inferred types. " +
                                $"Replace 'var' with an explicit type for '{lazyMatch.Groups[3].Value}'.");

                        string lazyName = lazyMatch.Groups[3].Value;
                        string lazyInit = lazyMatch.Groups[4].Value.Trim();
                        var lf = new Field(lazyName, lazyType)
                        {
                            Access      = ParseAccess(lazyMatch.Groups[1].Value),
                            IsLazy      = true,
                            Initializer = lazyInit
                        };
                        foreach (var attr in memberAttrs) lf.Attributes.Add(attr);
                        cls.Fields.Add(lf);
                        ConsumeLine();
                        continue;
                    }

                    // 2b. Readonly field: public readonly int X; or public static readonly int X = expr;
                    var readonlyFieldMatch = Regex.Match(memberLine, @"^\s*(public|private|protected|internal)?\s*(static\s+)?readonly\s+(\w+(?:<[^>]+>)?(?:\[\])?)\s+(\w+)\s*(?:=\s*([^;]+))?\s*;");
                    if (readonlyFieldMatch.Success)
                    {
                        var f = new Field(readonlyFieldMatch.Groups[4].Value, readonlyFieldMatch.Groups[3].Value)
                        {
                            Access = ParseAccess(readonlyFieldMatch.Groups[1].Value),
                            IsStatic = readonlyFieldMatch.Groups[2].Success,
                            IsReadonly = true
                        };
                        if (readonlyFieldMatch.Groups[5].Success && !string.IsNullOrEmpty(readonlyFieldMatch.Groups[5].Value))
                            f.Initializer = readonlyFieldMatch.Groups[5].Value.Trim();
                        foreach (var attr in memberAttrs) f.Attributes.Add(attr);
                        cls.Fields.Add(f);
                        ConsumeLine();
                        continue;
                    }

                    // 2b2. Volatile field: public volatile int X;
                    var volatileFieldMatch = Regex.Match(memberLine, @"^\s*(public|private|protected|internal)?\s*(static\s+)?volatile\s+(\w+(?:<[^>]+>)?(?:\[\])?)\s+(\w+)\s*(?:=\s*([^;]+))?\s*;");
                    if (volatileFieldMatch.Success)
                    {
                        var f = new Field(volatileFieldMatch.Groups[4].Value, volatileFieldMatch.Groups[3].Value)
                        {
                            Access = ParseAccess(volatileFieldMatch.Groups[1].Value),
                            IsStatic = volatileFieldMatch.Groups[2].Success,
                            Mutability = MutabilityModifier.Volatile
                        };
                        if (volatileFieldMatch.Groups[5].Success && !string.IsNullOrEmpty(volatileFieldMatch.Groups[5].Value))
                            f.Initializer = volatileFieldMatch.Groups[5].Value.Trim();
                        foreach (var attr in memberAttrs) f.Attributes.Add(attr);
                        cls.Fields.Add(f);
                        ConsumeLine();
                        continue;
                    }

                    // 2b3. Fixed buffer: public fixed int buffer[10];
                    var fixedMatch = Regex.Match(memberLine, @"^\s*(public|private|protected|internal)?\s*(static\s+)?fixed\s+(\w+)\s+(\w+)\s*\[([^\]]+)\]\s*;");
                    if (fixedMatch.Success)
                    {
                        var f = new Field(fixedMatch.Groups[4].Value, fixedMatch.Groups[3].Value)
                        {
                            Access = ParseAccess(fixedMatch.Groups[1].Value),
                            IsStatic = fixedMatch.Groups[2].Success,
                            IsFixed = true,
                            FixedSize = fixedMatch.Groups[5].Value.Trim()
                        };
                        foreach (var attr in memberAttrs) f.Attributes.Add(attr);
                        cls.Fields.Add(f);
                        ConsumeLine();
                        continue;
                    }

                    // 2c. Fields: public int Health; or public static int Health = 42;
                    var fieldMatch = Regex.Match(memberLine, @"^\s*(public|private|protected|internal)?\s*(static\s+)?(\w+)<[^>]*>\s+(\w+)\s*(?:=\s*([^;]+))?\s*;\s*$"); // Generic
                    if (!fieldMatch.Success)
                        fieldMatch = Regex.Match(memberLine, @"^\s*(public|private|protected|internal)?\s*(static\s+)?(\w+)\s+(\w+)\s*(?:=\s*([^;]+))?\s*;\s*$"); // Simple

                    if (fieldMatch.Success)
                    {
                        string type = fieldMatch.Groups[3].Value;
                        string name = fieldMatch.Groups[4].Value;
                        var f = new Field(name, type) { Access = ParseAccess(fieldMatch.Groups[1].Value), IsStatic = fieldMatch.Groups[2].Success };
                        if (fieldMatch.Groups[5].Success && !string.IsNullOrEmpty(fieldMatch.Groups[5].Value))
                            f.Initializer = fieldMatch.Groups[5].Value.Trim();
                        foreach (var attr in memberAttrs) f.Attributes.Add(attr);
                        cls.Fields.Add(f);
                        ConsumeLine();
                        continue;
                    }

                    // 3. Properties — multiple formats:
                    //    a) Auto-property: public int Score { get; set; }
                    //    b) Expression-bodied: public string Name => "Derp";
                    //    c) Multi-line with get/set blocks:
                    //           public int Length {
                    //               get { /* stub */ }
                    //           }
                    //    d) Multi-line with get and set blocks:
                    //           public char this[int index] {
                    //               get { /* stub */ }
                    //               set { /* stub */ }
                    //           }

                    // 3a. Expression-bodied property: public string Name => expr;
                    var exprBodyMatch = Regex.Match(memberLine, @"^\s*(public|private|protected|internal)?\s*(static\s+)?(sealed\s+)?(override\s+)?(virtual\s+)?(abstract\s+)?(\w+)(?:<[^>]*>)?\s+(\w+)\s*=>\s*(.+);");
                    if (exprBodyMatch.Success)
                    {
                        var p = new Property(exprBodyMatch.Groups[8].Value, exprBodyMatch.Groups[7].Value)
                        {
                            Access = ParseAccess(exprBodyMatch.Groups[1].Value),
                            HasGetter = true,
                            IsStatic = exprBodyMatch.Groups[2].Success,
                            IsSealed = exprBodyMatch.Groups[3].Success,
                            IsOverride = exprBodyMatch.Groups[4].Success,
                            IsVirtual = exprBodyMatch.Groups[5].Success,
                            IsAbstract = exprBodyMatch.Groups[6].Success,
                            IsExpressionBodied = true,
                            Initializer = exprBodyMatch.Groups[9].Value.Trim()
                        };
                        foreach (var attr in memberAttrs) p.Attributes.Add(attr);
                        cls.Properties.Add(p);
                        ConsumeLine();
                        continue;
                    }

                    // 3b. Single-line auto-property: public int Score { get; set; } = 0;
                    var autoPropMatch = Regex.Match(memberLine, @"^\s*(public|private|protected|internal)?\s*(static\s+)?(sealed\s+)?(override\s+)?(virtual\s+)?(abstract\s+)?(\w+)(?:<[^>]*>)?\s+(\w+)\s*\{\s*(get;\s*set;|set;\s*get;|get;)\s*\}(?:\s*=\s*([^;]+))?\s*;");
                    if (autoPropMatch.Success)
                    {
                        var p = new Property(autoPropMatch.Groups[8].Value, autoPropMatch.Groups[7].Value)
                        {
                            Access = ParseAccess(autoPropMatch.Groups[1].Value),
                            HasGetter = autoPropMatch.Groups[9].Value.Contains("get;"),
                            HasSetter = autoPropMatch.Groups[9].Value.Contains("set;"),
                            IsStatic = autoPropMatch.Groups[2].Success,
                            IsSealed = autoPropMatch.Groups[3].Success,
                            IsOverride = autoPropMatch.Groups[4].Success,
                            IsVirtual = autoPropMatch.Groups[5].Success,
                            IsAbstract = autoPropMatch.Groups[6].Success
                        };
                        if (autoPropMatch.Groups[10].Success && !string.IsNullOrEmpty(autoPropMatch.Groups[10].Value))
                            p.Initializer = autoPropMatch.Groups[10].Value.Trim();
                        foreach (var attr in memberAttrs) p.Attributes.Add(attr);
                        cls.Properties.Add(p);
                        ConsumeLine();
                        continue;
                    }

                    // 3c. Multi-line property: starts with "Type Name {" and spans multiple lines
                    var multiLinePropMatch = Regex.Match(memberLine, @"^\s*(public|private|protected|internal)?\s*(static\s+)?(sealed\s+)?(override\s+)?(virtual\s+)?(abstract\s+)?(\w+)(?:<[^>]*>)?\s+(\w+)\s*\{");
                    if (multiLinePropMatch.Success && !memberLine.Contains("(")) // not a method
                    {
                        var p = new Property(multiLinePropMatch.Groups[8].Value, multiLinePropMatch.Groups[7].Value)
                        {
                            Access = ParseAccess(multiLinePropMatch.Groups[1].Value),
                            IsStatic = multiLinePropMatch.Groups[2].Success,
                            IsSealed = multiLinePropMatch.Groups[3].Success,
                            IsOverride = multiLinePropMatch.Groups[4].Success,
                            IsVirtual = multiLinePropMatch.Groups[5].Success,
                            IsAbstract = multiLinePropMatch.Groups[6].Success
                        };

                        // Check if the property closes on the same line (single-line with body)
                        if (memberLine.TrimEnd().EndsWith("}"))
                        {
                            // Single-line property with body like: public int Length { get { /* stub */ } }
                            string inner = memberLine.Substring(memberLine.IndexOf('{') + 1, memberLine.LastIndexOf('}') - memberLine.IndexOf('{') - 1).Trim();
                            p.HasGetter = inner.Contains("get");
                            p.HasSetter = inner.Contains("set");
                        }
                        else
                        {
                            // Multi-line: consume lines until closing brace
                            ConsumeLine(); // consume the opening line
                            int depth = 1;
                            while (!IsEndOfFile() && depth > 0)
                            {
                                string propLine = PeekLine()?.Trim();
                                if (string.IsNullOrWhiteSpace(propLine)) { ConsumeLine(); continue; }
                                if (propLine.StartsWith("//")) { ConsumeLine(); continue; }

                                if (propLine.Contains("get")) p.HasGetter = true;
                                if (propLine.Contains("set")) p.HasSetter = true;

                                // Count braces (string-literal aware)
                                depth += CountBraces(PeekLine());
                                ConsumeLine();
                            }
                        }

                        foreach (var attr in memberAttrs) p.Attributes.Add(attr);
                        cls.Properties.Add(p);
                        if (!memberLine.TrimEnd().EndsWith("}")) { /* already consumed */ }
                        else { ConsumeLine(); }
                        continue;
                    }

                    // 3d. Indexer: public char this[int index] { get; set; } or multi-line
                    var indexerMatch = Regex.Match(memberLine, @"^\s*(public|private|protected|internal)?\s*(static\s+)?(sealed\s+)?(override\s+)?(virtual\s+)?(abstract\s+)?(\w+(?:<[^>]+>)?)\s+this\s*\[([^\]]+)\]\s*(\{)?");
                    if (indexerMatch.Success)
                    {
                        var p = new Property("Item", indexerMatch.Groups[7].Value)
                        {
                            Access = ParseAccess(indexerMatch.Groups[1].Value),
                            IsStatic = indexerMatch.Groups[2].Success,
                            IsSealed = indexerMatch.Groups[3].Success,
                            IsOverride = indexerMatch.Groups[4].Success,
                            IsVirtual = indexerMatch.Groups[5].Success,
                            IsAbstract = indexerMatch.Groups[6].Success,
                            IsIndexer = true
                        };
                        // Parse indexer parameters
                        string indexerParams = indexerMatch.Groups[8].Value;
                        foreach (var param in indexerParams.Split(','))
                        {
                            var parts = param.Trim().Split(' ');
                            if (parts.Length >= 2)
                                p.IndexerParameters.Add(new Parameter(parts.Last(), string.Join(" ", parts.Take(parts.Length - 1))));
                        }

                        if (indexerMatch.Groups[9].Value == "{")
                        {
                            // Multi-line indexer body
                            ConsumeLine();
                            int depth = 1;
                            while (!IsEndOfFile() && depth > 0)
                            {
                                string propLine = PeekLine()?.Trim();
                                if (string.IsNullOrWhiteSpace(propLine)) { ConsumeLine(); continue; }
                                if (propLine.StartsWith("//")) { ConsumeLine(); continue; }

                                if (propLine.Contains("get")) p.HasGetter = true;
                                if (propLine.Contains("set")) p.HasSetter = true;

                                depth += CountBraces(PeekLine());
                                ConsumeLine();
                            }
                        }
                        else
                        {
                            ConsumeLine(); // indexer without body
                        }

                        foreach (var attr in memberAttrs) p.Attributes.Add(attr);
                        cls.Properties.Add(p);
                        continue;
                    }

                    // 4. Constructors: public ClassName() { ... } or public ClassName(int value) : base(val) { ... }
                    var ctorMatch = Regex.Match(memberLine, @"^\s*(public|private|protected|internal)?\s*(static\s+)?(\w+)\s*\(([^)]*)\)\s*(?::\s*(base|this)\s*\(([^)]*)\))?\s*(\{)?");
                    if (ctorMatch.Success && ctorMatch.Groups[3].Value == cls.Name)
                    {
                        var m = new Method(ctorMatch.Groups[3].Value, "")
                        {
                            Access = ParseAccess(ctorMatch.Groups[1].Value),
                            IsConstructor = true
                        };
                        if (ctorMatch.Groups[2].Success)
                            m.Modifiers |= MethodModifier.Static;
                        foreach (var attr in memberAttrs) m.Attributes.Add(attr);

                        // Params
                        string paramsRaw = ctorMatch.Groups[4].Value;
                        ParseParameters(m.Parameters, paramsRaw);

                        // Constructor initializer: : base(...) or : this(...)
                        if (ctorMatch.Groups[5].Success)
                        {
                            string initKind = ctorMatch.Groups[5].Value; // "base" or "this"
                            string initArgs = ctorMatch.Groups[6].Value;
                            m.CtorInitializers.Add(new CtorInitializer(initKind, initArgs));
                        }

                        ConsumeLine(); // consume the constructor declaration line
                        if (ctorMatch.Groups[7].Value != "{")
                        {
                            // { is on a separate line — consume lines until we find it
                            while (!IsEndOfFile())
                            {
                                string nextLine = PeekLine()?.Trim();
                                if (nextLine == "{" || nextLine?.EndsWith("{") == true) break;
                                ConsumeLine();
                            }
                        }
                        // Parse the constructor body using ParseMethodBody
                        ParseMethodBody(m.Body);
                        cls.Methods.Add(m);
                        continue;
                    }

                    // 5. Static Operators: public static Int32 operator+(Int32 left, Int32 right) { ... }
                    var operatorMatch = Regex.Match(memberLine, @"^\s*(public|private|protected|internal)?\s*static\s+(\w+(?:<[^>]+>)?)\s+operator([+\-*\/%&|^<>=!]+|\[\])\s*\(([^)]*)\)\s*(\{)?");
                    if (operatorMatch.Success)
                    {
                        string returnType = operatorMatch.Groups[2].Value;
                        string op = operatorMatch.Groups[3].Value;
                        var oper = new OperatorOverload(op, returnType)
                        {
                            Access = ParseAccess(operatorMatch.Groups[1].Value)
                        };
                        foreach (var attr in memberAttrs) oper.Attributes.Add(attr);

                        string paramsRaw = operatorMatch.Groups[4].Value;
                        ParseParameters(oper.Parameters, paramsRaw);

                        ConsumeLine(); // consume the operator declaration line
                        if (operatorMatch.Groups[5].Value != "{")
                        {
                            // { is on a separate line — consume lines until we find it
                            while (!IsEndOfFile())
                            {
                                string nextLine = PeekLine()?.Trim();
                                if (nextLine == "{" || nextLine?.EndsWith("{") == true) break;
                                ConsumeLine();
                            }
                        }
                        // Parse the operator body using ParseMethodBody
                        ParseMethodBody(oper.Body);
                        cls.Operators.Add(oper);
                        continue;
                    }

                    // 5b. Instance operators: public String operator+(String& other) { ... }
                    // These are non-static operator overloads, parsed as methods with name "operator+"
                    var instanceOpMatch = Regex.Match(memberLine, @"^\s*(public|private|protected|internal)?\s*(override\s+)?(virtual\s+)?(\w+(?:<[^>]+>)?\s*\&?)\s+operator([+\-*\/%&|^<>=!]+|\[\])\s*\(([^)]*)\)\s*(\{)?");
                    if (instanceOpMatch.Success)
                    {
                        string returnType = instanceOpMatch.Groups[4].Value.Trim();
                        string op = instanceOpMatch.Groups[5].Value;
                        var m = new Method("operator" + op, returnType)
                        {
                            Access = ParseAccess(instanceOpMatch.Groups[1].Value)
                        };
                        if (instanceOpMatch.Groups[2].Success) m.Modifiers |= MethodModifier.Override;
                        if (instanceOpMatch.Groups[3].Success) m.Modifiers |= MethodModifier.Virtual;
                        foreach (var attr in memberAttrs) m.Attributes.Add(attr);

                        string paramsRaw = instanceOpMatch.Groups[6].Value;
                        ParseParameters(m.Parameters, paramsRaw);

                        ConsumeLine(); // consume the instance operator declaration line
                        if (instanceOpMatch.Groups[7].Value != "{")
                        {
                            // { is on a separate line — consume lines until we find it
                            while (!IsEndOfFile())
                            {
                                string nextLine = PeekLine()?.Trim();
                                if (nextLine == "{" || nextLine?.EndsWith("{") == true) break;
                                ConsumeLine();
                            }
                        }
                        // Parse the instance operator body using ParseMethodBody
                        ParseMethodBody(m.Body);
                        cls.Methods.Add(m);
                        continue;
                    }

                    // 5c. Conversion operators: public static implicit operator int(MyType v) { ... }
                    //     public static explicit operator string(MyType v) { ... }
                    var convOpMatch = Regex.Match(memberLine, @"^\s*(public|private|protected|internal)?\s*static\s+(implicit|explicit)\s+operator\s+(\w+(?:<[^>]+>)?)\s*\(([^)]*)\)\s*(\{)?");
                    if (convOpMatch.Success)
                    {
                        string convType = convOpMatch.Groups[2].Value; // "implicit" or "explicit"
                        string returnType = convOpMatch.Groups[3].Value;
                        var oper = new OperatorOverload(convType + " operator", returnType)
                        {
                            Access = ParseAccess(convOpMatch.Groups[1].Value)
                        };
                        foreach (var attr in memberAttrs) oper.Attributes.Add(attr);

                        string paramsRaw = convOpMatch.Groups[4].Value;
                        ParseParameters(oper.Parameters, paramsRaw);

                        ConsumeLine(); // consume the conversion operator declaration line
                        if (convOpMatch.Groups[5].Value != "{")
                        {
                            while (!IsEndOfFile())
                            {
                                string nextLine = PeekLine()?.Trim();
                                if (nextLine == "{" || nextLine?.EndsWith("{") == true) break;
                                ConsumeLine();
                            }
                        }
                        ParseMethodBody(oper.Body);
                        cls.Operators.Add(oper);
                        continue;
                    }

                    // 6. Methods: public void Move(int x, int y) { ... }
                    // Return type can include &, *, [], <>, etc. e.g. "String&", "List<int>"

                    // 6a. Tuple return type: public (string name, int age) GetPair()
                    //     Detected when the first non-modifier token is '('
                    var tupleReturnHead = Regex.Match(memberLine,
                        @"^\s*(public|private|protected|internal)?\s*(static\s+)?(new\s+)?(sealed\s+)?(override\s+)?(virtual\s+)?(async\s+)?\(");
                    if (tupleReturnHead.Success)
                    {
                        int parenStart = memberLine.IndexOf('(', tupleReturnHead.Index);
                        int depth2 = 0, parenEnd = -1;
                        for (int ki = parenStart; ki < memberLine.Length; ki++)
                        {
                            if (memberLine[ki] == '(') depth2++;
                            else if (memberLine[ki] == ')') { if (--depth2 == 0) { parenEnd = ki; break; } }
                        }
                        if (parenEnd >= 0)
                        {
                            // Build "(Type1, Type2)" — strip element names
                            string tupleInner = memberLine.Substring(parenStart + 1, parenEnd - parenStart - 1);
                            string tupleReturnType = "(" + string.Join(", ", tupleInner.Split(',')
                                .Select(elem => {
                                    var tp = elem.Trim().Split(new[]{' '}, System.StringSplitOptions.RemoveEmptyEntries);
                                    return tp.Length >= 1 ? tp[0] : elem.Trim();
                                })) + ")";

                            string afterTuple = memberLine.Substring(parenEnd + 1).Trim();
                            var tupleRest = Regex.Match(afterTuple, @"^(\w+)\s*\(([^)]*)\)\s*(\{)?");
                            if (tupleRest.Success)
                            {
                                string methodName = tupleRest.Groups[1].Value;
                                var m = new Method(methodName, tupleReturnType)
                                {
                                    Access = ParseAccess(tupleReturnHead.Groups[1].Value)
                                };
                                if (tupleReturnHead.Groups[2].Success) m.Modifiers |= MethodModifier.Static;
                                if (tupleReturnHead.Groups[3].Success) m.Modifiers |= MethodModifier.New;
                                if (tupleReturnHead.Groups[4].Success) m.Modifiers |= MethodModifier.Sealed;
                                if (tupleReturnHead.Groups[5].Success) m.Modifiers |= MethodModifier.Override;
                                if (tupleReturnHead.Groups[6].Success) m.Modifiers |= MethodModifier.Virtual;
                                if (tupleReturnHead.Groups[7].Success) m.Modifiers |= MethodModifier.Async;
                                foreach (var attr in memberAttrs) m.Attributes.Add(attr);

                                string paramsRaw2 = tupleRest.Groups[2].Value;
                                if (!string.IsNullOrWhiteSpace(paramsRaw2))
                                {
                                    foreach (var p in paramsRaw2.Split(','))
                                    {
                                        var parts = p.Trim().Split(' ');
                                        if (parts.Length == 2) m.Parameters.Add(new Parameter(parts[1], parts[0]));
                                    }
                                }
                                ConsumeLine(); // consume the tuple-return method declaration line
                                if (tupleRest.Groups[3].Value != "{")
                                {
                                    // { is on a separate line — consume lines until we find it
                                    while (!IsEndOfFile())
                                    {
                                        string nextLine = PeekLine()?.Trim();
                                        if (nextLine == "{" || nextLine?.EndsWith("{") == true) break;
                                        ConsumeLine();
                                    }
                                }
                                ParseMethodBody(m.Body);
                                cls.Methods.Add(m);
                                continue;
                            }
                        }
                    }

                    // 6b. Strategy: match everything, then extract method name as last word before (
                    var methodMatch = Regex.Match(memberLine, @"^\s*(public|private|protected|internal)?\s*(static\s+)?(new\s+)?(sealed\s+)?(override\s+)?(virtual\s+)?(abstract\s+)?(partial\s+)?(async\s+)?(extern\s+)?(.+)\s+((?:\w+\.)*\w+)\s*\(([^)]*)\)\s*(\{)?");
                    if (methodMatch.Success)
                    {
                        string rawReturnType = methodMatch.Groups[11].Value.Trim();
                        string methodName = methodMatch.Groups[12].Value;
                        
                        var m = new Method(methodName, rawReturnType)
                        {
                            Access = ParseAccess(methodMatch.Groups[1].Value)
                        };
                        if (methodMatch.Groups[2].Success) m.Modifiers |= MethodModifier.Static;
                        if (methodMatch.Groups[3].Success) m.Modifiers |= MethodModifier.New;
                        if (methodMatch.Groups[4].Success) m.Modifiers |= MethodModifier.Sealed;
                        if (methodMatch.Groups[5].Success) m.Modifiers |= MethodModifier.Override;
                        if (methodMatch.Groups[6].Success) m.Modifiers |= MethodModifier.Virtual;
                        if (methodMatch.Groups[7].Success) m.Modifiers |= MethodModifier.Abstract;
                        if (methodMatch.Groups[8].Success) m.Modifiers |= MethodModifier.Partial;
                        if (methodMatch.Groups[9].Success) m.Modifiers |= MethodModifier.Async;
                        bool isExtern = methodMatch.Groups[10].Success;
                        if (isExtern) m.Modifiers |= MethodModifier.Extern;

                        // Apply collected attributes (from lines before the method)
                        foreach (var attr in memberAttrs) m.Attributes.Add(attr);

                        // Also check for inline attributes on the same line
                        var attrMatch = Regex.Match(memberLine, @"^(\[[^\]]+\])\s*");
                        if (attrMatch.Success)
                        {
                            string attrText = attrMatch.Groups[1].Value;
                            if (attrText == "[Inline]")
                            {
                                m.Attributes.Add(new SourceAttribute("Inline"));
                            }
                        }

                        // Handle constructors (no return type)
                        if (string.IsNullOrEmpty(rawReturnType) && methodName == cls.Name)
                        {
                            m.IsConstructor = true;
                        }

                        // Params
                        string paramsRaw = methodMatch.Groups[13].Value;
                        ParseParameters(m.Parameters, paramsRaw);

                        // Abstract/partial/extern methods end with ; not {
                        bool isAbstractOrPartial = (memberLine.TrimEnd().EndsWith(";") && !memberLine.TrimEnd().EndsWith("{}")) || isExtern;
                        if (isAbstractOrPartial)
                        {
                            ConsumeLine(); // consume the abstract/partial method declaration line
                            // No body to parse — abstract methods have no implementation
                            cls.Methods.Add(m);
                            continue;
                        }

                        ConsumeLine(); // consume the method declaration line
                        if (methodMatch.Groups[14].Value != "{")
                        {
                            // { is on a separate line — consume lines until we find it
                            while (!IsEndOfFile())
                            {
                                string nextLine = PeekLine()?.Trim();
                                if (nextLine == "{" || nextLine?.EndsWith("{") == true) break;
                                ConsumeLine();
                            }
                        }
                        // Parse the method body using ParseStatement
                        ParseMethodBody(m.Body);
                        cls.Methods.Add(m);
                        continue;
                    }

                    // 7. Finalizer: ~ClassName() { ... }
                    var finalizerMatch = Regex.Match(memberLine, @"^\s*~(\w+)\s*\(([^)]*)\)\s*(\{)?");
                    if (finalizerMatch.Success && finalizerMatch.Groups[1].Value == cls.Name)
                    {
                        var m = new Method(cls.Name, "void")
                        {
                            IsDestructor = true,
                            Access = AccessModifier.Public
                        };
                        foreach (var attr in memberAttrs) m.Attributes.Add(attr);

                        ConsumeLine(); // consume the finalizer declaration line
                        if (finalizerMatch.Groups[3].Value != "{")
                        {
                            // { is on a separate line — consume lines until we find it
                            while (!IsEndOfFile())
                            {
                                string nextLine = PeekLine()?.Trim();
                                if (nextLine == "{" || nextLine?.EndsWith("{") == true) break;
                                ConsumeLine();
                            }
                        }
                        // Parse the finalizer body using ParseMethodBody
                        ParseMethodBody(m.Body);
                        cls.Finalizer = m;
                        continue;
                    }

                    // 8. Companion object: companion object { ... } or companion object Name { ... }
                    var companionMatch = Regex.Match(memberLine, @"^\s*companion\s+object(?:\s+(\w+))?\s*\{");
                    if (companionMatch.Success)
                    {
                        var companion = new CompanionObject
                        {
                            Name = companionMatch.Groups[1].Success ? companionMatch.Groups[1].Value : null
                        };
                        ConsumeLine(); // consume the "companion object {" line
                        int depth = 1;
                        while (!IsEndOfFile() && depth > 0)
                        {
                            string compLine = PeekLine()?.Trim();
                            if (string.IsNullOrWhiteSpace(compLine) || compLine.StartsWith("//")) { ConsumeLine(); continue; }
                            if (compLine == "}") { ConsumeLine(); depth--; continue; }

                            var compAttrs = CollectAttributes();
                            compLine = PeekLine()?.Trim();
                            if (compLine == null) break;

                            // Constants
                            var compConstMatch = Regex.Match(compLine, @"^\s*(public|private|protected|internal)?\s*const\s+(\w+)\s+(\w+)\s*=\s*([^;]+);\s*$");
                            if (compConstMatch.Success)
                            {
                                var c = new Constant(compConstMatch.Groups[3].Value, compConstMatch.Groups[2].Value, compConstMatch.Groups[4].Value.Trim())
                                { Access = ParseAccess(compConstMatch.Groups[1].Value) };
                                foreach (var attr in compAttrs) c.Attributes.Add(attr);
                                companion.Constants.Add(c);
                                ConsumeLine();
                                continue;
                            }

                            // Methods
                            var compMethodMatch = Regex.Match(compLine, @"^\s*(public|private|protected|internal)?\s*(static\s+)?(\w+)\s+(\w+)\s*\(([^)]*)\)\s*(\{)?");
                            if (compMethodMatch.Success)
                            {
                                var m = new Method(compMethodMatch.Groups[4].Value, compMethodMatch.Groups[3].Value)
                                {
                                    Access = ParseAccess(compMethodMatch.Groups[1].Value)
                                };
                                m.Modifiers |= MethodModifier.Static; // companion methods are always static
                                foreach (var attr in compAttrs) m.Attributes.Add(attr);

                                string paramsRaw = compMethodMatch.Groups[5].Value;
                                if (!string.IsNullOrWhiteSpace(paramsRaw))
                                    foreach (var p in paramsRaw.Split(','))
                                    {
                                        var parts = p.Trim().Split(' ');
                                        if (parts.Length == 2) m.Parameters.Add(new Parameter(parts[1], parts[0]));
                                    }

                                ConsumeLine(); // consume the companion method declaration line
                                if (compMethodMatch.Groups[6].Value != "{")
                                {
                                    // { is on a separate line — consume lines until we find it
                                    while (!IsEndOfFile())
                                    {
                                        string nextLine = PeekLine()?.Trim();
                                        if (nextLine == "{" || nextLine?.EndsWith("{") == true) break;
                                        ConsumeLine();
                                    }
                                }
                                // Parse the companion method body using ParseMethodBody
                                ParseMethodBody(m.Body);

                                companion.Methods.Add(m);
                                continue;
                            }

                            ConsumeLine(); // skip unknown in companion
                        }
                        cls.Companion = companion;
                        continue;
                    }

                    // 9. Event: public event EventHandler OnClick;
                    var eventMatch = Regex.Match(memberLine, @"^\s*(public|private|protected|internal)?\s*(static\s+)?(virtual\s+)?(override\s+)?event\s+(\w+)\s+(\w+)\s*;");
                    if (eventMatch.Success)
                    {
                        var evt = new Event(eventMatch.Groups[6].Value, eventMatch.Groups[5].Value)
                        {
                            Access = ParseAccess(eventMatch.Groups[1].Value),
                            IsStatic = eventMatch.Groups[2].Success,
                            IsVirtual = eventMatch.Groups[3].Success,
                            IsOverride = eventMatch.Groups[4].Success
                        };
                        foreach (var attr in memberAttrs) evt.Attributes.Add(attr);
                        cls.Events.Add(evt);
                        ConsumeLine();
                        continue;
                    }

                    // 10. Nested class/struct
                    if (IsClassOrStructLine(memberLine))
                    {
                        var nested = ParseClass();
                        if (nested != null) cls.NestedClasses.Add(nested);
                        continue;
                    }

                    // 11. Nested enum
                    if (StartsWithAccessAndKeyword(memberLine, DbKeywords.Enum))
                    {
                        cls.NestedEnums.Add(ParseEnum());
                        continue;
                    }

                    // 12. Nested delegate
                    if (StartsWithAccessAndKeyword(memberLine, DbKeywords.Delegate))
                    {
                        cls.NestedDelegates.Add(ParseDelegate());
                        continue;
                    }

                    ConsumeLine();
                    }
                    catch (Exception ex)
                    {
                        // Error recovery: skip the current line and continue parsing
                        System.Diagnostics.Debug.WriteLine($"[DbParser] Skipping member due to parse error: {ex.Message}");
                        ConsumeLine();
                    }
                }
            return cls;
        }

        private AccessModifier ParseAccess(string access)
        {
            return access switch
            {
                "public"    => AccessModifier.Public,
                "private"   => AccessModifier.Private,
                "protected"  => AccessModifier.Protected,
                "internal"   => AccessModifier.Internal,
                _           => AccessModifier.Private
            };
        }

        private EnumDecl ParseEnum()
        {
            string line = ConsumeLine().Trim();
            // Strip optional access modifier: "public enum X {", "private enum X {"
            string stripped = line;
            AccessModifier access = AccessModifier.Public; // default
            foreach (var mod in new[] { "public ", "private ", "protected ", "internal " })
            {
                if (stripped.StartsWith(mod))
                {
                    access = mod switch
                    {
                        "public " => AccessModifier.Public,
                        "private " => AccessModifier.Private,
                        "protected " => AccessModifier.Protected,
                        "internal " => AccessModifier.Internal,
                        _ => AccessModifier.Public
                    };
                    stripped = stripped.Substring(mod.Length).TrimStart();
                    break;
                }
            }

            // The opening brace may be on the same line or the next line
            if (!stripped.Contains("{"))
            {
                // Brace is on the next line — consume it
                string nextLine = PeekLine()?.Trim();
                if (nextLine == "{")
                    ConsumeLine();
            }

            var match = Regex.Match(stripped, @"enum\s+([a-zA-Z0-9_]+)(?:\s*:\s*(\w+))?");
            if (!match.Success) throw new Exception($"Invalid enum declaration: {line}");

            var en = new EnumDecl(match.Groups[1].Value) { Access = access };
            if (match.Groups[2].Success && !string.IsNullOrEmpty(match.Groups[2].Value))
                en.UnderlyingType = match.Groups[2].Value;

            while (!IsEndOfFile())
            {
                string member = PeekLine()?.Trim();
                if (member == "}") { ConsumeLine(); break; }
                if (string.IsNullOrWhiteSpace(member)) { ConsumeLine(); continue; }
                if (member.StartsWith("///")) { ConsumeLine(); continue; }
                if (member.StartsWith("[")) { ConsumeLine(); continue; } // skip attributes
                string cleanMember = member.TrimEnd(',').Trim();
                // Handle explicit enum values: Name = Value
                if (cleanMember.Contains("="))
                {
                    int eqIdx = cleanMember.IndexOf('=');
                    string name = cleanMember.Substring(0, eqIdx).Trim();
                    string value = cleanMember.Substring(eqIdx + 1).Trim();
                    en.Members.Add($"{name} = {value}");
                }
                else
                {
                    en.Members.Add(cleanMember);
                }
                ConsumeLine();
            }
            return en;
        }

        private DelegateDecl ParseDelegate()
        {
            string line = ConsumeLine().Trim();
            // Support: delegate ReturnType Name(params); with generic return types
            var match = Regex.Match(line, @"delegate\s+((?:\w+\.)*\w+(?:<[^>]+>)?(?:\[\])?)\s+([a-zA-Z0-9_]+)\s*\(([^)]*)\)\s*;");
            if (!match.Success) throw new Exception($"Invalid delegate declaration: {line}");

            var del = new DelegateDecl(match.Groups[2].Value, match.Groups[1].Value.Trim());
            string paramsRaw = match.Groups[3].Value;
            if (!string.IsNullOrWhiteSpace(paramsRaw))
            {
                ParseParameters(del.Parameters, paramsRaw);
            }
            return del;
        }

        /// <summary>
        /// Parses a mixin declaration: <c>with TypeName { ... }</c> or <c>public with TypeName { ... }</c>.
        /// Mixins add methods to an existing type without inheritance.
        /// </summary>
        private MixinDeclaration ParseMixin()
        {
            string line = ConsumeLine().Trim();
            var match = Regex.Match(line, @"^\s*(public|private|protected|internal)?\s*with\s+(\w+(?:\.\w+)*)\s*\{?");
            if (!match.Success) throw new Exception($"Invalid mixin declaration: {line}");

            var mixin = new MixinDeclaration(match.Groups[2].Value)
            {
                Access = ParseAccess(match.Groups[1].Value)
            };

            // If opening brace wasn't on the same line, consume until we find it
            if (!line.Contains("{"))
            {
                while (!IsEndOfFile())
                {
                    string nextLine = PeekLine()?.Trim();
                    if (nextLine == "{" || nextLine?.EndsWith("{") == true) break;
                    ConsumeLine();
                }
            }

            // Parse mixin body
            while (!IsEndOfFile())
            {
                string inner = PeekLine()?.Trim();
                if (string.IsNullOrWhiteSpace(inner) || inner.StartsWith("//")) { ConsumeLine(); continue; }
                if (inner == "}") { ConsumeLine(); break; }

                var memberAttrs = CollectAttributes();
                inner = PeekLine()?.Trim();
                if (inner == null) break;

                // Methods inside mixin
                var methodMatch = Regex.Match(inner, @"^\s*(public|private|protected|internal)?\s*(static\s+)?(override\s+)?(virtual\s+)?(\w+(?:<[^>]+>)?)\s+(\w+)\s*\(([^)]*)\)\s*(\{)?");
                if (methodMatch.Success)
                {
                    var m = new Method(methodMatch.Groups[6].Value, methodMatch.Groups[5].Value)
                    {
                        Access = ParseAccess(methodMatch.Groups[1].Value)
                    };
                    if (methodMatch.Groups[2].Success) m.Modifiers |= MethodModifier.Static;
                    if (methodMatch.Groups[3].Success) m.Modifiers |= MethodModifier.Override;
                    if (methodMatch.Groups[4].Success) m.Modifiers |= MethodModifier.Virtual;
                    foreach (var attr in memberAttrs) m.Attributes.Add(attr);

                    string paramsRaw = methodMatch.Groups[7].Value;
                    ParseParameters(m.Parameters, paramsRaw);

                    ConsumeLine();
                    if (methodMatch.Groups[8].Value != "{")
                    {
                        while (!IsEndOfFile())
                        {
                            string nextLine = PeekLine()?.Trim();
                            if (nextLine == "{" || nextLine?.EndsWith("{") == true) break;
                            ConsumeLine();
                        }
                    }
                    ParseMethodBody(m.Body);
                    mixin.Methods.Add(m);
                    continue;
                }

                // Properties inside mixin
                var propMatch = Regex.Match(inner, @"^\s*(public|private|protected|internal)?\s*(static\s+)?(override\s+)?(virtual\s+)?(\w+(?:<[^>]+>)?)\s+(\w+)\s*\{\s*(get;\s*set;|set;\s*get;|get;)\s*\}");
                if (propMatch.Success)
                {
                    var p = new Property(propMatch.Groups[6].Value, propMatch.Groups[5].Value)
                    {
                        Access = ParseAccess(propMatch.Groups[1].Value),
                        HasGetter = propMatch.Groups[7].Value.Contains("get;"),
                        HasSetter = propMatch.Groups[7].Value.Contains("set;"),
                        IsStatic = propMatch.Groups[2].Success,
                        IsOverride = propMatch.Groups[3].Success,
                        IsVirtual = propMatch.Groups[4].Success
                    };
                    foreach (var attr in memberAttrs) p.Attributes.Add(attr);
                    mixin.Properties.Add(p);
                    ConsumeLine();
                    continue;
                }

                ConsumeLine(); // skip unknown
            }
            return mixin;
        }

        // ── New top-level declaration parsers ──────────────────────────────────

        private InterfaceDeclaration ParseInterface()
        {
            string line = ConsumeLine().Trim();
            var match = Regex.Match(line, @"^\s*(public|private|protected|internal)?\s*interface\s+(\w+)(?:<([^>]+)>)?\s*(?::\s*([^{]+))?\s*\{?");
            if (!match.Success) throw new Exception($"Invalid interface declaration: {line}");

            var iface = new InterfaceDeclaration(match.Groups[2].Value)
            {
                Access = ParseAccess(match.Groups[1].Value)
            };

            // Type parameters
            if (match.Groups[3].Success && !string.IsNullOrWhiteSpace(match.Groups[3].Value))
                foreach (var tp in match.Groups[3].Value.Split(','))
                    iface.TypeParams.Add(tp.Trim());

            // Extends
            if (match.Groups[4].Success && !string.IsNullOrWhiteSpace(match.Groups[4].Value))
                foreach (var ext in match.Groups[4].Value.Split(','))
                    iface.Extends.Add(ext.Trim());

            // Parse interface body
            if (!line.TrimEnd().EndsWith("{"))
                ConsumeLine(); // consume the opening brace line if not on same line

            while (!IsEndOfFile())
            {
                string inner = PeekLine()?.Trim();
                if (string.IsNullOrWhiteSpace(inner) || inner.StartsWith("//")) { ConsumeLine(); continue; }
                if (inner == "}") { ConsumeLine(); break; }

                // Collect attributes
                var memberAttrs = CollectAttributes();
                inner = PeekLine()?.Trim();
                if (inner == null) break;

                // Method declaration — allow with or without trailing semicolon, and support generic return types
                var methodMatch = Regex.Match(inner, @"^\s*(public|private|protected|internal)?\s*(static\s+)?(override\s+)?(virtual\s+)?(abstract\s+)?((?:\w+\.)*\w+(?:<[^>]+>)?(?:\[\])?)\s+(\w+)\s*\(([^)]*)\)\s*;?\s*$");
                if (methodMatch.Success)
                {
                    var m = new Method(methodMatch.Groups[7].Value, methodMatch.Groups[6].Value)
                    {
                        Access = ParseAccess(methodMatch.Groups[1].Value)
                    };
                    if (methodMatch.Groups[5].Success) m.Modifiers |= MethodModifier.Abstract;
                    if (methodMatch.Groups[2].Success) m.Modifiers |= MethodModifier.Static;
                    if (methodMatch.Groups[3].Success) m.Modifiers |= MethodModifier.Override;
                    if (methodMatch.Groups[4].Success) m.Modifiers |= MethodModifier.Virtual;
                    foreach (var attr in memberAttrs) m.Attributes.Add(attr);

                    string paramsRaw = methodMatch.Groups[8].Value;
                    if (!string.IsNullOrWhiteSpace(paramsRaw))
                        ParseParameters(m.Parameters, paramsRaw);
                    iface.Methods.Add(m);
                    ConsumeLine();
                    continue;
                }

                // Property declaration
                var propMatch = Regex.Match(inner, @"^\s*(public|private|protected|internal)?\s*(static\s+)?(override\s+)?(virtual\s+)?(\w+)\s+(\w+)\s*\{\s*(get;\s*set;|set;\s*get;|get;)\s*\}");
                if (propMatch.Success)
                {
                    var p = new Property(propMatch.Groups[6].Value, propMatch.Groups[5].Value)
                    {
                        Access = ParseAccess(propMatch.Groups[1].Value),
                        HasGetter = propMatch.Groups[7].Value.Contains("get;"),
                        HasSetter = propMatch.Groups[7].Value.Contains("set;"),
                        IsStatic = propMatch.Groups[2].Success,
                        IsOverride = propMatch.Groups[3].Success,
                        IsVirtual = propMatch.Groups[4].Success
                    };
                    foreach (var attr in memberAttrs) p.Attributes.Add(attr);
                    iface.Properties.Add(p);
                    ConsumeLine();
                    continue;
                }

                ConsumeLine(); // skip unknown
            }
            return iface;
        }

        private SumTypeDeclaration ParseSumType()
        {
            string line = ConsumeLine().Trim();
            // sum TypeName or enum TypeName (with | variants)
            var match = Regex.Match(line, @"^\s*(public|private|protected|internal)?\s*(sum|enum)\s+(\w+)(?:<([^>]+)>)?\s*\{?");
            if (!match.Success) throw new Exception($"Invalid sum type declaration: {line}");

            var sum = new SumTypeDeclaration(match.Groups[3].Value)
            {
                Access = ParseAccess(match.Groups[1].Value)
            };

            if (match.Groups[4].Success && !string.IsNullOrWhiteSpace(match.Groups[4].Value))
                foreach (var tp in match.Groups[4].Value.Split(','))
                    sum.TypeParams.Add(tp.Trim());

            // Parse variants until closing brace
            while (!IsEndOfFile())
            {
                string inner = PeekLine()?.Trim();
                if (string.IsNullOrWhiteSpace(inner) || inner.StartsWith("//")) { ConsumeLine(); continue; }
                if (inner == "}") { ConsumeLine(); break; }

                // Variant: Name or Name(Type1, Type2) or Name(Type1 name1, Type2 name2) — supports generic types like Dictionary<string, int>
                var variantMatch = Regex.Match(inner, @"^\s*(\w+)(?:\s*\((.+)\))?\s*,?\s*$");
                if (variantMatch.Success)
                {
                    var variant = new SumVariant(variantMatch.Groups[1].Value);
                    if (variantMatch.Groups[2].Success && !string.IsNullOrWhiteSpace(variantMatch.Groups[2].Value))
                    {
                        foreach (var param in SplitTopLevelCommas(variantMatch.Groups[2].Value))
                        {
                            var parts = param.Trim().Split(' ');
                            if (parts.Length >= 2)
                                variant.Data.Add(new Parameter(parts.Last(), string.Join(" ", parts.Take(parts.Length - 1))));
                            else if (parts.Length == 1 && !string.IsNullOrWhiteSpace(parts[0]))
                                variant.Data.Add(new Parameter("", parts[0]));
                        }
                    }
                    sum.Variants.Add(variant);
                }
                ConsumeLine();
            }
            return sum;
        }

        private TypeAliasDeclaration ParseTypeAlias()
        {
            string line = ConsumeLine().Trim();
            // type AliasName = TargetType;
            var match = Regex.Match(line, @"^\s*(public|private|protected|internal)?\s*type\s+(\w+)\s*=\s*([^;]+);");
            if (!match.Success) throw new Exception($"Invalid type alias declaration: {line}");

            var alias = new TypeAliasDeclaration(match.Groups[2].Value, match.Groups[3].Value.Trim())
            {
                Access = ParseAccess(match.Groups[1].Value)
            };
            return alias;
        }

        private FileScopeDirective ParseFileScopeDirective()
        {
            string line = ConsumeLine().Trim();
            // #include <header>, #include "header", #pragma once, #define MACRO
            if (line.StartsWith("#include"))
            {
                string rest = line.Substring(8).Trim();
                bool isSystem = rest.StartsWith("<");
                string target = isSystem
                    ? rest.Trim('<', '>').Trim()
                    : rest.Trim('"').Trim();
                return new FileScopeDirective("include", target) { IsSystem = isSystem };
            }
            else if (line.StartsWith("#pragma"))
            {
                string target = line.Substring(7).Trim();
                return new FileScopeDirective("pragma", target);
            }
            else if (line.StartsWith("#define"))
            {
                string target = line.Substring(7).Trim();
                return new FileScopeDirective("define", target);
            }
            // Fallback
            return new FileScopeDirective("unknown", line);
        }

        // ── Helper Methods ─────────────────────────────────────────────────────

        private static bool IsClassOrStructLine(string line)
        {
            // Match lines like "class Foo", "struct Foo", "stub Foo", "public class Foo",
            // "abstract class Foo", "sealed class Foo", "readonly struct Foo", "partial class Foo",
            // "abstract sealed class Foo", etc.
            return Regex.IsMatch(line, @"^\s*(public|private|protected|internal)?\s*(?:(?:abstract|sealed|static|partial|readonly)\s+)*(class|struct|stub)\s+");
        }

        /// <summary>
        /// Checks whether a trimmed line starts with a given keyword followed by a space, paren, or brace.
        /// Delegates to <see cref="DbKeywords.StartsWithKeyword"/> for consistent boundary checking.
        /// </summary>
        private static bool StartsWithKeyword(string line, string keyword)
            => DbKeywords.StartsWithKeyword(line, keyword);

        /// <summary>
        /// Checks if a line starts with an optional access modifier followed by the keyword.
        /// Handles: "enum X", "public enum X", "private enum X", etc.
        /// </summary>
        private static bool StartsWithAccessAndKeyword(string line, string keyword)
        {
            string trimmed = line.TrimStart();
            foreach (var mod in new[] { "public ", "private ", "protected ", "internal " })
            {
                if (trimmed.StartsWith(mod))
                {
                    trimmed = trimmed.Substring(mod.Length).TrimStart();
                    break;
                }
            }
            return StartsWithKeyword(trimmed, keyword);
        }

        private bool IsEndOfFile() => _currentLine >= _lines.Length;

        private void ParseParameters(List<Parameter> parametersList, string paramsRaw)
        {
            if (string.IsNullOrWhiteSpace(paramsRaw)) return;

            // Split on commas that are at depth 0 (outside of < > ( ) [ ])
            var paramParts = SplitTopLevelCommas(paramsRaw);

            foreach (var p in paramParts)
            {
                string trimmed = p.Trim();
                if (string.IsNullOrWhiteSpace(trimmed)) continue;

                var parts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) continue;

                string modifier = "";
                bool isParams = false;
                bool isOptional = false;
                int startIndex = 0;

                if (parts[0] == "ref" || parts[0] == "out" || parts[0] == "in" || parts[0] == "this")
                {
                    modifier = parts[0];
                    startIndex = 1;
                }
                else if (parts[0] == "params")
                {
                    isParams = true;
                    startIndex = 1;
                }

                // Check for default value: Type name = expr
                string defaultValue = null;
                int eqIdx = trimmed.IndexOf('=');
                if (eqIdx > 0)
                {
                    // Make sure it's not ==, !=, <=, >=, =>
                    char afterEq = eqIdx + 1 < trimmed.Length ? trimmed[eqIdx + 1] : '\0';
                    char beforeEq = eqIdx > 0 ? trimmed[eqIdx - 1] : '\0';
                    if (afterEq != '=' && beforeEq != '!' && beforeEq != '<' && beforeEq != '>' && afterEq != '>')
                    {
                        defaultValue = trimmed.Substring(eqIdx + 1).Trim().TrimEnd();
                        trimmed = trimmed.Substring(0, eqIdx).Trim();
                        isOptional = true;
                        // Re-split without the default value
                        parts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    }
                }

                if (parts.Length - startIndex >= 2)
                {
                    // Type can include generics: Dictionary<string, int> name
                    // Everything before the last word is the type
                    string name = parts[parts.Length - 1];
                    string type = string.Join(" ", parts.Skip(startIndex).Take(parts.Length - startIndex - 1));
                    var param = new Parameter(name, type);
                    param.Modifier = modifier;
                    param.IsParams = isParams;
                    if (isOptional) param.Default = defaultValue;
                    parametersList.Add(param);
                }
                else if (parts.Length - startIndex == 1)
                {
                    var param = new Parameter("", parts[startIndex]);
                    param.Modifier = modifier;
                    param.IsParams = isParams;
                    if (isOptional) param.Default = defaultValue;
                    parametersList.Add(param);
                }
            }
        }

        private string ConsumeLine()
        {
            string line = _lines[_currentLine];
            _currentLine++;
            return line;
        }

        private string PeekLine() => IsEndOfFile() ? null : _lines[_currentLine];

        /// <summary>
        /// Parses a [Represents("...")] attribute string into a DbAttribute,
        /// determining the MemberAccess from the content:
        ///   - Starts with "->" → Arrow
        ///   - Contains "::" → Colons (static/qualified access)
        ///   - Starts with "." or is a plain member name → Dot
        ///   - Starts with ".*" → DotAsterisk
        ///   - Starts with "->*" → ArrowAsterisk
        ///   - Starts with "?." → QuestionMarkDot
        ///   - Starts with "?[]" → QuestionMarkBracket
        ///   - Starts with "[]" → Bracket
        /// </summary>
        private SourceAttribute ParseRepresentsAttribute(string attrText)
        {
            // attrText is like: [Represents("std::string::size()")]
            // Extract the content inside the quotes
            var innerMatch = Regex.Match(attrText, @"\[""Represents""\(([^)]+)\)\]");
            if (!innerMatch.Success)
                innerMatch = Regex.Match(attrText, @"\[Represents\(""([^""]+)""\)\]");

            string content = innerMatch.Success ? innerMatch.Groups[1].Value.Trim() : attrText;

            var attr = new SourceAttribute("Represents");
            attr.Arguments.Add(content);

            // Determine MemberAccess from the content
            if (content.StartsWith("->*"))
                attr.Access = MemberAccess.ArrowAsterisk;
            else if (content.StartsWith("->"))
                attr.Access = MemberAccess.Arrow;
            else if (content.StartsWith("?.["))
                attr.Access = MemberAccess.QuestionMarkBracket;
            else if (content.StartsWith("?."))
                attr.Access = MemberAccess.QuestionMarkDot;
            else if (content.StartsWith("."))
                attr.Access = MemberAccess.Dot;
            else if (content.Contains("::"))
                attr.Access = MemberAccess.Colons;
            else if (content == "operator[]")
                attr.Access = MemberAccess.Bracket;
            else
                attr.Access = MemberAccess.Dot; // Default: plain member name

            return attr;
        }

        /// <summary>
        /// Collects any attribute lines (like [Represents(...)]) that appear
        /// before the current position, consuming them.
        /// Returns the list of parsed attributes.
        /// </summary>
        private List<SourceAttribute> CollectAttributes()
        {
            var attributes = new List<SourceAttribute>();
            while (!IsEndOfFile())
            {
                string peek = PeekLine()?.Trim();
                // Don't consume blank lines — let the caller decide what to do with them
                if (string.IsNullOrWhiteSpace(peek)) break;
                if (!peek.StartsWith("[")) break;

                // It's an attribute line — consume it
                string attrLine = ConsumeLine().Trim();

                // Handle multi-line attributes: if the line starts with [ but doesn't end with ], keep consuming
                if (attrLine.StartsWith("[") && !attrLine.EndsWith("]"))
                {
                    while (!IsEndOfFile())
                    {
                        string nextLine = ConsumeLine().Trim();
                        attrLine += " " + nextLine;
                        if (nextLine.EndsWith("]")) break;
                    }
                }

                // Parse [Represents("...")]
                var representsMatch = Regex.Match(attrLine, @"\[Represents\(""([^""]+)""\)\]");
                if (representsMatch.Success)
                {
                    string content = representsMatch.Groups[1].Value;
                    var attr = new SourceAttribute("Represents");
                    attr.Arguments.Add(content);

                    if (content.StartsWith("->*"))
                        attr.Access = MemberAccess.ArrowAsterisk;
                    else if (content.StartsWith("->"))
                        attr.Access = MemberAccess.Arrow;
                    else if (content.StartsWith("?.["))
                        attr.Access = MemberAccess.QuestionMarkBracket;
                    else if (content.StartsWith("?."))
                        attr.Access = MemberAccess.QuestionMarkDot;
                    else if (content.StartsWith("."))
                        attr.Access = MemberAccess.Dot;
                    else if (content.Contains("::"))
                        attr.Access = MemberAccess.Colons;
                    else
                        attr.Access = MemberAccess.Dot;

                    attributes.Add(attr);
                }
                else if (attrLine == "[Inline]")
                {
                    attributes.Add(new SourceAttribute("Inline"));
                }
                else if (attrLine == "[Stack]")
                {
                    attributes.Add(new SourceAttribute("Stack"));
                }
                else if (attrLine == "[Heap]")
                {
                    attributes.Add(new SourceAttribute("Heap"));
                }
                else if (attrLine == "[Shared]")
                {
                    attributes.Add(new SourceAttribute("Shared"));
                }
                else if (attrLine == "[Raw]")
                {
                    attributes.Add(new SourceAttribute("Raw"));
                }
                else if (attrLine == "[Address]")
                {
                    var attr = new SourceAttribute("Address");
                    attr.Decoration = TypeDecoration.Address;
                    attributes.Add(attr);
                }
                else if (attrLine == "[Reference]")
                {
                    var attr = new SourceAttribute("Reference");
                    attr.Decoration = TypeDecoration.Reference;
                    attributes.Add(attr);
                }
                else if (attrLine == "[Naked]")
                {
                    var attr = new SourceAttribute("Naked");
                    attr.Decoration = TypeDecoration.Naked;
                    attributes.Add(attr);
                }
                else if (attrLine == "[Const]")
                {
                    attributes.Add(new SourceAttribute("Const"));
                }
                else if (attrLine == "[Explicit]")
                {
                    attributes.Add(new SourceAttribute("Explicit"));
                }
                else if (attrLine == "[Noexcept]")
                {
                    attributes.Add(new SourceAttribute("Noexcept"));
                }
                else if (attrLine == "[Mutable]")
                {
                    attributes.Add(new SourceAttribute("Mutable"));
                }
                else
                {
                    // Generic attribute: [Friend(Player)], [Represents(...)], etc.
                    var genericMatch = Regex.Match(attrLine, @"^\[([A-Za-z_]\w*)(?:\(([^)]*)\))?\]$");
                    if (genericMatch.Success)
                    {
                        var attr = new SourceAttribute(genericMatch.Groups[1].Value);
                        if (genericMatch.Groups[2].Success && !string.IsNullOrEmpty(genericMatch.Groups[2].Value))
                        {
                            // Split arguments by comma, handling quoted strings
                            string argsRaw = genericMatch.Groups[2].Value;
                            foreach (var arg in argsRaw.Split(','))
                                attr.Arguments.Add(arg.Trim().Trim('"'));
                        }
                        attributes.Add(attr);
                    }
                }
            }
            return attributes;
        }

        /// <summary>
        /// Parses a method body: reads lines until the closing brace,
        /// parsing each statement into the method's Body list.
        /// </summary>
        private void ParseMethodBody(List<Statement> body)
        {
            int depth = 1;
            while (!IsEndOfFile() && depth > 0)
            {
                string innerLine = PeekLine()?.Trim();
                if (innerLine == null) break;

                if (innerLine == "}")
                {
                    ConsumeLine();
                    depth--;
                    if (depth == 0) break;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(innerLine) || innerLine.StartsWith("//"))
                {
                    ConsumeLine();
                    continue;
                }

                // Parse the statement (ParseStatement handles all block types)
                ParseStatement(body);
            }
        }

        private void ParseStatement(List<Statement> body)
        {
            string line = ConsumeLine().Trim();
            if (string.IsNullOrWhiteSpace(line)) return;

            // ── Return statement ─────────────────────────────────────────────
            if (StartsWithKeyword(line, DbKeywords.Return) || line == "return;")
            {
                if (line == "return;")
                {
                    body.Add(new ReturnStatement());
                }
                else
                {
                    string exprText = line.Substring(7).Trim().TrimEnd(';');
                    exprText = TransformIfPipeline(exprText);
                    var parsedExpr = _exprParser.ParseExpression(exprText);
                    if (parsedExpr != null)
                    {
                        body.Add(new ReturnStatement(parsedExpr));
                    }
                    else
                    {
                        // Fallback to raw text if expression parsing fails
                        body.Add(new ReturnStatement(exprText));
                    }
                }
                return;
            }

            // ── Break / Continue ──────────────────────────────────────────────
            if (line == DbKeywords.Break + ";")
            {
                body.Add(new BreakStatement());
                return;
            }
            if (line == DbKeywords.Continue + ";")
            {
                body.Add(new ContinueStatement());
                return;
            }

            // ── Using statement: using (var x = expr) { ... } ──────────────
            if (StartsWithKeyword(line, DbKeywords.Using))
            {
                string usingContent = ExtractParenContent(line);
                if (!string.IsNullOrEmpty(usingContent))
                {
                    string resourceDecl = usingContent.Trim();
                    Variable resourceVar;
                    // Try: var x = expr  or  Type x = expr  or  Type x
                    var varMatch = Regex.Match(resourceDecl, @"^(var|\w+(?:<[^>]+>)?(?:\[\])?)\s+(\w+)\s*=\s*(.+)$");
                    if (varMatch.Success)
                    {
                        resourceVar = new Variable(varMatch.Groups[2].Value, varMatch.Groups[1].Value)
                        {
                            Initializer = varMatch.Groups[3].Value.Trim()
                        };
                        if (varMatch.Groups[1].Value == "var") resourceVar.IsInferred = true;
                    }
                    else
                    {
                        // Bare expression: using (mutex) { ... }
                        resourceVar = new Variable("", resourceDecl);
                    }

                    var usingBlock = new UsingBlock(resourceVar);
                    if (line.EndsWith("{") || PeekLine()?.Trim() == "{")
                    {
                        if (!line.EndsWith("{")) ConsumeLine();
                        int depth = 1;
                        while (!IsEndOfFile() && depth > 0)
                        {
                            string inner = PeekLine()?.Trim();
                            if (inner == "}") { ConsumeLine(); depth--; }
                            else if (inner != null)
                            {
                                ConsumeLine();
                                ParseStatement(usingBlock.Body);
                            }
                            else ConsumeLine();
                        }
                    }
                    body.Add(usingBlock);
                    return;
                }
            }

            // ── Lock statement: lock (expr) { ... } ────────────────────────
            if (StartsWithKeyword(line, DbKeywords.Lock))
            {
                string lockContent = ExtractParenContent(line);
                if (!string.IsNullOrEmpty(lockContent))
                {
                    string lockExpr = lockContent.Trim();
                    var lockBlock = new LockBlock(lockExpr);
                    if (line.EndsWith("{") || PeekLine()?.Trim() == "{")
                    {
                        if (!line.EndsWith("{")) ConsumeLine();
                        int depth = 1;
                        while (!IsEndOfFile() && depth > 0)
                        {
                            string inner = PeekLine()?.Trim();
                            if (inner == "}") { ConsumeLine(); depth--; }
                            else if (inner != null)
                            {
                                ConsumeLine();
                                ParseStatement(lockBlock.Body);
                            }
                            else ConsumeLine();
                        }
                    }
                    body.Add(lockBlock);
                    return;
                }
            }

            // ── Checked / Unchecked: checked { ... } / unchecked { ... } ────
            if (StartsWithKeyword(line, DbKeywords.Checked) || StartsWithKeyword(line, DbKeywords.Unchecked))
            {
                bool isChecked = StartsWithKeyword(line, DbKeywords.Checked);
                var checkedBlock = new CheckedBlock(isChecked);
                if (line.EndsWith("{") || PeekLine()?.Trim() == "{")
                {
                    if (!line.EndsWith("{")) ConsumeLine();
                    int depth = 1;
                    while (!IsEndOfFile() && depth > 0)
                    {
                        string inner = PeekLine()?.Trim();
                        if (inner == "}") { ConsumeLine(); depth--; }
                        else if (inner != null)
                        {
                            ConsumeLine();
                            ParseStatement(checkedBlock.Body);
                        }
                        else ConsumeLine();
                    }
                }
                body.Add(checkedBlock);
                return;
            }

            // ── Unsafe: unsafe { ... } ────────────────────────────────────────
            if (StartsWithKeyword(line, DbKeywords.Unsafe))
            {
                var unsafeBlock = new UnsafeBlock();
                if (line.EndsWith("{") || PeekLine()?.Trim() == "{")
                {
                    if (!line.EndsWith("{")) ConsumeLine();
                    int depth = 1;
                    while (!IsEndOfFile() && depth > 0)
                    {
                        string inner = PeekLine()?.Trim();
                        if (inner == "}") { ConsumeLine(); depth--; }
                        else if (inner != null)
                        {
                            ConsumeLine();
                            ParseStatement(unsafeBlock.Body);
                        }
                        else ConsumeLine();
                    }
                }
                body.Add(unsafeBlock);
                return;
            }

            // ── Fixed: fixed (Type* ptr = &expr) { ... } ──────────────────────
            if (StartsWithKeyword(line, DbKeywords.Fixed))
            {
                string decl = ExtractParenContent(line);
                var fixedStmt = new FixedStatement(decl);
                if (line.EndsWith("{") || PeekLine()?.Trim() == "{")
                {
                    if (!line.EndsWith("{")) ConsumeLine();
                    int depth = 1;
                    while (!IsEndOfFile() && depth > 0)
                    {
                        string inner = PeekLine()?.Trim();
                        if (inner == "}") { ConsumeLine(); depth--; }
                        else if (inner != null)
                        {
                            ConsumeLine();
                            ParseStatement(fixedStmt.Body);
                        }
                        else ConsumeLine();
                    }
                }
                body.Add(fixedStmt);
                return;
            }

            // ── Do-while ─────────────────────────────────────────────────────
            if (StartsWithKeyword(line, DbKeywords.Do))
            {
                var doWhile = new DoWhileStatement();
                if (line.EndsWith("{") || PeekLine()?.Trim() == "{")
                {
                    if (!line.EndsWith("{")) ConsumeLine();
                    int depth = 1;
                    while (!IsEndOfFile() && depth > 0)
                    {
                        string inner = PeekLine()?.Trim();
                        if (inner == "}") { ConsumeLine(); depth--; }
                        else if (inner != null)
                        {
                            ConsumeLine();
                            ParseStatement(doWhile.Body);
                        }
                        else ConsumeLine();
                    }
                }
                // Next line should be "while (condition);"
                string whileLine = PeekLine()?.Trim();
                if (whileLine != null && StartsWithKeyword(whileLine, DbKeywords.While))
                {
                    ConsumeLine();
                    string conditionText = ExtractParenContent(whileLine);
                    var parsedExpr = _exprParser.ParseExpression(conditionText);
                    doWhile.Condition = parsedExpr?.ToString() ?? conditionText;
                }
                body.Add(doWhile);
                return;
            }

            // ── Match statement ──────────────────────────────────────────────
            if (StartsWithKeyword(line, DbKeywords.Switch))
            {
                var match = new MatchStatement();
                string conditionText = ExtractParenContent(line);
                if (!string.IsNullOrEmpty(conditionText)) match.Expression = conditionText;

                if (line.EndsWith("{") || PeekLine()?.Trim() == "{")
                {
                    if (!line.EndsWith("{")) ConsumeLine();
                    // Parse match arms until closing brace
                    while (!IsEndOfFile())
                    {
                        string inner = PeekLine()?.Trim();
                        if (string.IsNullOrWhiteSpace(inner) || inner.StartsWith("//")) { ConsumeLine(); continue; }
                        if (inner == "}") { ConsumeLine(); break; }

                        // Match arm: pattern => { ... } or pattern if guard => { ... }
                        string armLine = ConsumeLine().Trim();
                        // Extract guard using ExtractParenContent for nested parens
                        var armMatch = Regex.Match(armLine, @"^\s*(.+?)\s*(?:if\s*\(([^)]+)\))?\s*=>\s*(\{)?");
                        if (armMatch.Success)
                        {
                            var arm = new MatchArm
                            {
                                Pattern = armMatch.Groups[1].Value.Trim(),
                                Guard = armMatch.Groups[2].Success ? armMatch.Groups[2].Value : null
                            };

                            if (armMatch.Groups[3].Value == "{")
                            {
                                // Block body
                                int depth = 1;
                                while (!IsEndOfFile() && depth > 0)
                                {
                                    string armInner = PeekLine()?.Trim();
                                    if (armInner == null) break;
                                    if (armInner == "}") { ConsumeLine(); depth--; }
                                    else
                                    {
                                        ConsumeLine();
                                        ParseStatement(arm.Body);
                                    }
                                }
                            }
                            else
                            {
                                // Expression body (single line after =>)
                                string expr = armLine.Substring(armLine.IndexOf("=>") + 2).Trim().TrimEnd(';');
                                arm.Body.Add(new Action(ActionKind.Other, expr));
                            }
                            match.Arms.Add(arm);
                        }
                    }
                }
                body.Add(match);
                return;
            }

            if (StartsWithKeyword(line, DbKeywords.If))
            {
                string expression = ExtractParenContent(line);
                if (string.IsNullOrEmpty(expression)) expression = line;

                var cond = new Condition(ConditionKind.If, expression);

                if (line.EndsWith("{") || PeekLine()?.Trim() == "{")
                {
                    if (!line.EndsWith("{")) ConsumeLine();
                    int depth = 1;
                    while (!IsEndOfFile() && depth > 0)
                    {
                        string inner = PeekLine()?.Trim();
                        if (inner == "}") { ConsumeLine(); depth--; }
                        else if (inner != null)
                        {
                            ConsumeLine();
                            ParseStatement(cond.Body);
                        }
                        else ConsumeLine();
                    }
                }

                // Handle else if / else chains
                while (!IsEndOfFile())
                {
                    string nextLine = PeekLine()?.Trim();
                    if (nextLine == null) break;

                    if (StartsWithKeyword(nextLine, DbKeywords.Else))
                    {
                        ConsumeLine(); // consume the "else" or "else if" line
                        string elseRest = nextLine.Substring(DbKeywords.Else.Length).Trim();

                        if (StartsWithKeyword(elseRest, DbKeywords.If) || elseRest.StartsWith("if(") || elseRest.StartsWith("if ("))
                        {
                            // else if (condition) { ... }
                            var elseIfExpr = Regex.Match(nextLine, @"\(([^)]+)\)");
                            string elseIfCondition = elseIfExpr.Success ? elseIfExpr.Groups[1].Value : elseRest;
                            var elseIfCond = new Condition(ConditionKind.ElseIf, elseIfCondition);

                            if (nextLine.EndsWith("{") || PeekLine()?.Trim() == "{")
                            {
                                if (!nextLine.EndsWith("{")) ConsumeLine();
                                int depth = 1;
                                while (!IsEndOfFile() && depth > 0)
                                {
                                    string inner = PeekLine()?.Trim();
                                    if (inner == "}") { ConsumeLine(); depth--; }
                                    else if (inner != null)
                                    {
                                        ConsumeLine();
                                        ParseStatement(elseIfCond.Body);
                                    }
                                    else ConsumeLine();
                                }
                            }
                            cond.ElseBody.Add(elseIfCond);
                            // Continue chaining: the elseIfCond becomes the new "cond" for further else chains
                            // We need to track the tail for further else/else if
                            // For simplicity, we add else-if as a nested Condition in ElseBody
                            // and continue checking for more else/else if
                            cond = elseIfCond; // chain from the latest else-if
                        }
                        else
                        {
                            // else { ... }
                            var elseCond = new Condition(ConditionKind.Else);
                            // Check if else is followed by { on the same line or next line
                            string elseLine = nextLine;
                            if (elseLine.Contains("{") || PeekLine()?.Trim() == "{")
                            {
                                if (!elseLine.Contains("{")) ConsumeLine();
                                int depth = 1;
                                while (!IsEndOfFile() && depth > 0)
                                {
                                    string inner = PeekLine()?.Trim();
                                    if (inner == "}") { ConsumeLine(); depth--; }
                                    else if (inner != null)
                                    {
                                        ConsumeLine();
                                        ParseStatement(elseCond.Body);
                                    }
                                    else ConsumeLine();
                                }
                            }
                            cond.ElseBody.Add(elseCond);
                            break; // else terminates the chain
                        }
                    }
                    else
                    {
                        break; // no else follows
                    }
                }

                // Find the original if condition to add to body
                // Walk back to find the root if
                body.Add(cond);
            }
            else if (StartsWithKeyword(line, DbKeywords.Unless) || line.StartsWith("unless("))
            {
                // unless (cond) { } → if (!(cond)) { }
                string expression = ExtractParenContent(line);
                if (string.IsNullOrEmpty(expression)) expression = line;

                var unlessCond = new Condition(ConditionKind.Unless, expression);
                if (line.EndsWith("{") || PeekLine()?.Trim() == "{")
                {
                    if (!line.EndsWith("{")) ConsumeLine();
                    int depth = 1;
                    while (!IsEndOfFile() && depth > 0)
                    {
                        string inner = PeekLine()?.Trim();
                        if (inner == "}") { ConsumeLine(); depth--; }
                        else if (inner != null) { ConsumeLine(); ParseStatement(unlessCond.Body); }
                        else ConsumeLine();
                    }
                }
                body.Add(unlessCond);
            }
            else if (StartsWithKeyword(line, DbKeywords.Repeat) || line == DbKeywords.Repeat || line.StartsWith("repeat{"))
            {
                // repeat N { }            → for (int _i = 0; _i < N; ++_i)
                // repeat until (cond) { } → while (!(cond))
                Loop loop;
                if (line.StartsWith(DbKeywords.Repeat + " " + DbKeywords.Until) || line.StartsWith(DbKeywords.Repeat + DbKeywords.Until + "("))
                {
                    string untilCondition = ExtractParenContent(line);
                    loop = new Loop(LoopKind.RepeatUntil, untilCondition ?? "");
                }
                else
                {
                    // Forms:
                    //   repeat i: start -> end (step) { }  → forward range
                    //   repeat i: start <- end (step) { }  → reverse range
                    //   repeat i: N { }                    → 0 to N, named counter
                    //   repeat N { }                       → 0 to N, anonymous counter
                    var fwdm = Regex.Match(line,
                        @"repeat\s+(\w+)\s*:\s*(.+?)\s*->\s*(.+?)(?:\s*\(([^)]+)\))?(?:\s*\{|$)");
                    var bwdm = Regex.Match(line,
                        @"repeat\s+(\w+)\s*:\s*(.+?)\s*<-\s*(.+?)(?:\s*\(([^)]+)\))?(?:\s*\{|$)");

                    if (fwdm.Success)
                    {
                        string varName = fwdm.Groups[1].Value;
                        string start   = fwdm.Groups[2].Value.Trim();
                        string end     = fwdm.Groups[3].Value.Trim();
                        string step    = fwdm.Groups[4].Success ? fwdm.Groups[4].Value.Trim() : null;
                        loop = new Loop(LoopKind.Repeat, step != null ? $"{start}->{end},{step}" : $"{start}->{end}");
                        loop.IterationVariable = new Variable(varName, "int");
                    }
                    else if (bwdm.Success)
                    {
                        string varName = bwdm.Groups[1].Value;
                        string start   = bwdm.Groups[2].Value.Trim();
                        string end     = bwdm.Groups[3].Value.Trim();
                        string step    = bwdm.Groups[4].Success ? bwdm.Groups[4].Value.Trim() : null;
                        loop = new Loop(LoopKind.Repeat, step != null ? $"{start}<-{end},{step}" : $"{start}<-{end}");
                        loop.IterationVariable = new Variable(varName, "int");
                    }
                    else
                    {
                        var namedm = Regex.Match(line, @"repeat\s+(\w+)\s*:\s*(.+?)(?:\s*\{|$)");
                        if (namedm.Success)
                        {
                            loop = new Loop(LoopKind.Repeat, namedm.Groups[2].Value.Trim());
                            loop.IterationVariable = new Variable(namedm.Groups[1].Value, "int");
                        }
                        else
                        {
                            var m = Regex.Match(line, @"repeat\s+(\S+?)(?:\s*\{|$)");
                            loop = new Loop(LoopKind.Repeat, m.Success ? m.Groups[1].Value : "");
                        }
                    }
                }

                // Parse body, capturing guard from closing "} if/unless (cond)" line
                string repeatGuard = null; bool guardIsUnless = false;
                if (line.Contains("{") || PeekLine()?.Trim() == "{")
                {
                    if (!line.Contains("{")) ConsumeLine();
                    int depth = 1;
                    while (!IsEndOfFile() && depth > 0)
                    {
                        string inner = PeekLine()?.Trim();
                        if (inner == null) { ConsumeLine(); continue; }
                        if (inner.StartsWith("}"))
                        {
                            string closingLine = ConsumeLine().Trim();
                            depth--;
                            if (depth == 0 && closingLine.Length > 1)
                            {
                                // "} unless (cond)" or "} if (cond)"
                                var gm = Regex.Match(closingLine, @"\}\s*(if|unless)\s*\(([^)]+)\)");
                                if (gm.Success)
                                {
                                    repeatGuard    = gm.Groups[2].Value;
                                    guardIsUnless  = gm.Groups[1].Value == "unless";
                                }
                            }
                        }
                        else { ConsumeLine(); ParseStatement(loop.Body); }
                    }
                }

                // Wrap loop in guard condition if present
                if (repeatGuard != null)
                {
                    string guardExpr = guardIsUnless ? $"!({repeatGuard})" : repeatGuard;
                    var guard = new Condition(ConditionKind.If, guardExpr);
                    guard.Body.Add(loop);
                    body.Add(guard);
                }
                else
                {
                    body.Add(loop);
                }
            }
            else if (StartsWithKeyword(line, DbKeywords.While) || StartsWithKeyword(line, DbKeywords.For) || StartsWithKeyword(line, DbKeywords.Foreach))
            {
                LoopKind kind = StartsWithKeyword(line, DbKeywords.While) ? LoopKind.While :
                                StartsWithKeyword(line, DbKeywords.For) ? LoopKind.For : LoopKind.ForEach;

                // Extract the full parenthesized content (handles nested parens like generics)
                string expression = ExtractParenContent(line);

                var loop = new Loop(kind, expression);
                if (kind == LoopKind.ForEach)
                {
                    // Supports: foreach (Type name in collection)
                    //           foreach (var name in collection)
                    //           foreach (Dictionary<string, int> name in collection)
                    //           foreach (NS.Type name in obj.Collection)
                    var m = Regex.Match(line, @"foreach\s*\(\s*(var|\w+(?:<[^>]+>)?(?:\.\w+)*(?:\[\])*)\s+(\w+)\s+in\s+(.+?)\s*\)");
                    if (m.Success)
                    {
                        loop.IterationVariable = new Variable(m.Groups[2].Value, m.Groups[1].Value);
                        loop.Collection = m.Groups[3].Value.Trim();
                    }
                }
                else if (kind == LoopKind.For)
                {
                    // Decompose for(init; cond; incr) into separate parts
                    // The expression contains the full paren content
                    string forContent = expression;
                    int semi1 = FindTopLevelSemicolon(forContent, 0);
                    int semi2 = semi1 >= 0 ? FindTopLevelSemicolon(forContent, semi1 + 1) : -1;
                    if (semi1 >= 0 && semi2 >= 0)
                    {
                        string initPart = forContent.Substring(0, semi1).Trim();
                        string condPart = forContent.Substring(semi1 + 1, semi2 - semi1 - 1).Trim();
                        string incrPart = forContent.Substring(semi2 + 1).Trim();
                        loop.Expression = condPart; // condition only
                        // Store init and increment as raw text in the loop for the emitter
                        loop.ForInitializer = initPart;
                        loop.ForIncrement = incrPart;
                    }
                }

                if (line.EndsWith("{") || PeekLine()?.Trim() == "{")
                {
                    if (!line.EndsWith("{")) ConsumeLine();
                    int depth = 1;
                    while (!IsEndOfFile() && depth > 0)
                    {
                        string inner = PeekLine()?.Trim();
                        if (inner == "}") { ConsumeLine(); depth--; }
                        else if (inner != null)
                        {
                            ConsumeLine();
                            ParseStatement(loop.Body);
                        }
                        else ConsumeLine();
                    }
                }
                body.Add(loop);
            }
            else if (StartsWithKeyword(line, DbKeywords.Try))
            {
                var tryBlock = new TryCatchBlock();
                if (line.EndsWith("{") || PeekLine()?.Trim() == "{")
                {
                    if (!line.EndsWith("{")) ConsumeLine();
                    int depth = 1;
                    while (!IsEndOfFile() && depth > 0)
                    {
                        string inner = PeekLine()?.Trim();
                        if (inner == "}") { ConsumeLine(); depth--; }
                        else if (inner != null)
                        {
                            ConsumeLine();
                            ParseStatement(tryBlock.TryBody);
                        }
                        else ConsumeLine();
                    }
                }

                while (!IsEndOfFile() && StartsWithKeyword(PeekLine()?.Trim(), DbKeywords.Catch))
                {
                    string catchLine = ConsumeLine().Trim();
                    string catchContent = ExtractParenContent(catchLine);
                    var clause = new CatchClause();
                    if (!string.IsNullOrEmpty(catchContent))
                    {
                        // Handle: ExceptionType variableName  or  ExceptionType  or  ExceptionType<Generic> variableName
                        // Split on the last space to separate type from variable name
                        int lastSpace = catchContent.LastIndexOf(' ');
                        if (lastSpace > 0)
                        {
                            clause.ExceptionType = catchContent.Substring(0, lastSpace).Trim();
                            clause.VariableName = catchContent.Substring(lastSpace + 1).Trim();
                        }
                        else clause.ExceptionType = catchContent.Trim();
                    }

                    if (catchLine.EndsWith("{") || PeekLine()?.Trim() == "{")
                    {
                        if (!catchLine.EndsWith("{")) ConsumeLine();
                        int cDepth = 1;
                        while (!IsEndOfFile() && cDepth > 0)
                        {
                            string cInner = PeekLine()?.Trim();
                            if (cInner == "}") { ConsumeLine(); cDepth--; }
                            else if (cInner != null)
                            {
                                ConsumeLine();
                                ParseStatement(clause.Body);
                            }
                            else ConsumeLine();
                        }
                    }
                    tryBlock.Catches.Add(clause);
                }

                if (!IsEndOfFile() && StartsWithKeyword(PeekLine()?.Trim(), DbKeywords.Finally))
                {
                    ConsumeLine();
                    int fDepth = 1;
                    while (!IsEndOfFile() && fDepth > 0)
                    {
                        string fInner = PeekLine()?.Trim();
                        if (fInner == "}") { ConsumeLine(); fDepth--; }
                        else if (fInner != null)
                        {
                            ConsumeLine();
                            ParseStatement(tryBlock.FinallyBody);
                        }
                        else ConsumeLine();
                    }
                }
                body.Add(tryBlock);
            }
            else if (StartsWithKeyword(line, DbKeywords.Yield))
            {
                bool isBreak = line.Trim().StartsWith(DbKeywords.Yield + " break") || line.Trim() == DbKeywords.Yield + " break;";
                string yieldExpr = isBreak ? null : line.Substring(line.IndexOf(DbKeywords.Yield + " return") + DbKeywords.Yield.Length + 7).Trim().TrimEnd(';').Trim();
                var parsedExpr = isBreak ? null : _exprParser.ParseExpression(yieldExpr);
                body.Add(new YieldStatement(isBreak) { Expression = parsedExpr?.ToString() ?? yieldExpr });
            }
            else if (StartsWithKeyword(line, DbKeywords.Throw))
            {
                string expr = line.TrimStart().Substring(DbKeywords.Throw.Length).Trim().TrimEnd(';');
                var throwStmt = new ThrowStatement();
                if (expr.Contains("("))
                {
                    // Extract exception type and arguments using ExtractParenContent
                    int parenIdx = expr.IndexOf('(');
                    throwStmt.ExceptionType = expr.Substring(0, parenIdx).Trim();
                    string argsContent = ExtractParenContent(expr);
                    throwStmt.Arguments = argsContent;
                }
                else throwStmt.ExceptionType = expr;
                body.Add(throwStmt);
            }

            // ── Block defer: ``first; ... last;`` ────────────────────────────
            else if (line.StartsWith(DbKeywords.DeferBlockTick))
            {
                var deferLines = new System.Text.StringBuilder();
                // First line: strip opening ``
                string firstLine = line.Substring(2).Trim();
                if (firstLine.EndsWith("``"))
                {
                    // Entire block on one line: ``expr;``
                    deferLines.Append(firstLine.Substring(0, firstLine.Length - 2).Trim());
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(firstLine))
                        deferLines.AppendLine(firstLine);
                    // Consume subsequent lines until one ends with ``
                    while (!IsEndOfFile())
                    {
                        string next = ConsumeLine().Trim();
                        if (next.EndsWith("``"))
                        {
                            string lastLine = next.Substring(0, next.Length - 2).TrimEnd();
                            if (!string.IsNullOrWhiteSpace(lastLine))
                                deferLines.AppendLine(lastLine);
                            break;
                        }
                        deferLines.AppendLine(next);
                    }
                }
                body.Add(new DeferStatement(deferLines.ToString().TrimEnd()));
            }

            // ── Single-line defer: `expr; or defer expr; ─────────────────────
            else if (line.StartsWith(DbKeywords.DeferTick) || StartsWithKeyword(line, DbKeywords.Defer))
            {
                string deferBody = line.StartsWith(DbKeywords.DeferTick)
                    ? line.Substring(1).Trim().TrimEnd(';')
                    : line.Substring(DbKeywords.Defer.Length + 1).Trim().TrimEnd(';');
                body.Add(new DeferStatement(deferBody + ";"));
            }

            // ── Log: log stream for MessageType.Level payload; ──────────────
            else if (StartsWithKeyword(line, DbKeywords.Log))
            {
                // Parse: log <stream> for <messageType> <payload>;
                string logExpr = line.TrimStart().Substring(DbKeywords.Log.Length).Trim().TrimEnd(';');
                int forKeywordIdx = logExpr.IndexOf(" for ");
                if (forKeywordIdx >= 0)
                {
                    string stream = logExpr.Substring(0, forKeywordIdx).Trim();
                    string rest = logExpr.Substring(forKeywordIdx + 5).Trim();  // after " for "

                    // rest is: "MessageType.Debug payload" or "MessageType.Debug"
                    // Find the MessageType.X part — it's the first token that contains a dot or is a single word
                    // The payload may contain spaces, parens, etc. so we need to find the boundary carefully.
                    // MessageType can be: Type.Level, Type, or qualified like Namespace.Type.Level
                    int spaceIdx = rest.IndexOf(' ');
                    string messageType;
                    string payload = "";
                    if (spaceIdx >= 0)
                    {
                        messageType = rest.Substring(0, spaceIdx).Trim();
                        payload = rest.Substring(spaceIdx + 1).Trim();
                    }
                    else
                    {
                        messageType = rest.Trim();
                    }

                    var logStmt = new LogStatement(stream, messageType, payload);
                    logStmt.SourceFile = _filePath;
                    logStmt.SourceLine = _currentLine;
                    body.Add(logStmt);
                }
                else
                {
                    // Bare log without "for" — treat as Action for now
                    body.Add(new Action(ActionKind.Other, line.TrimEnd(';')));
                }
            }

            // ── Swap: b <-> a; or swap(b, a); — check BEFORE move (<-) ──────
            else if (IndexOfTopLevel(line, DbKeywords.SwapArrow) >= 0)
            {
                int idx = IndexOfTopLevel(line, DbKeywords.SwapArrow);
                var swapParts = line.TrimEnd(';').Substring(0, idx + 3).TrimEnd(';').Split(new[] { DbKeywords.SwapArrow }, 2, StringSplitOptions.None);
                if (swapParts.Length == 2)
                    body.Add(new SwapStatement(swapParts[0].Trim(), swapParts[1].Trim()));
            }
            else if (line.StartsWith(DbKeywords.Swap + "("))
            {
                string swapContent = ExtractParenContent(line);
                var swapParts = SplitTopLevelCommas(swapContent ?? "");
                if (swapParts.Count == 2)
                    body.Add(new SwapStatement(swapParts[0].Trim(), swapParts[1].Trim()));
            }

            // ── Move: var b <- a; or b <- a; ─────────────────────────────────
            else if (IndexOfTopLevel(line, DbKeywords.MoveArrow) >= 0)
            {
                bool isDecl   = line.TrimStart().StartsWith("var ");
                string moveLine = isDecl ? Regex.Replace(line, @"^\s*var\s+", "") : line;
                int idx = IndexOfTopLevel(moveLine, DbKeywords.MoveArrow);
                if (idx >= 0)
                {
                    string lhs = moveLine.Substring(0, idx).TrimEnd();
                    string rhs = moveLine.Substring(idx + 2).TrimEnd(';').Trim();
                    body.Add(new MoveStatement(lhs, rhs, isDecl));
                }
            }

            else
            {
                // ── Postfix conditional: expr if (cond) : alt;  /  expr unless (cond) : alt;
                if (TryExtractPostfix(line, out string pfPrimary, out bool pfIsUnless, out string pfCond, out string pfAlt))
                {
                    body.Add(new PostfixConditional
                    {
                        Primary   = pfPrimary,
                        Condition = pfCond,
                        IsUnless  = pfIsUnless,
                        Alt       = pfAlt
                    });
                }
                else
                {
                // ── Pipeline: expr, f(), g() → g(f(expr)) ────────────────────
                string rawExpr = line.TrimEnd(';');
                int topEq = FindTopLevelEquals(rawExpr);
                if (topEq >= 0)
                {
                    string lhs    = rawExpr.Substring(0, topEq).TrimEnd();
                    string rhs    = rawExpr.Substring(topEq + 1).Trim();
                    string newRhs = TransformIfPipeline(rhs);
                    if (newRhs != rhs) line = $"{lhs} = {newRhs};";
                }
                else
                {
                    string newExpr = TransformIfPipeline(rawExpr);
                    if (newExpr != rawExpr) line = $"{newExpr};";
                }
                // Try to parse as an expression statement (assignment, method call, etc.)
                var parsedExpr = _exprParser.ParseExpression(line.TrimEnd(';'));
                if (parsedExpr != null)
                {
                    // Determine the action kind from the expression type
                    if (parsedExpr is AssignmentExpression)
                        body.Add(new Action(ActionKind.Assignment, line));
                    else if (parsedExpr is InvocationExpression)
                        body.Add(new Action(ActionKind.MethodCall, line));
                    else
                        body.Add(new Action(ActionKind.Other, line));
                }
                else
                {
                    // Fallback: treat as a raw action line
                    body.Add(new Action(ActionKind.Other, line));
                }
                }
            }
        }

        // ── Postfix conditional helpers ───────────────────────────────────────

        /// <summary>
        /// Finds the index of a target substring at depth 0 while respecting string and char literals.
        /// Returns the index of the first character of <paramref name="target"/>, or -1 if not found.
        /// </summary>
        private int IndexOfTopLevel(string s, string target)
        {
            bool inStr = false, inChar = false;
            for (int i = 0; i < s.Length; i++)
            {
                if (inStr)
                {
                    if (s[i] == '\\' && i + 1 < s.Length)
                        i++; // skip escaped char
                    else if (s[i] == '"')
                        inStr = false;
                    continue;
                }
                if (inChar)
                {
                    if (s[i] == '\\' && i + 1 < s.Length)
                        i++; // skip escaped char
                    else if (s[i] == '\'')
                        inChar = false;
                    continue;
                }

                if (s[i] == '"')
                    inStr = true;
                else if (s[i] == '\'')
                    inChar = true;
                else if (i + target.Length <= s.Length && s.Substring(i, target.Length) == target)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Finds ` if (` or ` unless (` at paren-depth 0 (not at position 0).
        /// Extracts primary action, condition, and optional alt branch.
        /// </summary>
        private bool TryExtractPostfix(string line, out string primary, out bool isUnless,
                                       out string condition, out string alt)
        {
            primary = null; isUnless = false; condition = null; alt = null;

            int depth    = 0;
            int foundIdx = -1;
            bool foundUnless = false;
            bool inStr = false, inChar = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (inStr)
                {
                    if (c == '\\' && i + 1 < line.Length)
                        i++;
                    else if (c == '"')
                        inStr = false;
                    continue;
                }
                if (inChar)
                {
                    if (c == '\\' && i + 1 < line.Length)
                        i++;
                    else if (c == '\'')
                        inChar = false;
                    continue;
                }
                if (c == '"') { inStr = true; continue; }
                if (c == '\'') { inChar = true; continue; }

                if (c == '(' || c == '[' || c == '{') depth++;
                else if (c == ')' || c == ']' || c == '}') depth--;

                if (depth == 0 && i > 0)
                {
                    if (i + 5 <= line.Length && line.Substring(i, Math.Min(5, line.Length - i)) == " if (")
                    { foundIdx = i; foundUnless = false; break; }
                    if (i + 9 <= line.Length && line.Substring(i, Math.Min(9, line.Length - i)) == " unless (")
                    { foundIdx = i; foundUnless = true; break; }
                }
            }

            if (foundIdx < 0) return false;

            primary = line.Substring(0, foundIdx).Trim();
            if (string.IsNullOrWhiteSpace(primary)) return false;

            // Find matching ')' for the condition
            int parenOpen = foundIdx + (foundUnless ? 8 : 4); // index of '('
            int parenClose = FindMatchingParen(line, parenOpen);
            if (parenClose < 0) return false;

            condition = line.Substring(parenOpen + 1, parenClose - parenOpen - 1).Trim();
            isUnless  = foundUnless;

            // Optional ": alt" after the closing ')'
            string after = line.Substring(parenClose + 1).Trim().TrimEnd(';');
            if (after.StartsWith(":"))
                alt = after.Substring(1).Trim();

            return true;
        }

        /// <summary>Returns the index of the ')' that closes the '(' at <paramref name="openIdx"/>.</summary>
        private int FindMatchingParen(string s, int openIdx)
        {
            int depth = 0;
            bool inStr = false, inChar = false;
            for (int i = openIdx; i < s.Length; i++)
            {
                char c = s[i];
                if (inStr) { if (c == '\\') i++; else if (c == '"') inStr = false; continue; }
                if (inChar) { if (c == '\\') i++; else if (c == '\'') inChar = false; continue; }
                if (c == '"') { inStr = true; continue; }
                if (c == '\'') { inChar = true; continue; }

                if (c == '(') depth++;
                else if (c == ')') { if (--depth == 0) return i; }
            }
            return -1;
        }

        // ── Pipeline helpers ─────────────────────────────────────────────────

        /// <summary>
        /// Splits <paramref name="s"/> on commas that are at paren/bracket depth 0.
        /// </summary>
        private List<string> SplitTopLevelCommas(string s)
        {
            var result = new List<string>();
            int depth = 0, start = 0;
            bool inStr = false, inChar = false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (inStr) { if (c == '\\') i++; else if (c == '"') inStr = false; continue; }
                if (inChar) { if (c == '\\') i++; else if (c == '\'') inChar = false; continue; }
                if (c == '"') { inStr = true; continue; }
                if (c == '\'') { inChar = true; continue; }

                if (c == '(' || c == '[' || c == '{') depth++;
                else if (c == ')' || c == ']' || c == '}') depth--;
                else if (c == ',' && depth == 0)
                { result.Add(s.Substring(start, i - start)); start = i + 1; }
            }
            result.Add(s.Substring(start));
            return result;
        }

        /// <summary>
        /// Nests pipeline stages right-to-left: <c>a, f(), g(x)</c> → <c>g(f(a), x)</c>.
        /// Each stage's previous result is injected as its first argument.
        /// </summary>
        private string BuildPipeline(List<string> stages)
        {
            string acc = stages[0].Trim();
            for (int i = 1; i < stages.Count; i++)
            {
                string stage = stages[i].Trim();
                int p = stage.IndexOf('(');
                if (p < 0)
                {
                    acc = $"{stage}({acc})";
                }
                else
                {
                    string name  = stage.Substring(0, p);
                    string inner = stage.Substring(p + 1, stage.Length - p - 2).Trim();
                    acc = string.IsNullOrEmpty(inner) ? $"{name}({acc})" : $"{name}({acc}, {inner})";
                }
            }
            return acc;
        }

        /// <summary>Transforms a pipeline expression if it contains top-level commas; otherwise returns it unchanged.</summary>
        private string TransformIfPipeline(string expr)
        {
            var stages = SplitTopLevelCommas(expr);
            if (stages.Count < 2) return expr;
            // Only treat as pipeline if at least one non-first segment looks like a call
            if (!stages.Skip(1).Any(s => s.Trim().Contains("(")))
                return expr;
            return BuildPipeline(stages);
        }

        /// <summary>
        /// Extracts the content inside the first set of balanced parentheses from a line.
        /// Handles nested parens (e.g. for generic types like List&lt;int&gt;).
        /// </summary>
        private string ExtractParenContent(string line)
        {
            int start = line.IndexOf('(');
            if (start < 0) return "";
            int depth = 0;
            for (int i = start; i < line.Length; i++)
            {
                if (line[i] == '(') depth++;
                else if (line[i] == ')')
                {
                    depth--;
                    if (depth == 0)
                        return line.Substring(start + 1, i - start - 1);
                }
            }
            // Unbalanced — return everything after the opening paren
            return line.Substring(start + 1);
        }

        /// <summary>
        /// Finds the index of the nth semicolon at depth 0 (outside parens/brackets/braces/strings).
        /// Returns -1 if not found.
        /// </summary>
        private int FindTopLevelSemicolon(string s, int startIndex)
        {
            int depth = 0;
            bool inStr = false, inChar = false;
            for (int i = startIndex; i < s.Length; i++)
            {
                char c = s[i];
                if (inStr) { if (c == '\\' && i + 1 < s.Length) i++; else if (c == '"') inStr = false; continue; }
                if (inChar) { if (c == '\\' && i + 1 < s.Length) i++; else if (c == '\'') inChar = false; continue; }
                if (c == '"') { inStr = true; continue; }
                if (c == '\'') { inChar = true; continue; }
                if (c == '(' || c == '[' || c == '{' || c == '<') depth++;
                else if (c == ')' || c == ']' || c == '}' || c == '>') depth--;
                else if (c == ';' && depth == 0) return i;
            }
            return -1;
        }

        /// <summary>
        /// Finds the index of the first <c>=</c> at depth 0 that is not part of
        /// <c>==</c>, <c>!=</c>, <c>&lt;=</c>, <c>&gt;=</c>, or <c>=&gt;</c>.
        /// Returns -1 if none found.
        /// </summary>
        private int FindTopLevelEquals(string s)
        {
            int depth = 0;
            bool inStr = false, inChar = false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (inStr) { if (c == '\\') i++; else if (c == '"') inStr = false; continue; }
                if (inChar) { if (c == '\\') i++; else if (c == '\'') inChar = false; continue; }
                if (c == '"') { inStr = true; continue; }
                if (c == '\'') { inChar = true; continue; }

                if (c == '(' || c == '[' || c == '{') depth++;
                else if (c == ')' || c == ']' || c == '}') depth--;
                else if (c == '=' && depth == 0)
                {
                    char prev = i > 0 ? s[i - 1] : '\0';
                    char next = i + 1 < s.Length ? s[i + 1] : '\0';
                    if (prev == '!' || prev == '<' || prev == '>' || prev == '=') continue;
                    if (next == '=' || next == '>') continue;
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// Counts the net brace depth change in a line, ignoring braces inside
        /// string and char literals. Returns positive for net opening braces,
        /// negative for net closing braces, zero if balanced or none.
        /// </summary>
        private int CountBraces(string line)
        {
            int depth = 0;
            bool inStr = false, inChar = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (inStr) { if (c == '\\' && i + 1 < line.Length) i++; else if (c == '"') inStr = false; continue; }
                if (inChar) { if (c == '\\' && i + 1 < line.Length) i++; else if (c == '\'') inChar = false; continue; }
                if (c == '"') { inStr = true; continue; }
                if (c == '\'') { inChar = true; continue; }
                if (c == '{') depth++;
                else if (c == '}') depth--;
            }
            return depth;
        }

        private void ParseVariable(List<Variable> variables, string line)
        {
            // Strip trailing semicolon and access modifiers
            string trimmed = line.Trim().TrimEnd(';');
            trimmed = System.Text.RegularExpressions.Regex.Replace(trimmed, @"^(public|private|protected|internal)\s+", "");

            // Strip ref/out/in/params modifiers
            bool isRef = false, isOut = false, isIn = false;
            if (trimmed.StartsWith("ref ")) { isRef = true; trimmed = trimmed.Substring(4).TrimStart(); }
            else if (trimmed.StartsWith("out ")) { isOut = true; trimmed = trimmed.Substring(4).TrimStart(); }
            else if (trimmed.StartsWith("in ")) { isIn = true; trimmed = trimmed.Substring(3).TrimStart(); }

            // Strip readonly/const modifiers
            bool isReadonly = false, isConst = false;
            if (trimmed.StartsWith("readonly ")) { isReadonly = true; trimmed = trimmed.Substring(9).TrimStart(); }
            if (trimmed.StartsWith("const ")) { isConst = true; trimmed = trimmed.Substring(6).TrimStart(); }

            // Pattern: Type name = expr;  (handles qualified types like NS.Type and generics like Dictionary<string, int>)
            var assignMatch = System.Text.RegularExpressions.Regex.Match(trimmed, @"^((?:\w+\.)*\w+(?:<[^>]+>)?(?:\[\])?)\s+(\w+)\s*=\s*(.+)$");
            if (assignMatch.Success)
            {
                string type = assignMatch.Groups[1].Value;
                string name = assignMatch.Groups[2].Value;
                string initExpr = assignMatch.Groups[3].Value.Trim();

                var v = new Variable(name, type) { Initializer = initExpr };
                if (isRef) v.Modifier = "ref";
                if (isOut) v.Modifier = "out";
                if (isIn) v.Modifier = "in";
                if (isReadonly) v.Mutability = MutabilityModifier.ReadOnly;
                if (isConst) v.Mutability = MutabilityModifier.Const;

                // Try to parse the initializer as an expression
                var parsedInit = _exprParser.ParseExpression(initExpr);
                if (parsedInit is ObjectCreationExpression objCreate)
                {
                    v.IsNewObject = true;
                    v.ConstructorArgs = string.Join(", ", objCreate.Arguments.Select(a => a.ToString()));
                    foreach (var na in objCreate.NamedArgs)
                        v.NamedArgs.Add(na);
                }

                variables.Add(v);
                return;
            }

            // Pattern: var name = expr;
            var varMatch = System.Text.RegularExpressions.Regex.Match(trimmed, @"^var\s+(\w+)\s*=\s*(.+)$");
            if (varMatch.Success)
            {
                string name = varMatch.Groups[1].Value;
                string initExpr = varMatch.Groups[2].Value.Trim();

                var v = new Variable(name, "var") { Initializer = initExpr, IsInferred = true };
                if (isRef) v.Modifier = "ref";
                if (isOut) v.Modifier = "out";

                var parsedInit = _exprParser.ParseExpression(initExpr);
                if (parsedInit is ObjectCreationExpression objCreate)
                {
                    v.IsNewObject = true;
                    v.ConstructorArgs = string.Join(", ", objCreate.Arguments.Select(a => a.ToString()));
                    foreach (var na in objCreate.NamedArgs)
                        v.NamedArgs.Add(na);
                }

                variables.Add(v);
                return;
            }

            // Pattern: Type name; (declaration without initializer — handles qualified/generic types)
            var simpleMatch = System.Text.RegularExpressions.Regex.Match(trimmed, @"^((?:\w+\.)*\w+(?:<[^>]+>)?(?:\[\])?)\s+(\w+)\s*$");
            if (simpleMatch.Success)
            {
                var v = new Variable(simpleMatch.Groups[2].Value, simpleMatch.Groups[1].Value);
                if (isRef) v.Modifier = "ref";
                if (isOut) v.Modifier = "out";
                if (isIn) v.Modifier = "in";
                if (isReadonly) v.Mutability = MutabilityModifier.ReadOnly;
                if (isConst) v.Mutability = MutabilityModifier.Const;
                variables.Add(v);
                return;
            }

            // Fallback: try the old regex patterns for backward compatibility
            var oldMatch = System.Text.RegularExpressions.Regex.Match(line, @"^\s*(?:public|private|protected|internal)?\s*(\w+)\s+(\w+)\s*=\s*new\s+(\w+)\s*\(([^)]*)\)\s*;\s*$");
            if (oldMatch.Success)
            {
                string type = oldMatch.Groups[1].Value;
                string name = oldMatch.Groups[2].Value;
                string ctorType = oldMatch.Groups[3].Value;
                string argsRaw = oldMatch.Groups[4].Value;

                var v = new Variable(name, type) { IsNewObject = true };
                string[] parts = argsRaw.Split(',');
                foreach (var part in parts)
                {
                    string p = part.Trim();
                    if (p.Contains(":"))
                    {
                        var kv = p.Split(':');
                        v.NamedArgs.Add(new NamedArgument(kv[0].Trim(), kv[1].Trim()));
                    }
                    else if (!string.IsNullOrWhiteSpace(p))
                    {
                        if (v.ConstructorArgs == null) v.ConstructorArgs = "";
                        v.ConstructorArgs += (v.ConstructorArgs.Length > 0 ? ", " : "") + p;
                    }
                }
                variables.Add(v);
            }
        }
    }
}
