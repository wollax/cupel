using Microsoft.ML.Tokenizers;

namespace Wollax.Cupel.Tiktoken;

/// <summary>
/// Provides accurate tiktoken-based token counting for <see cref="ContextItem"/> content
/// using the official Microsoft tokenizer implementation.
/// </summary>
/// <remarks>
/// <para>
/// This class wraps <see cref="TiktokenTokenizer"/> from the
/// <c>Microsoft.ML.Tokenizers</c> package. Consumers must add the appropriate
/// data package for their target model:
/// </para>
/// <list type="bullet">
///   <item><c>Microsoft.ML.Tokenizers.Data.O200kBase</c> — for GPT-4o, o1, o3 models</item>
///   <item><c>Microsoft.ML.Tokenizers.Data.Cl100kBase</c> — for GPT-4, GPT-3.5-turbo models</item>
/// </list>
/// </remarks>
public sealed class TiktokenTokenCounter
{
    private readonly Tokenizer _tokenizer;

    private TiktokenTokenCounter(Tokenizer tokenizer)
    {
        _tokenizer = tokenizer;
    }

    /// <summary>
    /// Creates a token counter configured for the specified model.
    /// </summary>
    /// <param name="modelName">The model name (e.g. "gpt-4o", "gpt-4", "gpt-3.5-turbo").</param>
    /// <returns>A new <see cref="TiktokenTokenCounter"/> instance for the given model.</returns>
    /// <exception cref="NotSupportedException">The model name is not recognized.</exception>
    public static TiktokenTokenCounter CreateForModel(string modelName)
    {
        var tokenizer = TiktokenTokenizer.CreateForModel(modelName);
        return new TiktokenTokenCounter(tokenizer);
    }

    /// <summary>
    /// Creates a token counter configured for the specified encoding.
    /// </summary>
    /// <param name="encodingName">The encoding name (e.g. "o200k_base", "cl100k_base").</param>
    /// <returns>A new <see cref="TiktokenTokenCounter"/> instance for the given encoding.</returns>
    /// <exception cref="NotSupportedException">The encoding name is not recognized.</exception>
    public static TiktokenTokenCounter CreateForEncoding(string encodingName)
    {
        var tokenizer = TiktokenTokenizer.CreateForEncoding(encodingName);
        return new TiktokenTokenCounter(tokenizer);
    }

    /// <summary>
    /// Counts the number of tokens in the specified text.
    /// </summary>
    /// <param name="text">The text to tokenize.</param>
    /// <returns>The number of tokens in the text.</returns>
    public int CountTokens(string text)
    {
        return _tokenizer.CountTokens(text);
    }

    /// <summary>
    /// Counts the number of tokens in the specified text span.
    /// </summary>
    /// <param name="text">The text span to tokenize.</param>
    /// <returns>The number of tokens in the text.</returns>
    public int CountTokens(ReadOnlySpan<char> text)
    {
        return _tokenizer.CountTokens(text);
    }

    /// <summary>
    /// Returns a new <see cref="ContextItem"/> with <see cref="ContextItem.Tokens"/>
    /// set to the counted token value for the item's content.
    /// All other properties are preserved.
    /// </summary>
    /// <param name="item">The context item whose tokens should be counted.</param>
    /// <returns>A new <see cref="ContextItem"/> with the accurate token count.</returns>
    public ContextItem WithTokenCount(ContextItem item)
    {
        return item with { Tokens = CountTokens(item.Content) };
    }
}
