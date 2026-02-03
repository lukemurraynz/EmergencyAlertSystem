using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace EmergencyAlerts.Load.Tests;

/// <summary>
/// Load tests for Emergency Alert System using xUnit and parallel tasks.
/// 
/// Requirements:
/// - 100 concurrent alert creations
/// - 50 concurrent approvals
/// - 500 dashboard subscribers (SignalR)
/// 
/// Run with:
///   dotnet test --filter "Category=Load"
/// </summary>
public class LoadTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly HttpClient _httpClient;
    private readonly string _apiBase;

    public LoadTests(ITestOutputHelper output)
    {
        _output = output;
        _apiBase = Environment.GetEnvironmentVariable("API_URL") ?? "http://localhost:5000";
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_apiBase),
            Timeout = TimeSpan.FromSeconds(30)
        };
        _httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer test-token-12345");
    }

    [Fact(Skip = "Manual load test - enable when needed")]
    [Trait("Category", "Load")]
    public async Task LoadTest_100ConcurrentAlertCreations_ShouldSucceed()
    {
        // Arrange
        var concurrentRequests = 100;
        var tasks = new List<Task<HttpResponseMessage>>();
        var stopwatch = Stopwatch.StartNew();

        // Act - Create 100 concurrent alerts
        for (int i = 0; i < concurrentRequests; i++)
        {
            var alert = CreateTestAlert($"Load Test Alert {i}");
            var task = _httpClient.PostAsJsonAsync("/api/v1/alerts", alert);
            tasks.Add(task);
        }

        var responses = await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        var successCount = responses.Count(r => r.IsSuccessStatusCode);
        var avgResponseTime = stopwatch.ElapsedMilliseconds / (double)concurrentRequests;

        _output.WriteLine($"Total Requests: {concurrentRequests}");
        _output.WriteLine($"Successful: {successCount} ({(successCount * 100.0 / concurrentRequests):F2}%)");
        _output.WriteLine($"Failed: {concurrentRequests - successCount}");
        _output.WriteLine($"Total Time: {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"Average Response Time: {avgResponseTime:F2}ms");

        // Success criteria: >95% success rate, <5s per request
        Assert.True(successCount >= concurrentRequests * 0.95, $"Success rate below 95%: {successCount}/{concurrentRequests}");
        Assert.True(avgResponseTime < 5000, $"Average response time exceeds 5s: {avgResponseTime}ms");
    }

    [Fact(Skip = "Manual load test - enable when needed")]
    [Trait("Category", "Load")]
    public async Task LoadTest_50ConcurrentApprovals_ShouldHandleFirstWins()
    {
        // Arrange - Create alerts first
        var alertIds = new List<Guid>();
        for (int i = 0; i < 50; i++)
        {
            var alert = CreateTestAlert($"Approval Load Test {i}");
            var response = await _httpClient.PostAsJsonAsync("/api/v1/alerts", alert);
            response.EnsureSuccessStatusCode();
            var created = await response.Content.ReadFromJsonAsync<AlertResponse>();
            alertIds.Add(created!.Id);
        }

        // Act - 50 concurrent approvals
        var stopwatch = Stopwatch.StartNew();
        var tasks = alertIds.Select(id =>
            _httpClient.PostAsync($"/api/v1/alerts/{id}/approve", null)
        ).ToList();

        var responses = await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        var successCount = responses.Count(r => r.StatusCode == System.Net.HttpStatusCode.NoContent);
        var conflictCount = responses.Count(r => r.StatusCode == System.Net.HttpStatusCode.Conflict);

        _output.WriteLine($"Total Approval Requests: {alertIds.Count}");
        _output.WriteLine($"Successful (204): {successCount}");
        _output.WriteLine($"Conflicts (409): {conflictCount}");
        _output.WriteLine($"Total Time: {stopwatch.ElapsedMilliseconds}ms");

        // All should succeed (first-wins semantics)
        Assert.Equal(50, successCount);
    }

    [Fact(Skip = "Manual load test - enable when needed")]
    [Trait("Category", "Load")]
    public async Task LoadTest_500DashboardQueries_ShouldHandleConcurrency()
    {
        // Arrange
        var concurrentRequests = 500;
        var stopwatch = Stopwatch.StartNew();

        // Act - 500 concurrent dashboard queries (simulating 500 subscribers polling)
        var tasks = Enumerable.Range(0, concurrentRequests)
            .Select(_ => _httpClient.GetAsync("/api/v1/dashboard/summary"))
            .ToList();

        var responses = await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        var successCount = responses.Count(r => r.IsSuccessStatusCode);
        var avgResponseTime = stopwatch.ElapsedMilliseconds / (double)concurrentRequests;

        _output.WriteLine($"Total Dashboard Requests: {concurrentRequests}");
        _output.WriteLine($"Successful: {successCount}");
        _output.WriteLine($"Total Time: {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"Average Response Time: {avgResponseTime:F2}ms");

        // Success criteria: 100% success, <2s avg response
        Assert.Equal(concurrentRequests, successCount);
        Assert.True(avgResponseTime < 2000, $"Average response time exceeds 2s: {avgResponseTime}ms");
    }

    [Fact(Skip = "Manual load test - enable when needed")]
    [Trait("Category", "Load")]
    public async Task LoadTest_MixedWorkload_ShouldMaintainPerformance()
    {
        // Arrange - Mix of creates, approvals, and queries
        var stopwatch = Stopwatch.StartNew();
        var tasks = new List<Task<HttpResponseMessage>>();

        // 50 creates
        for (int i = 0; i < 50; i++)
        {
            var alert = CreateTestAlert($"Mixed Load Test {i}");
            tasks.Add(_httpClient.PostAsJsonAsync("/api/v1/alerts", alert));
        }

        // 25 approvals (async, may not have alerts yet)
        for (int i = 0; i < 25; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                await Task.Delay(100); // Small delay to let creates finish
                var listResponse = await _httpClient.GetAsync("/api/v1/alerts?status=PendingApproval&pageSize=1");
                if (listResponse.IsSuccessStatusCode)
                {
                    var alerts = await listResponse.Content.ReadFromJsonAsync<AlertListResponse>();
                    if (alerts?.Items?.Any() == true)
                    {
                        return await _httpClient.PostAsync($"/api/v1/alerts/{alerts.Items[0].Id}/approve", null);
                    }
                }
                return listResponse; // Return list response if no alerts to approve
            }));
        }

        // 100 dashboard queries
        for (int i = 0; i < 100; i++)
        {
            tasks.Add(_httpClient.GetAsync("/api/v1/dashboard/summary"));
        }

        // Act
        var responses = await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        var successCount = responses.Count(r => r.IsSuccessStatusCode);
        var totalRequests = tasks.Count;

        _output.WriteLine($"Total Mixed Requests: {totalRequests}");
        _output.WriteLine($"Successful: {successCount} ({(successCount * 100.0 / totalRequests):F2}%)");
        _output.WriteLine($"Total Time: {stopwatch.ElapsedMilliseconds}ms");

        // Success criteria: >90% success rate under mixed load
        Assert.True(successCount >= totalRequests * 0.90, $"Mixed load success rate below 90%");
    }

    [Fact(Skip = "Manual stress test - enable when needed")]
    [Trait("Category", "Stress")]
    public async Task StressTest_SustainedLoad_60SecondsAt50RPS_ShouldMaintainSLA()
    {
        // Arrange
        var durationSeconds = 60;
        var requestsPerSecond = 50;
        var totalRequests = durationSeconds * requestsPerSecond;
        var delayMs = 1000 / requestsPerSecond;

        var stopwatch = Stopwatch.StartNew();
        var successCount = 0;
        var responseTimes = new List<long>();

        // Act - Sustained load for 60 seconds
        for (int i = 0; i < totalRequests; i++)
        {
            var requestStopwatch = Stopwatch.StartNew();

            var alert = CreateTestAlert($"Stress Test Alert {i}");
            var response = await _httpClient.PostAsJsonAsync("/api/v1/alerts", alert);

            requestStopwatch.Stop();
            responseTimes.Add(requestStopwatch.ElapsedMilliseconds);

            if (response.IsSuccessStatusCode)
            {
                successCount++;
            }

            // Maintain ~50 RPS
            await Task.Delay(delayMs);
        }

        stopwatch.Stop();

        // Assert
        var avgResponseTime = responseTimes.Average();
        var p95ResponseTime = responseTimes.OrderBy(t => t).ElementAt((int)(totalRequests * 0.95));

        _output.WriteLine($"Total Requests: {totalRequests}");
        _output.WriteLine($"Successful: {successCount} ({(successCount * 100.0 / totalRequests):F2}%)");
        _output.WriteLine($"Duration: {stopwatch.Elapsed.TotalSeconds:F2}s");
        _output.WriteLine($"Avg Response Time: {avgResponseTime:F2}ms");
        _output.WriteLine($"P95 Response Time: {p95ResponseTime}ms");
        _output.WriteLine($"Actual RPS: {totalRequests / stopwatch.Elapsed.TotalSeconds:F2}");

        // SLA: >95% success, P95 <5s
        Assert.True(successCount >= totalRequests * 0.95, "Sustained load success rate below 95%");
        Assert.True(p95ResponseTime < 5000, $"P95 response time exceeds 5s: {p95ResponseTime}ms");
    }

    // Helper methods
    private object CreateTestAlert(string headline)
    {
        return new
        {
            headline,
            description = $"Automated load test alert created at {DateTime.UtcNow:O}",
            severity = "Moderate",
            expiresAt = DateTime.UtcNow.AddHours(2),
            area = new
            {
                polygon = GenerateRandomPolygon()
            }
        };
    }

    private string GenerateRandomPolygon()
    {
        var random = new Random();
        var baseLon = -122.4 + (random.NextDouble() - 0.5) * 0.5;
        var baseLat = 47.6 + (random.NextDouble() - 0.5) * 0.5;
        var size = 0.05;

        var minLon = baseLon - size;
        var maxLon = baseLon + size;
        var minLat = baseLat - size;
        var maxLat = baseLat + size;

        return $"POLYGON(({minLon} {minLat}, {maxLon} {minLat}, {maxLon} {maxLat}, {minLon} {maxLat}, {minLon} {minLat}))";
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }

    // Response DTOs
    private record AlertResponse(Guid Id, string Headline, string Status, string Etag);
    private record AlertListResponse(List<AlertResponse> Items, int TotalCount);
}
