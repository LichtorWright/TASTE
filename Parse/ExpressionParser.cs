using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace taste.Parse
{
    // ── Token types for expression lexing ─────────────────────────────────────

    internal enum TokenType
    {
        // Literals
        IntegerLiteral,
        FloatLiteral,
        StringLiteral,
        CharLiteral,
        TrueKeyword,
        FalseKeyword,
        NullKeyword,

        // Identifiers & keywords
        Identifier,
        NewKeyword,
        AsKeyword,
        IsKeyword,
        AwaitKeyword,
        TypeofKeyword,
        DefaultKeyword,
        SizeofKeyword,
        WithKeyword,

        // Operators
        Plus,           // +
        Minus,          // -
        Asterisk,       // *
        Slash,          // /
        Percent,        // %
        Ampersand,      // &
        Pipe,           // |
        Caret,          // ^
        Tilde,          // ~
        Exclamation,    // !
        QuestionMark,   // ?
        Dot,            // .
        Comma,          // ,
        Colon,          // :
        DoubleColon,    // ::
        Arrow,          // ->
        DotAsterisk,    // .*
        ArrowAsterisk,  // ->*
        QuestionDot,    // ?.
        QuestionBracket, // ?[

        // Comparison
        Equals,         // ==
        NotEquals,       // !=
        LessThan,       // <
        GreaterThan,    // >
        LessEqual,      // <=
        GreaterEqual,   // >=

        // Logical
        DoubleAmpersand, // &&
        DoublePipe,      // ||

        // Assignment
        Assign,         // =
        PlusAssign,     // +=
        MinusAssign,    // -=
        StarAssign,     // *=
        SlashAssign,    // /=
        PercentAssign,  // %=
        AmpersandAssign,// &=
        PipeAssign,     // |=
        CaretAssign,    // ^=
        NullCoalesceAssign, // ??=

        // Increment/Decrement
        Increment,      // ++
        Decrement,      // --

        // Null coalescing
        DoubleQuestion, // ??

        // Lambda arrow
        ArrowLambda,    // =>

        // Range
        DotDot,         // ..
        DotDotEquals,   // ..=

        // Brackets
        OpenParen,      // (
        CloseParen,     // )
        OpenBracket,    // [
        CloseBracket,   // ]
        OpenBrace,      // {

        // Parameter modifier keywords (in argument position)
        OutKeyword,
        RefKeyword,
        InKeyword,

        // Special
        Eof
    }

    internal struct Token
    {
        public TokenType Type;
        public string Value;
        public int Position;

        public Token(TokenType type, string value, int position)
        {
            Type = type;
            Value = value;
            Position = position;
        }

        public override string ToString() => $"{Type}: '{Value}'";
    }

    // ── Expression Lexer ─────────────────────────────────────────────────────

    internal class ExpressionLexer
    {
        private readonly string _source;
        private int _pos;

        public ExpressionLexer(string source)
        {
            _source = source;
            _pos = 0;
        }

        public List<Token> Tokenize()
        {
            var tokens = new List<Token>();
            while (_pos < _source.Length)
            {
                char c = _source[_pos];

                // Skip whitespace
                if (char.IsWhiteSpace(c)) { _pos++; continue; }

                // Skip comments
                if (c == '/' && _pos + 1 < _source.Length)
                {
                    if (_source[_pos + 1] == '/')
                    {
                        // Line comment — skip to end of line
                        while (_pos < _source.Length && _source[_pos] != '\n') _pos++;
                        continue;
                    }
                    if (_source[_pos + 1] == '*')
                    {
                        // Block comment
                        _pos += 2;
                        while (_pos + 1 < _source.Length && !(_source[_pos] == '*' && _source[_pos + 1] == '/'))
                            _pos++;
                        _pos += 2;
                        continue;
                    }
                }

                // String literal
                if (c == '"') { tokens.Add(ReadStringLiteral()); continue; }
                if (c == '\'') { tokens.Add(ReadCharLiteral()); continue; }

                // Verbatim string @"..."
                if (c == '@' && _pos + 1 < _source.Length && _source[_pos + 1] == '"')
                {
                    tokens.Add(ReadVerbatimStringLiteral());
                    continue;
                }

                // Numbers
                if (char.IsDigit(c) || (c == '.' && _pos + 1 < _source.Length && char.IsDigit(_source[_pos + 1])))
                {
                    tokens.Add(ReadNumber());
                    continue;
                }

                // Identifiers & keywords
                if (char.IsLetter(c) || c == '_' || c == '@')
                {
                    tokens.Add(ReadIdentifier());
                    continue;
                }

                // Multi-character operators (order matters — longest match first)
                if (TryReadMultiCharOp(tokens)) continue;

                // Single-character tokens
                tokens.Add(ReadSingleChar(c));
            }

            tokens.Add(new Token(TokenType.Eof, "", _pos));
            return tokens;
        }

        private Token ReadStringLiteral()
        {
            int start = _pos;
            _pos++; // skip opening "
            var sb = new StringBuilder();
            while (_pos < _source.Length && _source[_pos] != '"')
            {
                if (_source[_pos] == '\\' && _pos + 1 < _source.Length)
                {
                    _pos++;
                    sb.Append(EscapeChar(_source[_pos]));
                }
                else
                {
                    sb.Append(_source[_pos]);
                }
                _pos++;
            }
            _pos++; // skip closing "
            return new Token(TokenType.StringLiteral, sb.ToString(), start);
        }

        private Token ReadCharLiteral()
        {
            int start = _pos;
            _pos++; // skip opening '
            char value;
            if (_source[_pos] == '\\')
            {
                _pos++;
                value = EscapeChar(_source[_pos]);
            }
            else
            {
                value = _source[_pos];
            }
            _pos++; // the char
            _pos++; // skip closing '
            return new Token(TokenType.CharLiteral, value.ToString(), start);
        }

        private Token ReadVerbatimStringLiteral()
        {
            int start = _pos;
            _pos += 2; // skip @"
            var sb = new StringBuilder();
            while (_pos < _source.Length)
            {
                if (_source[_pos] == '"' && _pos + 1 < _source.Length && _source[_pos + 1] == '"')
                {
                    sb.Append('"');
                    _pos += 2;
                }
                else if (_source[_pos] == '"')
                {
                    _pos++;
                    break;
                }
                else
                {
                    sb.Append(_source[_pos]);
                    _pos++;
                }
            }
            return new Token(TokenType.StringLiteral, sb.ToString(), start);
        }

        private char EscapeChar(char c) => c switch
        {
            'n' => '\n', 'r' => '\r', 't' => '\t', '0' => '\0',
            '\\' => '\\', '"' => '"', '\'' => '\'',
            _ => c
        };

        private Token ReadNumber()
        {
            int start = _pos;
            bool isFloat = false;

            // Hex: 0x...
            if (_source[_pos] == '0' && _pos + 1 < _source.Length &&
                (_source[_pos + 1] == 'x' || _source[_pos + 1] == 'X'))
            {
                _pos += 2;
                while (_pos < _source.Length && IsHexDigit(_source[_pos])) _pos++;
                // Optional L/UL suffix
                if (_pos < _source.Length && (char.ToLower(_source[_pos]) == 'l' || char.ToLower(_source[_pos]) == 'u'))
                    _pos++;
                return new Token(TokenType.IntegerLiteral, _source[start.._pos], start);
            }

            // Binary: 0b...
            if (_source[_pos] == '0' && _pos + 1 < _source.Length &&
                (_source[_pos + 1] == 'b' || _source[_pos + 1] == 'B'))
            {
                _pos += 2;
                while (_pos < _source.Length && (_source[_pos] == '0' || _source[_pos] == '1')) _pos++;
                return new Token(TokenType.IntegerLiteral, _source[start.._pos], start);
            }

            while (_pos < _source.Length && char.IsDigit(_source[_pos])) _pos++;

            if (_pos < _source.Length && _source[_pos] == '.')
            {
                // Check it's not a method call like obj.method
                if (_pos + 1 < _source.Length && char.IsDigit(_source[_pos + 1]))
                {
                    isFloat = true;
                    _pos++; // consume .
                    while (_pos < _source.Length && char.IsDigit(_source[_pos])) _pos++;
                }
            }

            if (_pos < _source.Length && (_source[_pos] == 'e' || _source[_pos] == 'E'))
            {
                isFloat = true;
                _pos++;
                if (_pos < _source.Length && (_source[_pos] == '+' || _source[_pos] == '-')) _pos++;
                while (_pos < _source.Length && char.IsDigit(_source[_pos])) _pos++;
            }

            // Suffixes: f, F, d, D, m, M, l, L, ul, UL
            if (_pos < _source.Length)
            {
                char suffix = char.ToLower(_source[_pos]);
                if (suffix == 'f' || suffix == 'd' || suffix == 'm') { _pos++; isFloat = true; }
                else if (suffix == 'l') { _pos++; }
                else if (suffix == 'u') { _pos++; if (_pos < _source.Length && char.ToLower(_source[_pos]) == 'l') _pos++; }
            }

            return new Token(isFloat ? TokenType.FloatLiteral : TokenType.IntegerLiteral, _source[start.._pos], start);
        }

        private Token ReadIdentifier()
        {
            int start = _pos;
            if (_source[_pos] == '@') _pos++; // skip @ for verbatim identifiers
            while (_pos < _source.Length && (char.IsLetterOrDigit(_source[_pos]) || _source[_pos] == '_')) _pos++;
            string text = _source[start.._pos];

            var type = text switch
            {
                "true" => TokenType.TrueKeyword,
                "false" => TokenType.FalseKeyword,
                "null" => TokenType.NullKeyword,
                "new" => TokenType.NewKeyword,
                "as" => TokenType.AsKeyword,
                "is" => TokenType.IsKeyword,
                "await" => TokenType.AwaitKeyword,
                "typeof" => TokenType.TypeofKeyword,
                "default" => TokenType.DefaultKeyword,
                "sizeof" => TokenType.SizeofKeyword,
                "with" => TokenType.WithKeyword,
                "out" => TokenType.OutKeyword,
                "ref" => TokenType.RefKeyword,
                "in" => TokenType.InKeyword,
                _ => TokenType.Identifier
            };

            return new Token(type, text, start);
        }

        private bool TryReadMultiCharOp(List<Token> tokens)
        {
            // 3-char operators
            if (_pos + 2 < _source.Length)
            {
                string tri = _source.Substring(_pos, 3);
                var tt = tri switch
                {
                    "->*" => (TokenType.ArrowAsterisk, 3),
                    "?[]" => (TokenType.QuestionBracket, 3),
                    "..=" => (TokenType.DotDotEquals, 3),
                    _ => default
                };
                if (tt.Item1 != default)
                {
                    tokens.Add(new Token(tt.Item1, tri, _pos));
                    _pos += tt.Item2;
                    return true;
                }
            }

            // 2-char operators
            if (_pos + 1 < _source.Length)
            {
                string duo = _source.Substring(_pos, 2);
                var tt = duo switch
                {
                    "::" => (TokenType.DoubleColon, 2),
                    "->" => (TokenType.Arrow, 2),
                    ".*" => (TokenType.DotAsterisk, 2),
                    "?." => (TokenType.QuestionDot, 2),
                    "==" => (TokenType.Equals, 2),
                    "!=" => (TokenType.NotEquals, 2),
                    "<=" => (TokenType.LessEqual, 2),
                    ">=" => (TokenType.GreaterEqual, 2),
                    "&&" => (TokenType.DoubleAmpersand, 2),
                    "||" => (TokenType.DoublePipe, 2),
                    "++" => (TokenType.Increment, 2),
                    "--" => (TokenType.Decrement, 2),
                    "??" => (TokenType.DoubleQuestion, 2),
                    "=>" => (TokenType.ArrowLambda, 2),
                    ".." => (TokenType.DotDot, 2),
                    "+=" => (TokenType.PlusAssign, 2),
                    "-=" => (TokenType.MinusAssign, 2),
                    "*=" => (TokenType.StarAssign, 2),
                    "/=" => (TokenType.SlashAssign, 2),
                    "%=" => (TokenType.PercentAssign, 2),
                    "&=" => (TokenType.AmpersandAssign, 2),
                    "|=" => (TokenType.PipeAssign, 2),
                    "^=" => (TokenType.CaretAssign, 2),
                    _ => default
                };
                if (tt.Item1 != default)
                {
                    tokens.Add(new Token(tt.Item1, duo, _pos));
                    _pos += tt.Item2;
                    return true;
                }
            }

            return false;
        }

        private Token ReadSingleChar(char c) => c switch
        {
            '+' => new Token(TokenType.Plus, "+", _pos++),
            '-' => new Token(TokenType.Minus, "-", _pos++),
            '*' => new Token(TokenType.Asterisk, "*", _pos++),
            '/' => new Token(TokenType.Slash, "/", _pos++),
            '%' => new Token(TokenType.Percent, "%", _pos++),
            '&' => new Token(TokenType.Ampersand, "&", _pos++),
            '|' => new Token(TokenType.Pipe, "|", _pos++),
            '^' => new Token(TokenType.Caret, "^", _pos++),
            '~' => new Token(TokenType.Tilde, "~", _pos++),
            '!' => new Token(TokenType.Exclamation, "!", _pos++),
            '?' => new Token(TokenType.QuestionMark, "?", _pos++),
            '.' => new Token(TokenType.Dot, ".", _pos++),
            ',' => new Token(TokenType.Comma, ",", _pos++),
            ':' => new Token(TokenType.Colon, ":", _pos++),
            '(' => new Token(TokenType.OpenParen, "(", _pos++),
            ')' => new Token(TokenType.CloseParen, ")", _pos++),
            '[' => new Token(TokenType.OpenBracket, "[", _pos++),
            ']' => new Token(TokenType.CloseBracket, "]", _pos++),
            '{' => new Token(TokenType.OpenBrace, "{", _pos++),
            '=' => new Token(TokenType.Assign, "=", _pos++),
            '<' => new Token(TokenType.LessThan, "<", _pos++),
            '>' => new Token(TokenType.GreaterThan, ">", _pos++),
            _ => new Token(TokenType.Identifier, c.ToString(), _pos++)
        };

        private static bool IsHexDigit(char c) => char.IsDigit(c) || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
    }

    // ── Expression Parser (recursive descent) ────────────────────────────────

    /// <summary>
    /// Parses a C#/Db expression string into an <see cref="Expression"/> AST node.
    /// Uses recursive descent with proper operator precedence.
    ///
    /// Precedence (lowest to highest):
    ///   1. Assignment:       = += -= *= /= %= &= |= ^= ??=
    ///   2. Null coalescing:  ??
    ///   3. Ternary:         ? :
    ///   4. Logical OR:      ||
    ///   5. Logical AND:     &amp;&amp;
    ///   6. Bitwise OR:      |
    ///   7. Bitwise XOR:     ^
    ///   8. Bitwise AND:     &amp;
    ///   9. Equality:        == !=
    ///  10. Relational:      &lt; &gt; &lt;= &gt;=
    ///  11. Range:           .. ..=
    ///  12. Shift:           &lt;&lt; &gt;&gt;
    ///  13. Additive:        + -
    ///  14. Multiplicative:  * / %
    ///  15. Unary:           ! - + ~ ++ -- (cast)
    ///  16. Postfix:         . -> :: [] () ++ -- ?.
    ///  17. Primary:         literals, identifiers, (expr), new, await
    /// </summary>
    public class ExpressionParser
    {
        private List<Token> _tokens;
        private int _pos;

        /// <summary>
        /// Parse an expression string into an AST node.
        /// Returns null if the expression cannot be parsed.
        /// </summary>
        public Expression ParseExpression(string source)
        {
            if (string.IsNullOrWhiteSpace(source)) return null;

            var lexer = new ExpressionLexer(source);
            _tokens = lexer.Tokenize();
            _pos = 0;

            try
            {
                var expr = ParseAssignment();
                return expr;
            }
            catch
            {
                // If expression parsing fails, return null — caller can fall back to raw text
                return null;
            }
        }

        // ── Token helpers ─────────────────────────────────────────────────────

        private Token Current => _pos < _tokens.Count ? _tokens[_pos] : _tokens[_tokens.Count - 1];
        private Token Peek(int offset = 0) => _pos + offset < _tokens.Count ? _tokens[_pos + offset] : _tokens[_tokens.Count - 1];

        private Token Advance()
        {
            var token = Current;
            if (_pos < _tokens.Count - 1) _pos++;
            return token;
        }

        private bool Match(TokenType type) { if (Current.Type == type) { Advance(); return true; } return false; }
        private bool Match(TokenType type, out Token token) { if (Current.Type == type) { token = Current; Advance(); return true; } token = default; return false; }
        private bool Is(TokenType type) => Current.Type == type;
        private bool Is(params TokenType[] types) { foreach (var t in types) if (Current.Type == t) return true; return false; }

        // ── Precedence levels ──────────────────────────────────────────────────

        /// <summary>
        /// Parses a single argument in a method call, handling out/ref/in modifiers.
        /// Examples: <c>out line</c>, <c>ref x</c>, <c>in value</c>, or just <c>expr</c>.
        /// </summary>
        private Argument ParseArgument()
        {
            string modifier = "";
            if (Is(TokenType.OutKeyword))  { modifier = "out"; Advance(); }
            else if (Is(TokenType.RefKeyword)) { modifier = "ref"; Advance(); }
            else if (Is(TokenType.InKeyword))  { modifier = "in";  Advance(); }
            return new Argument(ParseAssignment(), modifier);
        }

        // Level 1: Assignment (right-to-left)
        private Expression ParseAssignment()
        {
            var expr = ParseWithExpression();

            if (Is(TokenType.Assign, TokenType.PlusAssign, TokenType.MinusAssign,
                   TokenType.StarAssign, TokenType.SlashAssign, TokenType.PercentAssign,
                   TokenType.AmpersandAssign, TokenType.PipeAssign, TokenType.CaretAssign,
                   TokenType.NullCoalesceAssign))
            {
                var op = Advance();
                var value = ParseAssignment(); // right-to-left
                return new AssignmentExpression(expr, op.Value, value);
            }

            return expr;
        }

        // Level 1.5: With expression
        private Expression ParseWithExpression()
        {
            var expr = ParseNullCoalescing();

            while (Match(TokenType.WithKeyword))
            {
                var right = ParseNullCoalescing();
                
                // Flatten chained `with` operations into a single ObjectCreationExpression
                if (expr is ObjectCreationExpression obj && obj.Type == "$with")
                {
                    obj.Arguments.Add(right);
                }
                else
                {
                    var newObj = new ObjectCreationExpression { Type = "$with" };
                    newObj.Arguments.Add(expr);
                    newObj.Arguments.Add(right);
                    expr = newObj;
                }
            }

            return expr;
        }

        // Level 2: Null coalescing ??
        private Expression ParseNullCoalescing()
        {
            var expr = ParseTernary();

            while (Match(TokenType.DoubleQuestion))
            {
                var right = ParseTernary();
                expr = new BinaryExpression(expr, "??", right);
            }

            return expr;
        }

        // Level 3: Ternary ? :
        private Expression ParseTernary()
        {
            var expr = ParseLogicalOr();

            if (Match(TokenType.QuestionMark))
            {
                var trueExpr = ParseAssignment();
                Match(TokenType.Colon); // consume :
                var falseExpr = ParseAssignment();
                return new TernaryExpression(expr, trueExpr, falseExpr);
            }

            return expr;
        }

        // Level 4: Logical OR ||
        private Expression ParseLogicalOr()
        {
            var expr = ParseLogicalAnd();
            while (Match(TokenType.DoublePipe))
            {
                var right = ParseLogicalAnd();
                expr = new BinaryExpression(expr, "||", right);
            }
            return expr;
        }

        // Level 5: Logical AND &&
        private Expression ParseLogicalAnd()
        {
            var expr = ParseBitwiseOr();
            while (Match(TokenType.DoubleAmpersand))
            {
                var right = ParseBitwiseOr();
                expr = new BinaryExpression(expr, "&&", right);
            }
            return expr;
        }

        // Level 6: Bitwise OR |
        private Expression ParseBitwiseOr()
        {
            var expr = ParseBitwiseXor();
            while (Match(TokenType.Pipe))
            {
                var right = ParseBitwiseXor();
                expr = new BinaryExpression(expr, "|", right);
            }
            return expr;
        }

        // Level 7: Bitwise XOR ^
        private Expression ParseBitwiseXor()
        {
            var expr = ParseBitwiseAnd();
            while (Match(TokenType.Caret))
            {
                var right = ParseBitwiseAnd();
                expr = new BinaryExpression(expr, "^", right);
            }
            return expr;
        }

        // Level 8: Bitwise AND &
        private Expression ParseBitwiseAnd()
        {
            var expr = ParseEquality();
            while (Match(TokenType.Ampersand))
            {
                var right = ParseEquality();
                expr = new BinaryExpression(expr, "&", right);
            }
            return expr;
        }

        // Level 9: Equality == !=
        private Expression ParseEquality()
        {
            var expr = ParseRelational();
            while (Is(TokenType.Equals, TokenType.NotEquals))
            {
                var op = Advance();
                var right = ParseRelational();
                expr = new BinaryExpression(expr, op.Value, right);
            }
            return expr;
        }

        // Level 10: Relational < > <= >=
        private Expression ParseRelational()
        {
            var expr = ParseRange();
            while (Is(TokenType.LessThan, TokenType.GreaterThan, TokenType.LessEqual, TokenType.GreaterEqual))
            {
                var op = Advance();
                var right = ParseRange();
                expr = new BinaryExpression(expr, op.Value, right);
            }
            return expr;
        }

        // Level 11: Range .. ..=
        private Expression ParseRange()
        {
            var expr = ParseShift();

            if (Is(TokenType.DotDot) || Is(TokenType.DotDotEquals))
            {
                var op = Advance();
                var right = ParseShift();
                return new RangeExpression
                {
                    Start = expr,
                    End = right,
                    IsInclusive = op.Type == TokenType.DotDotEquals
                };
            }

            // Open-ended range: ..expr
            // This is handled at the primary level when we see .. at the start

            return expr;
        }

        // Level 12: Shift << >>
        private Expression ParseShift()
        {
            var expr = ParseAdditive();

            // << and >> are two < or > tokens
            while (Is(TokenType.LessThan) && Peek(1).Type == TokenType.LessThan ||
                   Is(TokenType.GreaterThan) && Peek(1).Type == TokenType.GreaterThan)
            {
                // Check it's not <= or >=
                if ((Is(TokenType.LessThan) && Peek(1).Type == TokenType.LessThan))
                {
                    Advance(); Advance();
                    var right = ParseAdditive();
                    expr = new BinaryExpression(expr, "<<", right);
                }
                else if ((Is(TokenType.GreaterThan) && Peek(1).Type == TokenType.GreaterThan))
                {
                    Advance(); Advance();
                    var right = ParseAdditive();
                    expr = new BinaryExpression(expr, ">>", right);
                }
            }

            return expr;
        }

        // Level 13: Additive + -
        private Expression ParseAdditive()
        {
            var expr = ParseMultiplicative();

            while (Is(TokenType.Plus, TokenType.Minus))
            {
                var op = Advance();
                var right = ParseMultiplicative();
                expr = new BinaryExpression(expr, op.Value, right);
            }

            return expr;
        }

        // Level 14: Multiplicative * / %
        private Expression ParseMultiplicative()
        {
            var expr = ParseUnary();

            while (Is(TokenType.Asterisk, TokenType.Slash, TokenType.Percent))
            {
                var op = Advance();
                var right = ParseUnary();
                expr = new BinaryExpression(expr, op.Value, right);
            }

            return expr;
        }

        // Level 15: Unary ! - + ~ ++ -- (cast)
        private Expression ParseUnary()
        {
            // Prefix operators
            if (Is(TokenType.Exclamation))
            {
                Advance();
                return new UnaryExpression("!", ParseUnary(), isPrefix: true);
            }
            if (Is(TokenType.Minus))
            {
                // Distinguish unary minus from binary minus:
                // If previous token was a literal/identifier/close, it's binary
                Advance();
                return new UnaryExpression("-", ParseUnary(), isPrefix: true);
            }
            if (Is(TokenType.Plus))
            {
                Advance();
                return new UnaryExpression("+", ParseUnary(), isPrefix: true);
            }
            if (Is(TokenType.Tilde))
            {
                Advance();
                return new UnaryExpression("~", ParseUnary(), isPrefix: true);
            }
            if (Is(TokenType.Increment))
            {
                Advance();
                var operand = ParseUnary();
                return new UnaryExpression("++", operand, isPrefix: true);
            }
            if (Is(TokenType.Decrement))
            {
                Advance();
                var operand = ParseUnary();
                return new UnaryExpression("--", operand, isPrefix: true);
            }

            // Cast: (Type)expr
            if (Is(TokenType.OpenParen))
            {
                // Try to detect a cast expression: (Type)expr
                if (IsCastExpression())
                {
                    return ParseCastExpression();
                }
            }

            return ParsePostfix();
        }

        private bool IsCastExpression()
        {
            // Look ahead: ( Identifier ) with no comma (not a tuple)
            if (!Is(TokenType.OpenParen)) return false;

            int saved = _pos;
            _pos++; // skip (
            // Skip type name (possibly qualified: A.B or A::B)
            if (!Is(TokenType.Identifier)) { _pos = saved; return false; }
            _pos++; // skip type name

            // Handle qualified types: A.B.C or A::B
            while (Is(TokenType.Dot) || Is(TokenType.DoubleColon))
            {
                _pos++;
                if (!Is(TokenType.Identifier)) { _pos = saved; return false; }
                _pos++;
            }

            // Handle generic types: List<int>
            if (Is(TokenType.LessThan))
            {
                int depth = 1;
                _pos++;
                while (_pos < _tokens.Count - 1 && depth > 0)
                {
                    if (Is(TokenType.LessThan)) depth++;
                    else if (Is(TokenType.GreaterThan)) depth--;
                    _pos++;
                }
            }

            bool isCast = Is(TokenType.CloseParen);
            _pos = saved;
            return isCast;
        }

        private Expression ParseCastExpression()
        {
            Advance(); // skip (
            var typeName = ParseTypeName();
            Advance(); // skip )

            // Check for 'as' keyword (already consumed) vs C-style cast
            var inner = ParseUnary();
            return new CastExpression(typeName, inner, isImplicit: true);
        }

        private string ParseTypeName()
        {
            var name = Advance().Value; // identifier
            while (Is(TokenType.Dot) || Is(TokenType.DoubleColon))
            {
                name += Advance().Value; // . or ::
                name += Advance().Value; // next identifier
            }

            // Handle generic type arguments: <T> or <T, U>
            if (Is(TokenType.LessThan))
            {
                name += Advance().Value; // <
                int depth = 1;
                while (depth > 0 && !Is(TokenType.Eof))
                {
                    if (Is(TokenType.LessThan)) depth++;
                    else if (Is(TokenType.GreaterThan)) depth--;
                    name += Advance().Value;
                }
            }

            // Handle array types: [] or [n]
            if (Is(TokenType.OpenBracket))
            {
                name += Advance().Value; // [
                while (!Is(TokenType.CloseBracket) && !Is(TokenType.Eof))
                    name += Advance().Value;
                if (Is(TokenType.CloseBracket)) name += Advance().Value; // ]
            }

            // Handle nullable: ?
            if (Is(TokenType.QuestionMark) && !Is(TokenType.QuestionDot) && !Is(TokenType.QuestionBracket))
            {
                name += Advance().Value; // ?
            }

            return name;
        }

        // Level 16: Postfix . -> :: [] () ++ -- ?. ?[]
        private Expression ParsePostfix()
        {
            var expr = ParsePrimary();

            while (true)
            {
                // Member access: expr.identifier
                if (Is(TokenType.Dot))
                {
                    Advance();
                    string member = Advance().Value;
                    expr = new MemberAccessExpression(expr, member, MemberAccess.Dot);
                }
                // Arrow access: expr->identifier
                else if (Is(TokenType.Arrow))
                {
                    Advance();
                    string member = Advance().Value;
                    expr = new MemberAccessExpression(expr, member, MemberAccess.Arrow);
                }
                // Scope resolution: expr::identifier
                else if (Is(TokenType.DoubleColon))
                {
                    Advance();
                    string member = Advance().Value;
                    expr = new MemberAccessExpression(expr, member, MemberAccess.Colons);
                }
                // Null-conditional member: expr?.identifier
                else if (Is(TokenType.QuestionDot))
                {
                    Advance();
                    string member = Advance().Value;
                    expr = new MemberAccessExpression(expr, member, MemberAccess.QuestionMarkDot);
                }
                // Indexer: expr[args]
                else if (Is(TokenType.OpenBracket))
                {
                    Advance(); // skip [
                    var indexExpr = ParseAssignment();
                    if (Is(TokenType.Comma))
                    {
                        // Multi-dimensional indexer — parse all args
                        // For now, flatten to a single string representation
                        var args = new List<Expression> { indexExpr };
                        while (Match(TokenType.Comma))
                        {
                            args.Add(ParseAssignment());
                        }
                        Advance(); // skip ]
                        // Use the first arg as the index for now; multi-dim can be enhanced later
                        expr = new MemberAccessExpression(expr, "Item", MemberAccess.Bracket);
                    }
                    else
                    {
                        Advance(); // skip ]
                        expr = new MemberAccessExpression(expr, "Item", MemberAccess.Bracket);
                    }
                }
                // Null-conditional indexer: expr?[args]
                else if (Is(TokenType.QuestionBracket))
                {
                    Advance(); // skip ?[
                    var indexExpr = ParseAssignment();
                    Advance(); // skip ]
                    expr = new MemberAccessExpression(expr, "Item", MemberAccess.QuestionMarkBracket);
                }
                // Invocation: expr(args)
                else if (Is(TokenType.OpenParen))
                {
                    Advance(); // skip (
                    var invocation = new InvocationExpression(expr);
                    if (!Is(TokenType.CloseParen))
                    {
                        invocation.Arguments.Add(ParseArgument());
                        while (Match(TokenType.Comma))
                        {
                            invocation.Arguments.Add(ParseArgument());
                        }
                    }
                }
                // Postfix increment: expr++
                else if (Is(TokenType.Increment))
                {
                    Advance();
                    expr = new UnaryExpression("++", expr, isPrefix: false);
                }
                // Postfix decrement: expr--
                else if (Is(TokenType.Decrement))
                {
                    Advance();
                    expr = new UnaryExpression("--", expr, isPrefix: false);
                }
                // 'as' type cast: expr as Type
                else if (Is(TokenType.AsKeyword))
                {
                    Advance();
                    string typeName = ParseTypeName();
                    expr = new CastExpression(typeName, expr, isImplicit: false);
                }
                // 'is' type check: expr is Type
                else if (Is(TokenType.IsKeyword))
                {
                    Advance();
                    string typeName = ParseTypeName();
                    expr = new IsTypeExpression(expr, typeName);
                }
                else
                {
                    break;
                }
            }

            return expr;
        }

        // Level 17: Primary — literals, identifiers, (expr), new, await
        private Expression ParsePrimary()
        {
            // Parenthesized expression or tuple
            if (Is(TokenType.OpenParen))
            {
                Advance(); // skip (

                // Check for empty tuple ()
                if (Is(TokenType.CloseParen))
                {
                    Advance();
                    return new TupleExpression();
                }

                var first = ParseAssignment();

                // Tuple: (expr, expr, ...)
                if (Is(TokenType.Comma))
                {
                    Advance(); // skip )
                    var tuple = new TupleExpression();
                    tuple.Elements.Add(first);
                    while (Match(TokenType.Comma))
                    {
                        tuple.Elements.Add(ParseAssignment());
                    }
                    Advance(); // skip )
                    return tuple;
                }

                // Parenthesized expression
                Advance(); // skip )
                return new ParenthesizedExpression(first);
            }

            // Array creation: [1, 2, 3]
            if (Is(TokenType.OpenBracket))
            {
                Advance(); // skip [
                var arr = new ArrayCreationExpression();
                if (!Is(TokenType.CloseBracket))
                {
                    arr.Elements.Add(ParseAssignment());
                    while (Match(TokenType.Comma))
                    {
                        arr.Elements.Add(ParseAssignment());
                    }
                }
                Advance(); // skip ]
                return arr;
            }

            // new Type(args) or new Type[n]
            if (Is(TokenType.NewKeyword))
            {
                Advance(); // skip new
                string typeName = ParseTypeName();

                // Object creation with arguments: new Type(args)
                if (Is(TokenType.OpenParen))
                {
                    Advance(); // skip (
                    var obj = new ObjectCreationExpression(typeName);
                    if (!Is(TokenType.CloseParen))
                    {
                        obj.Arguments.Add(ParseAssignment());
                        while (Match(TokenType.Comma))
                        {
                            obj.Arguments.Add(ParseAssignment());
                        }
                    }
                    Advance(); // skip )
                    if (Is(TokenType.OpenBrace))
                    {
                        Advance(); // skip {
                        while (!Is(TokenType.CloseParen) && !Is(TokenType.Eof))
                        {
                            string propName = Advance().Value;
                            Advance(); // skip =
                            string value = ParseAssignment()?.ToString() ?? "";
                            obj.NamedArgs.Add(new NamedArgument(propName, value));
                            if (!Match(TokenType.Comma)) break;
                        }
                        // Skip closing brace — we don't have a CloseBrace token,
                        // but object initializers end with } which we handle as Eof fallback
                    }

                    return obj;
                }

                // Array creation with size: new Type[n]
                if (Is(TokenType.OpenBracket))
                {
                    Advance(); // skip [
                    var size = ParseAssignment();
                    Advance(); // skip ]
                    var arr = new ObjectCreationExpression(typeName);
                    arr.Arguments.Add(size);
                    return arr;
                }

                // Default construction: new Type()
                return new ObjectCreationExpression(typeName);
            }

            // await expr
            if (Is(TokenType.AwaitKeyword))
            {
                Advance();
                var inner = ParseUnary();
                return new AwaitExpression(inner);
            }

            // typeof(Type)
            if (Is(TokenType.TypeofKeyword))
            {
                Advance(); // skip typeof
                Advance(); // skip (
                string typeName = ParseTypeName();
                Advance(); // skip )
                return new IdentifierExpression($"typeof({typeName})");
            }

            // sizeof(Type)
            if (Is(TokenType.SizeofKeyword))
            {
                Advance(); // skip sizeof
                Advance(); // skip (
                string typeName = ParseTypeName();
                Advance(); // skip )
                return new IdentifierExpression($"sizeof({typeName})");
            }

            // default(Type) or default
            if (Is(TokenType.DefaultKeyword))
            {
                Advance(); // skip default
                if (Is(TokenType.OpenParen))
                {
                    Advance(); // skip (
                    string typeName = ParseTypeName();
                    Advance(); // skip )
                    return new IdentifierExpression($"default({typeName})");
                }
                return new IdentifierExpression("default");
            }

            // Boolean literals
            if (Match(TokenType.TrueKeyword, out var trueToken))
            {
                return new LiteralExpression("true") { LiteralType = "bool" };
            }
            if (Match(TokenType.FalseKeyword, out var falseToken))
            {
                return new LiteralExpression("false") { LiteralType = "bool" };
            }

            // Null literal
            if (Match(TokenType.NullKeyword, out var nullToken))
            {
                return new LiteralExpression("null") { LiteralType = "null" };
            }

            // Numeric literals
            if (Is(TokenType.IntegerLiteral))
            {
                var token = Advance();
                return new LiteralExpression(token.Value) { LiteralType = "int" };
            }
            if (Is(TokenType.FloatLiteral))
            {
                var token = Advance();
                return new LiteralExpression(token.Value) { LiteralType = "float" };
            }

            // String literals
            if (Is(TokenType.StringLiteral))
            {
                var token = Advance();
                return new LiteralExpression($"\"{token.Value}\"") { LiteralType = "string" };
            }

            // Char literals
            if (Is(TokenType.CharLiteral))
            {
                var token = Advance();
                return new LiteralExpression($"'{token.Value}'") { LiteralType = "char" };
            }

            // Identifier (variable/type name)
            if (Is(TokenType.Identifier))
            {
                var token = Advance();
                return new IdentifierExpression(token.Value);
            }

            // Open-ended range: ..expr or ..=expr
            if (Is(TokenType.DotDot) || Is(TokenType.DotDotEquals))
            {
                bool inclusive = Is(TokenType.DotDotEquals);
                Advance(); // skip .. or ..=
                var end = ParseAssignment();
                return new RangeExpression
                {
                    Start = null,
                    End = end,
                    IsInclusive = inclusive
                };
            }

            // If we get here, we couldn't parse — return null
            return null;
        }
    }
}