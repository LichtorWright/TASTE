using System.Collections.Generic;
using System.Text;

namespace taste.Emit
{
    /// <summary>
    /// Abstract base for all language emitters.
    /// Subclass this to target a new language — override the Write* methods
    /// and call <see cref="EmitFile"/> to produce the output string.
    /// 
    /// Each Emitter subclass should consult its <see cref="LanguageProfile"/>
    /// from <see cref="LanguageMatrix"/> for language-specific syntax patterns
    /// (keywords, comment styles, statement terminators, etc.) rather than
    /// hardcoding them.
    /// </summary>
    public abstract class Emitter
    {
        /// <summary>
        /// The language profile this emitter uses for syntax patterns.
        /// Subclasses should consult this for keywords, delimiters, and templates
        /// rather than hardcoding language-specific strings.
        /// </summary>
        public LanguageProfile Profile { get; }

        protected Emitter(LanguageProfile profile)
        {
            Profile = profile;
        }

        // ── Output helpers ─────────────────────────────────────────────────────

        private readonly StringBuilder _sb = new StringBuilder();
        private int _indentLevel;
        private const string IndentUnit = "    "; // 4 spaces

        protected void Indent()  => _indentLevel++;
        protected void Dedent()  => _indentLevel = System.Math.Max(0, _indentLevel - 1);

        protected void WriteLine(string line = "")
        {
            if (line.Length == 0)
                _sb.AppendLine();
            else
                _sb.AppendLine(CurrentIndent() + line);
        }

        protected void Write(string text) => _sb.Append(text);

        private string CurrentIndent()
        {
            var sb = new StringBuilder(_indentLevel * IndentUnit.Length);
            for (int i = 0; i < _indentLevel; i++) sb.Append(IndentUnit);
            return sb.ToString();
        }

        /// <summary>
        /// Clears accumulated output — call before each top-level emit if reusing the writer.
        /// </summary>
        protected void Reset() => _sb.Clear();

        /// <summary>Returns everything written so far.</summary>
        protected string GetOutput() => _sb.ToString();

        // ── Public entry point ─────────────────────────────────────────────────

        /// <summary>Transpile a fully-parsed <see cref="CodeFile"/> to a source string.</summary>
        public string EmitFile(CodeFile file)
        {
            Reset();
            WriteFileHeader(file);
            foreach (var d in file.FileScopeDirectives) WriteFileScopeDirective(d);
            foreach (var u in file.Usings)        WriteUsing(u);
            foreach (var ns in file.Namespaces)   WriteNamespace(ns);
            foreach (var d in file.Delegates)     WriteDelegate(d);
            foreach (var e in file.Enums)         WriteEnum(e);
            foreach (var i in file.Interfaces)    WriteInterface(i);
            foreach (var s in file.SumTypes)      WriteSumType(s);
            foreach (var m in file.Mixins)        WriteMixin(m);
            foreach (var a in file.TypeAliases)   WriteTypeAlias(a);
            foreach (var cls in file.Classes)     WriteClass(cls);
            WriteFileFooter(file);
            return GetOutput();
        }

        // ── File-level hooks ───────────────────────────────────────────────────

        /// <summary>Called once before any other output (e.g. emit #include guards or pragma).</summary>
        protected virtual void WriteFileHeader(CodeFile file) { }

        /// <summary>Called once after all content (e.g. close header guards).</summary>
        protected virtual void WriteFileFooter(CodeFile file) { }

        // ── Top-level declarations ─────────────────────────────────────────────

        protected abstract void WriteUsing(Using u);
        protected abstract void WriteNamespace(Namespace ns);
        protected abstract void WriteDelegate(DelegateDecl d);
        protected abstract void WriteEnum(EnumDecl e);
        protected abstract void WriteClass(Class cls);
        protected abstract void WriteInterface(InterfaceDeclaration iface);
        protected abstract void WriteSumType(SumTypeDeclaration sum);
        protected abstract void WriteMixin(MixinDeclaration mixin);
        protected abstract void WriteTypeAlias(TypeAliasDeclaration alias);
        protected abstract void WriteFileScopeDirective(FileScopeDirective directive);

        // ── Class members ──────────────────────────────────────────────────────

        protected abstract void WriteConstant(Constant constant);
        protected abstract void WriteField(Field field);
        protected abstract void WriteProperty(Property property);
        protected abstract void WriteIndexer(Indexer indexer);
        protected abstract void WriteOperatorOverload(OperatorOverload op);
        protected abstract void WriteEvent(Event ev);
        protected abstract void WriteMethod(Method method);

        // ── Method internals ───────────────────────────────────────────────────

        protected abstract void WriteParameter(Parameter param);
        protected abstract void WriteVariable(Variable variable);

        /// <summary>
        /// Dispatches to the correct Write* override based on the runtime type of
        /// <paramref name="stmt"/>. Override if you need to intercept before dispatch.
        /// </summary>
        protected virtual void WriteStatement(Statement stmt)
        {
            switch (stmt)
            {
                case Action a:           WriteAction(a);           break;
                case Condition c:        WriteCondition(c);        break;
                case Loop l:             WriteLoop(l);            break;
                case TryCatchBlock t:    WriteTryCatch(t);         break;
                case UsingBlock u:       WriteUsingBlock(u);       break;
                case LockBlock l:        WriteLockBlock(l);        break;
                case ThrowStatement th:  WriteThrow(th);           break;
                case YieldStatement y:   WriteYield(y);            break;
                case CheckedBlock ch:    WriteCheckedBlock(ch);    break;
                case InlineNativeBlock b:   WriteInlineCpp(b);        break;
                case BlockStatement bs:  WriteBlockStatement(bs);  break;
                case DoWhileStatement dw: WriteDoWhile(dw);        break;
                case ReturnStatement r:   WriteReturn(r);          break;
                case BreakStatement br:    WriteBreak(br);           break;
                case ContinueStatement co: WriteContinue(co);        break;
                case MatchStatement ms:    WriteMatch(ms);           break;
                case DeferStatement ds:    WriteDefer(ds);           break;
                case MoveStatement mv:     WriteMove(mv);            break;
                case SwapStatement sw:     WriteSwap(sw);            break;
                case PostfixConditional pc: WritePostfixConditional(pc); break;
                case LogStatement ls:       WriteLog(ls);              break;
            }
        }

        protected abstract void WriteAction(Action action);
        protected abstract void WriteCondition(Condition condition);
        protected abstract void WriteLoop(Loop loop);
        protected abstract void WriteTryCatch(TryCatchBlock block);
        protected abstract void WriteUsingBlock(UsingBlock block);
        protected abstract void WriteLockBlock(LockBlock block);
        protected abstract void WriteThrow(ThrowStatement stmt);
        protected abstract void WriteYield(YieldStatement stmt);
        protected abstract void WriteCheckedBlock(CheckedBlock block);

        // ── New statement types ──────────────────────────────────────────────────

        protected abstract void WriteBlockStatement(BlockStatement block);
        protected abstract void WriteDoWhile(DoWhileStatement stmt);
        protected abstract void WriteReturn(ReturnStatement stmt);
        protected abstract void WriteBreak(BreakStatement stmt);
        protected abstract void WriteContinue(ContinueStatement stmt);
        protected abstract void WriteMatch(MatchStatement stmt);
        protected abstract void WriteDefer(DeferStatement stmt);
        protected abstract void WriteMove(MoveStatement stmt);
        protected abstract void WriteSwap(SwapStatement stmt);
        protected abstract void WritePostfixConditional(PostfixConditional stmt);
        protected abstract void WriteLog(LogStatement stmt);

        // ── Expression emission ─────────────────────────────────────────────────

        /// <summary>
        /// Dispatches to the correct Write* override based on the runtime type of
        /// <paramref name="expr"/>. Override if you need to intercept before dispatch.
        /// </summary>
        protected virtual void WriteExpression(Expression expr)
        {
            switch (expr)
            {
                case LiteralExpression lit:       WriteLiteralExpression(lit);       break;
                case IdentifierExpression id:     WriteIdentifierExpression(id);     break;
                case BinaryExpression bin:        WriteBinaryExpression(bin);        break;
                case UnaryExpression un:          WriteUnaryExpression(un);          break;
                case AssignmentExpression assign:  WriteAssignmentExpression(assign); break;
                case MemberAccessExpression mem:   WriteMemberAccessExpression(mem);  break;
                case InvocationExpression inv:     WriteInvocationExpression(inv);    break;
                case ObjectCreationExpression obj:  WriteObjectCreationExpression(obj); break;
                case CastExpression cast:          WriteCastExpression(cast);        break;
                case IsTypeExpression isType:      WriteIsTypeExpression(isType);    break;
                case ParenthesizedExpression paren: WriteParenthesizedExpression(paren); break;
                case TernaryExpression tern:       WriteTernaryExpression(tern);      break;
                case RangeExpression range:        WriteRangeExpression(range);       break;
                case LambdaExpression lambda:      WriteLambdaExpression(lambda);    break;
                case AwaitExpression await:        WriteAwaitExpression(await);      break;
                case ArrayCreationExpression arr:  WriteArrayCreationExpression(arr); break;
                case TupleExpression tuple:        WriteTupleExpression(tuple);      break;
            }
        }

        /// <summary>
        /// Emit an expression and return it as a string (useful for embedding in larger expressions).
        /// Default implementation writes to a temporary buffer and returns the content.
        /// </summary>
        protected virtual string EmitExpressionAsString(Expression expr)
        {
            // Save current output state
            var savedIndent = _indentLevel;
            var savedOutput = GetOutput();
            Reset();

            WriteExpression(expr);
            string result = GetOutput().Trim();

            // Restore output state
            Reset();
            _sb.Append(savedOutput);
            _indentLevel = savedIndent;

            return result;
        }

        protected abstract void WriteLiteralExpression(LiteralExpression expr);
        protected abstract void WriteIdentifierExpression(IdentifierExpression expr);
        protected abstract void WriteBinaryExpression(BinaryExpression expr);
        protected abstract void WriteUnaryExpression(UnaryExpression expr);
        protected abstract void WriteAssignmentExpression(AssignmentExpression expr);
        protected abstract void WriteMemberAccessExpression(MemberAccessExpression expr);
        protected abstract void WriteInvocationExpression(InvocationExpression expr);
        protected abstract void WriteObjectCreationExpression(ObjectCreationExpression expr);
        protected abstract void WriteCastExpression(CastExpression expr);
        protected abstract void WriteIsTypeExpression(IsTypeExpression expr);
        protected abstract void WriteParenthesizedExpression(ParenthesizedExpression expr);
        protected abstract void WriteTernaryExpression(TernaryExpression expr);
        protected abstract void WriteRangeExpression(RangeExpression expr);
        protected abstract void WriteLambdaExpression(LambdaExpression expr);
        protected abstract void WriteAwaitExpression(AwaitExpression expr);
        protected abstract void WriteArrayCreationExpression(ArrayCreationExpression expr);
        protected abstract void WriteTupleExpression(TupleExpression expr);

        /// <summary>
        /// Emit a verbatim <c>cpp { ... }</c> passthrough block.
        /// The default implementation writes each line as-is; override to adjust indentation.
        /// </summary>
        protected virtual void WriteInlineCpp(InlineNativeBlock block)
        {
            foreach (var line in block.Lines)
                WriteLine(line);
        }

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
        /// Look up a keyword for a given <see cref="CodePart"/> in this emitter's language.
        /// Returns empty string if the part is not defined for this language.
        /// </summary>
        protected string KeywordFor(CodePart part) => Profile.KeywordFor(part);

        /// <summary>
        /// Look up a syntax template for a given <see cref="CodePart"/> in this emitter's language.
        /// Returns null if the part has no template defined.
        /// </summary>
        protected string TemplateFor(CodePart part) => Profile.TemplateFor(part);
    }
}
