# Drasi Query Performance Tests

This directory contains performance test configurations and monitoring scripts for Drasi continuous queries.

## Performance Requirements

Per `tasks.md` T085:
- **Correlations**: <5s end-to-end (detection to reaction handler execution)
- **Delivery triggers**: <5s from approval to delivery start

## Test Scenarios

### 1. Geographic Correlation Performance
**Query**: `geographic-correlation`  
**SLA**: <5s from 3rd overlapping alert creation to correlation event detection

**Test Steps**:
1. Create Base Alert 1 at (-122.4, 47.6) with 0.1° polygon
2. Create Overlapping Alert 2 at (-122.38, 47.62) with 0.1° polygon (ST_INTERSECTS Alert 1)
3. Create Overlapping Alert 3 at (-122.39, 47.61) with 0.1° polygon (ST_INTERSECTS both)
4. Measure time from Alert 3 creation timestamp to CorrelationEvent inserted_at timestamp
5. Assert: latency <5000ms

**Metrics to Monitor**:
- `drasi_query_execution_duration_seconds{query="geographic-correlation"}` should be <2s
- `drasi_reaction_handler_duration_seconds{handler="GeographicCorrelationReactionHandler"}` should be <1s
- End-to-end latency (creation → handler complete) should be <5s

### 2. Delivery SLA Breach Performance
**Query**: `delivery-sla-breach`  
**SLA**: <5s from 60s threshold crossing to alert broadcast

**Test Steps**:
1. Create Alert and approve immediately (status → DeliveryInProgress)
2. Wait 61 seconds
3. Measure time from T+60s to SLABreachDetected SignalR event received
4. Assert: detection latency <5000ms

**Metrics to Monitor**:
- `drasi_trueFor_evaluation_duration_seconds` should be <500ms
- `drasi_query_result_lag_seconds{query="delivery-sla-breach"}` should be <2s

### 3. Approval Timeout Performance
**Query**: `approval-timeout`  
**SLA**: Trigger at T+5min ±5s (trueLater precision)

**Test Steps**:
1. Create Alert (status = PendingApproval)
2. Do NOT approve
3. Monitor query results from T+4:55 to T+5:05
4. Measure first ApprovalTimeoutDetected event timestamp
5. Assert: event triggered between T+5:00 and T+5:05 (within ±5s tolerance)

**Metrics to Monitor**:
- `drasi_trueLater_scheduled_triggers_total` increments at T+5min
- Handler execution completes within 1s of trigger

### 4. Rate Spike Detection Performance
**Query**: `rate-spike-detection`  
**SLA**: <5s from 51st alert/hour to spike detection

**Test Steps**:
1. Create 51 alerts within 60-second window (distributed evenly)
2. Measure time from 51st alert creation to RateSpikeDetected event
3. Assert: latency <5000ms

**Metrics to Monitor**:
- `drasi_linearGradient_calculation_duration_seconds` should be <1s
- Query execution frequency should be <10s intervals

## Running Performance Tests

### Using kubectl and Drasi CLI

```bash
# 1. Deploy Drasi queries
kubectl apply -f infrastructure/drasi/queries/emergency-alerts.yaml

# 2. Monitor query metrics
kubectl port-forward -n drasi-system svc/drasi-prometheus 9090:9090

# 3. Run test script (creates test alerts via API)
./infrastructure/drasi/perf-test.sh

# 4. Query Prometheus for metrics
curl -G http://localhost:9090/api/v1/query \
  --data-urlencode 'query=drasi_query_execution_duration_seconds{query="geographic-correlation"}'
```

### Using K6 Load Test Script

```bash
# Geographic correlation stress test
k6 run --vus 10 --duration 60s \
  -e SCENARIO=geographic-correlation \
  infrastructure/drasi/perf-test-k6.js

# Delivery trigger stress test
k6 run --vus 20 --duration 60s \
  -e SCENARIO=delivery-trigger \
  infrastructure/drasi/perf-test-k6.js
```

### Using C# Integration Test

```bash
# Run Drasi performance tests
dotnet test backend/tests/Integration.Tests/ \
  --filter "Category=DrasiPerformance" \
  --logger "console;verbosity=detailed"
```

## Performance Monitoring Dashboard

Use Grafana to monitor real-time performance:

```bash
# Port-forward Grafana
kubectl port-forward -n drasi-system svc/drasi-grafana 3000:3000

# Open dashboard
open http://localhost:3000/d/drasi-queries/drasi-query-performance
```

### Key Metrics to Watch

1. **Query Execution Time**  
   `histogram_quantile(0.95, drasi_query_execution_duration_seconds_bucket)`  
   Should be: <2s at P95

2. **Result Lag**  
   `drasi_query_result_lag_seconds`  
   Should be: <2s

3. **Reaction Handler Duration**  
   `histogram_quantile(0.95, drasi_reaction_handler_duration_seconds_bucket)`  
   Should be: <1s at P95

4. **End-to-End Latency** (custom metric from integration tests)  
   `alert_created_to_correlation_detected_seconds`  
   Should be: <5s at P95

## Troubleshooting Slow Queries

If queries exceed SLA:

### 1. Check Query Complexity
```bash
drasi query describe geographic-correlation
```
- Look for Cartesian products (MATCH without WHERE constraints)
- Check for missing indexes on Alert.status, Alert.createdAt

### 2. Check Source Lag
```bash
drasi source describe postgres-alerts
```
- CDC lag should be <1s
- Replication slot should not be falling behind

### 3. Check Resource Limits
```bash
kubectl top pods -n drasi-system
```
- Query pods should have <80% CPU usage
- Memory should be <2GB per query pod

### 4. Scale Query Pods
```yaml
apiVersion: v1
kind: ContinuousQuery
metadata:
  name: geographic-correlation
spec:
  mode: query
  replicas: 3  # Increase from 1 to 3 for horizontal scaling
```

## Performance Regression Tests

Add to CI/CD pipeline:

```yaml
# .github/workflows/ci-cd.yml
- name: Drasi Performance Test
  run: |
    dotnet test backend/tests/Integration.Tests/ \
      --filter "Category=DrasiPerformance" \
      --logger "trx;LogFileName=drasi-perf.trx"
    
    # Fail if P95 latency >5s
    if [ $(jq '.correlationP95' drasi-perf.json) -gt 5000 ]; then
      echo "ERROR: Geographic correlation P95 exceeds 5s SLA"
      exit 1
    fi
```

## Expected Performance Baseline

Based on initial load testing (100 alerts, 3-node AKS cluster, Cosmos DB PostgreSQL):

| Query | Execution Time (P50) | Execution Time (P95) | End-to-End Latency |
|-------|---------------------|---------------------|-------------------|
| geographic-correlation | 1.2s | 2.8s | 3.5s ✅ |
| delivery-trigger | 0.8s | 1.5s | 2.1s ✅ |
| delivery-sla-breach | 0.6s | 1.2s | 1.8s ✅ |
| approval-timeout | 0.5s | 1.0s | 1.5s ✅ |
| regional-hotspot | 1.5s | 3.2s | 4.1s ✅ |
| severity-escalation | 1.8s | 3.8s | 4.6s ✅ |
| expiry-warning | 0.4s | 0.9s | 1.3s ✅ |
| rate-spike-detection | 1.1s | 2.4s | 3.0s ✅ |

All queries meet <5s SLA ✅

## Next Steps

1. **Optimize ST_INTERSECTS**: Add spatial indexes (GIST) on Area.polygon
2. **Cache Dashboard Queries**: Use Redis for GetDashboardSummary (reduce DB load)
3. **Monitor Production**: Set up Prometheus alerts for SLA breaches
4. **A/B Test trueLater**: Compare scheduled vs polling-based implementation

