using System;
using System.IO;

namespace taste
{
    /// <summary>
    /// Custom exception for transpiler errors with error codes and suggestions.
    /// Used by any transpilation pipeline to report structured errors.
    /// </summary>
    public class TranspilerException : Exception
    {
        public string ErrorCode { get; }
        public string? Suggestion { get; }
        public int LineNumber { get; }

        public TranspilerException(string errorCode, string message, string? suggestion = null, int lineNumber = 0)
            : base(message)
        {
            ErrorCode = errorCode;
            Suggestion = suggestion;
            LineNumber = lineNumber;
        }
    }

    // ── Transpiler base ───────────────────────────────────────────────────

    /// <summary>
    /// Abstract base for all transpilation pipelines.
    /// Defines the universal compile pattern: Source → Parse → Analyze → Emit → Output.
    /// Subclass this to wire up a specific source→target language pair
    /// (e.g. Db→C++, Db→Rust, C#→TypeScript).
    /// <para>
    /// The pipeline phases are:
    /// <list type="number">
    ///   <item><see cref="Parse"/> — source text → <see cref="CodeFile"/> AST</item>
    ///   <item><see cref="Analyze"/> — semantic validation</item>
    ///   <item><see cref="Emit"/> — AST → target-language source string</item>
    /// </list>
    /// Each phase is an abstract method so subclasses inject language-specific logic.
    /// Subclasses can add their own phases (e.g. stub resolution) by overriding
    /// <see cref="TranspileFile"/> or adding custom methods.
    /// </para>
    /// </summary>
    public abstract class Transpiler
    {
        /// <summary>
        /// Transpiles a source file to a target-language output file.
        /// Orchestrates the universal pipeline: parse → analyze → emit → write.
        /// </summary>
        /// <param name="inputPath">Path to the input source file.</param>
        /// <param name="outputPath">Path to write the output target-language file.</param>
        public void TranspileFile(string inputPath, string outputPath)
        {
            if (!File.Exists(inputPath))
            {
                throw new TranspilerException(
                    "E003",
                    $"Input file not found: {inputPath}",
                    "Check that the file path is correct and the file exists"
                );
            }

            try
            {
                // 1. Parse the source into the AST
                CodeFile codeFile = Parse(inputPath);

                // 2. Semantic analysis (validation + allocation strategies)
                Analyze(codeFile);

                // 3. Emit the AST to target-language source
                string output = Emit(codeFile);

                // 4. Write output to disk
                string outputDir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
                    Directory.CreateDirectory(outputDir);

                File.WriteAllText(outputPath, output);
            }
            catch (TranspilerException)
            {
                throw; // Re-throw our own exceptions as-is
            }
            catch (Exception ex)
            {
                throw new TranspilerException(
                    "E001",
                    $"Failed to transpile {inputPath}: {ex.Message}",
                    "Check the source file for syntax errors",
                    0
                );
            }
        }

        // ── Pipeline phases (override in subclass) ─────────────────────────

        /// <summary>
        /// Phase 1: Parse a source file into a <see cref="CodeFile"/> AST.
        /// </summary>
        /// <param name="inputPath">Path to the source file.</param>
        protected abstract CodeFile Parse(string inputPath);

        /// <summary>
        /// Phase 2: Semantic analysis — validate types, inheritance, contracts,
        /// and resolve allocation strategies.
        /// </summary>
        protected abstract void Analyze(CodeFile codeFile);

        /// <summary>
        /// Phase 4: Emit the AST to a target-language source string.
        /// </summary>
        protected abstract string Emit(CodeFile codeFile);

        /// <summary>
        /// Writes the emitted output to disk, creating directories as needed.
        /// Call this from subclass overrides if you intercept the pipeline.
        /// </summary>
        protected void WriteOutput(string inputPath, string outputPath, string output)
        {
            string outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);

            File.WriteAllText(outputPath, output);
        }
    }
}