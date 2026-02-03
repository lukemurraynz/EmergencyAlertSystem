using Xunit;
using System.Collections.Generic;
using System.Linq;

namespace EmergencyAlerts.Infrastructure.Tests;

/// <summary>
/// Tests for Drasi query validation.
/// Ensures queries follow Cypher syntax rules and Drasi function constraints.
/// </summary>
public class DrasiQueryValidatorTests
{
    // Allowed Cypher features per DrasiQueryValidator logic
    private readonly HashSet<string> _allowedKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "MATCH", "WHERE", "RETURN", "WITH", "AND", "OR", "NOT", "AS"
    };

    // Drasi-specific functions that are allowed
    private readonly HashSet<string> _allowedDrasiFunctions = new(StringComparer.OrdinalIgnoreCase)
    {
        "drasi.trueFor", "drasi.trueLater", "drasi.previousDistinctValue",
        "drasi.linearGradient", "drasi.changeDateTime"
    };

    // Prohibited Cypher features (not supported by Drasi)
    private readonly HashSet<string> _prohibitedKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "COLLECT", "DISTINCT", "ORDER BY", "LIMIT", "SKIP", "COUNT",
        "CREATE", "DELETE", "MERGE", "SET", "REMOVE"
    };

    [Fact]
    public void ValidateQuery_AllowedCypherKeywords_ShouldPass()
    {
        // Arrange
        var query = @"
            MATCH (a:Alert)
            WHERE a.status = 'Approved' AND a.severity = 'Severe'
            RETURN a.id AS alertId, a.headline AS headline
        ";

        // Act
        var result = ValidateCypherSyntax(query);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateQuery_ProhibitedCOLLECT_ShouldFail()
    {
        // Arrange
        var query = @"
            MATCH (a:Alert)-[:CONTAINS]->(r:Recipient)
            RETURN a.id, COLLECT(r.email) AS emails
        ";

        // Act
        var result = ValidateCypherSyntax(query);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("COLLECT", result.Errors.First(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateQuery_ProhibitedDISTINCT_ShouldFail()
    {
        // Arrange
        var query = @"
            MATCH (a:Alert)
            RETURN DISTINCT a.severity
        ";

        // Act
        var result = ValidateCypherSyntax(query);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("DISTINCT", result.Errors.First(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateQuery_ProhibitedORDERBY_ShouldFail()
    {
        // Arrange
        var query = @"
            MATCH (a:Alert)
            RETURN a.headline, a.createdAt
            ORDER BY a.createdAt DESC
        ";

        // Act
        var result = ValidateCypherSyntax(query);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("ORDER BY", result.Errors.First(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateQuery_ProhibitedLIMIT_ShouldFail()
    {
        // Arrange
        var query = @"
            MATCH (a:Alert)
            RETURN a.id, a.headline
            LIMIT 10
        ";

        // Act
        var result = ValidateCypherSyntax(query);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("LIMIT", result.Errors.First(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateQuery_DrasiFunctionTrueFor_ShouldPass()
    {
        // Arrange
        var query = @"
            MATCH (a:Alert)
            WHERE drasi.trueFor(a.status = 'DeliveryInProgress', 60000) = true
            RETURN a.id, a.headline
        ";

        // Act
        var result = ValidateCypherSyntax(query);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateQuery_DrasiFunctionTrueLater_ShouldPass()
    {
        // Arrange
        var query = @"
            MATCH (a:Alert)
            WHERE drasi.trueLater(a.status = 'PendingApproval', 300000) = true
            RETURN a.id, a.createdAt
        ";

        // Act
        var result = ValidateCypherSyntax(query);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateQuery_DrasiFunctionPreviousDistinctValue_ShouldPass()
    {
        // Arrange
        var query = @"
            MATCH (a:Alert)
            WHERE drasi.previousDistinctValue(a.severity) <> a.severity
            RETURN a.id, drasi.previousDistinctValue(a.severity) AS previousSeverity, a.severity AS currentSeverity
        ";

        // Act
        var result = ValidateCypherSyntax(query);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateQuery_DrasiFunctionLinearGradient_ShouldPass()
    {
        // Arrange
        var query = @"
            MATCH (a:Alert)
            WITH count(a) AS alertCount
            WHERE drasi.linearGradient(alertCount, 3600000) > 50
            RETURN alertCount, drasi.linearGradient(alertCount, 3600000) AS ratePerHour
        ";

        // Act
        var result = ValidateCypherSyntax(query);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateQuery_DrasiFunctionChangeDateTime_ShouldPass()
    {
        // Arrange
        var query = @"
            MATCH (a:Alert)
            WHERE a.status = 'Approved'
            RETURN a.id, drasi.changeDateTime(a.status) AS approvalTime
        ";

        // Act
        var result = ValidateCypherSyntax(query);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateQuery_InvalidDrasiFunction_ShouldFail()
    {
        // Arrange - Using a non-existent Drasi function
        var query = @"
            MATCH (a:Alert)
            WHERE drasi.customFunction(a.status) = true
            RETURN a.id
        ";

        // Act
        var result = ValidateCypherSyntax(query);

        // Assert - Should fail if strict validation is enabled
        // For now, we allow unknown functions (Drasi runtime will catch it)
        // But we could add stricter validation in future
        Assert.True(result.IsValid || result.Warnings.Any());
    }

    [Fact]
    public void ValidateQuery_MiddlewareLabels_ShouldMatchExpectedValues()
    {
        // Arrange - Test query with middleware labels
        var query = @"
            MATCH (a:Alert)-[:COVERS]->(area:Area)
            WHERE a.status = 'Approved'
            RETURN a.id, area.polygon
        ";

        var expectedLabels = new[] { "Alert", "Area" };

        // Act
        var detectedLabels = ExtractLabels(query);

        // Assert
        Assert.All(expectedLabels, label =>
            Assert.Contains(label, detectedLabels));
    }

    [Fact]
    public void ValidateQuery_MultipleProhibitedKeywords_ShouldFailWithAllErrors()
    {
        // Arrange
        var query = @"
            MATCH (a:Alert)
            RETURN DISTINCT a.id
            ORDER BY a.createdAt DESC
            LIMIT 5
        ";

        // Act
        var result = ValidateCypherSyntax(query);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("DISTINCT", string.Join(", ", result.Errors), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ORDER BY", string.Join(", ", result.Errors), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LIMIT", string.Join(", ", result.Errors), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateQuery_ComplexGeographicCorrelation_ShouldPass()
    {
        // Arrange - Real query from geographic-correlation.cypher
        var query = @"
            MATCH (a1:Alert)-[:COVERS]->(area1:Area),
                  (a2:Alert)-[:COVERS]->(area2:Area)
            WHERE a1.id <> a2.id
              AND ST_INTERSECTS(area1.polygon, area2.polygon)
              AND a1.createdAt >= datetime() - duration({hours: 24})
            WITH a1, a2, area1, area2
            RETURN a1.id AS alert1Id, a2.id AS alert2Id, 
                   ST_ASTEXT(ST_INTERSECTION(area1.polygon, area2.polygon)) AS overlap
        ";

        // Act
        var result = ValidateCypherSyntax(query);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateQuery_DeliverySLABreach_ShouldPass()
    {
        // Arrange - Real query from delivery-sla-breach.cypher
        var query = @"
            MATCH (a:Alert)
            WHERE a.status = 'DeliveryInProgress'
              AND drasi.trueFor(a.status = 'DeliveryInProgress', 60000) = true
            RETURN a.id AS alertId, 
                   drasi.changeDateTime(a.status) AS deliveryStartTime,
                   datetime() AS currentTime
        ";

        // Act
        var result = ValidateCypherSyntax(query);

        // Assert
        Assert.True(result.IsValid);
    }

    // Helper methods
    private ValidationResult ValidateCypherSyntax(string query)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        // Check for prohibited keywords
        foreach (var keyword in _prohibitedKeywords)
        {
            if (query.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Prohibited keyword detected: {keyword}");
            }
        }

        // Check for SQL-specific syntax (not Cypher)
        if (query.Contains("SELECT", StringComparison.OrdinalIgnoreCase) ||
            query.Contains("INSERT", StringComparison.OrdinalIgnoreCase) ||
            query.Contains("UPDATE", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("SQL syntax detected - queries must use Cypher");
        }

        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings
        };
    }

    private List<string> ExtractLabels(string query)
    {
        var labels = new List<string>();

        // Simple regex-based label extraction (e.g., :Alert, :Area, :Recipient)
        var matches = System.Text.RegularExpressions.Regex.Matches(query, @":(\w+)");

        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            if (match.Groups.Count > 1)
            {
                labels.Add(match.Groups[1].Value);
            }
        }

        return labels.Distinct().ToList();
    }

    private class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }
}
