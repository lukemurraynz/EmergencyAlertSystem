using Microsoft.Extensions.Logging;

namespace EmergencyAlerts.Infrastructure.Drasi;

/// <summary>
/// Validator for Drasi continuous queries.
/// Ensures queries use only supported Cypher features and follow best practices.
/// </summary>
public class DrasiQueryValidator
{
    private readonly ILogger<DrasiQueryValidator> _logger;

    // Allowed Cypher keywords and functions per task specification
    private static readonly HashSet<string> AllowedKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "MATCH", "WHERE", "WITH", "RETURN", "AND", "OR", "NOT", "AS", "IS", "NULL"
    };

    private static readonly HashSet<string> AllowedAggregations = new(StringComparer.OrdinalIgnoreCase)
    {
        "COUNT", "SUM", "AVG", "MIN", "MAX"
    };

    private static readonly HashSet<string> AllowedDrasiFunctions = new(StringComparer.OrdinalIgnoreCase)
    {
        "drasi.changeDateTime", "drasi.trueFor", "drasi.trueLater",
        "drasi.previousDistinctValue", "drasi.linearGradient"
    };

    // Prohibited features per task specification
    private static readonly HashSet<string> ProhibitedKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "COLLECT", "DISTINCT", "ORDER BY", "LIMIT", "OFFSET",
        "CREATE", "DELETE", "SET", "REMOVE", "MERGE"
    };

    public DrasiQueryValidator(ILogger<DrasiQueryValidator> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Validates a Drasi continuous query against the allowed feature set.
    /// </summary>
    /// <param name="queryText">The Cypher query text</param>
    /// <returns>Validation result with errors if invalid</returns>
    public DrasiQueryValidationResult Validate(string queryText)
    {
        if (string.IsNullOrWhiteSpace(queryText))
        {
            return new DrasiQueryValidationResult(
                IsValid: false,
                ErrorMessage: "Query text cannot be empty");
        }

        var errors = new List<string>();

        // Check for prohibited keywords
        foreach (var prohibited in ProhibitedKeywords)
        {
            if (queryText.Contains(prohibited, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Prohibited keyword detected: '{prohibited}'. This feature is not supported in Drasi continuous queries.");
            }
        }

        // Validate query structure
        if (!queryText.TrimStart().StartsWith("MATCH", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("Drasi queries must start with MATCH clause");
        }

        if (!queryText.Contains("RETURN", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("Drasi queries must contain a RETURN clause");
        }

        // Check for potential SQL injection or malformed syntax
        if (queryText.Contains("--") || queryText.Contains("/*"))
        {
            errors.Add("Query contains potentially unsafe comment syntax");
        }

        if (errors.Any())
        {
            var errorMessage = string.Join("; ", errors);
            _logger.LogWarning("Drasi query validation failed: {Errors}", errorMessage);

            return new DrasiQueryValidationResult(
                IsValid: false,
                ErrorMessage: errorMessage);
        }

        _logger.LogDebug("Drasi query validation passed");

        return new DrasiQueryValidationResult(
            IsValid: true,
            ErrorMessage: null);
    }

    /// <summary>
    /// Validates that middleware labels match query node labels.
    /// </summary>
    /// <param name="queryLabels">Labels used in MATCH clauses (e.g., "Alert", "Area")</param>
    /// <param name="middlewareLabels">Labels configured in source subscriptions</param>
    /// <returns>Validation result</returns>
    public DrasiQueryValidationResult ValidateLabels(
        IEnumerable<string> queryLabels,
        IEnumerable<string> middlewareLabels)
    {
        var queryLabelSet = new HashSet<string>(queryLabels, StringComparer.OrdinalIgnoreCase);
        var middlewareLabelSet = new HashSet<string>(middlewareLabels, StringComparer.OrdinalIgnoreCase);

        var missingLabels = queryLabelSet.Except(middlewareLabelSet).ToList();

        if (missingLabels.Any())
        {
            var errorMessage = $"Query uses labels that are not configured in middleware: {string.Join(", ", missingLabels)}";
            _logger.LogError("Label validation failed: {ErrorMessage}", errorMessage);

            return new DrasiQueryValidationResult(
                IsValid: false,
                ErrorMessage: errorMessage);
        }

        return new DrasiQueryValidationResult(
            IsValid: true,
            ErrorMessage: null);
    }
}

/// <summary>
/// Result of Drasi query validation.
/// </summary>
/// <param name="IsValid">Whether the query is valid</param>
/// <param name="ErrorMessage">Error message if validation failed</param>
public record DrasiQueryValidationResult(bool IsValid, string? ErrorMessage);
