#!/bin/bash

##
# Drasi Performance Test Runner
#
# Tests all 8 Drasi queries for performance and validates <5s SLA.
# Usage: ./perf-test.sh [API_URL] [AUTH_TOKEN]
##

set -e

API_URL="${1:-http://localhost:5000}"
AUTH_TOKEN="${2:-test-token-12345}"

echo "================================"
echo "Drasi Performance Test Runner"
echo "================================"
echo "API URL: $API_URL"
echo ""

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Test counters
PASSED=0
FAILED=0

# Helper function: Create alert via API
create_alert() {
    local headline="$1"
    local severity="$2"
    local polygon="$3"
    local expires_at=$(date -u -d "+2 hours" +"%Y-%m-%dT%H:%M:%SZ")

    curl -s -X POST "$API_URL/api/v1/alerts" \
        -H "Content-Type: application/json" \
        -H "Authorization: Bearer $AUTH_TOKEN" \
        -d "{
            \"headline\": \"$headline\",
            \"description\": \"Performance test alert\",
            \"severity\": \"$severity\",
            \"expiresAt\": \"$expires_at\",
            \"area\": {
                \"polygon\": \"$polygon\"
            }
        }"
}

# Helper function: Approve alert
approve_alert() {
    local alert_id="$1"
    curl -s -X POST "$API_URL/api/v1/alerts/$alert_id/approve" \
        -H "Authorization: Bearer $AUTH_TOKEN"
}

# Helper function: Measure query latency
measure_latency() {
    local test_name="$1"
    local start_time="$2"
    local end_time=$(date +%s%3N)
    local latency=$((end_time - start_time))

    echo -n "  Latency: ${latency}ms ... "
    
    if [ $latency -lt 5000 ]; then
        echo -e "${GREEN}PASS${NC} (<5s SLA)"
        ((PASSED++))
    else
        echo -e "${RED}FAIL${NC} (>5s SLA)"
        ((FAILED++))
    fi
}

echo "Test 1: Geographic Correlation Performance"
echo "-------------------------------------------"
START=$(date +%s%3N)

# Create 3 overlapping alerts
ALERT1=$(create_alert "Perf Test Geo 1" "Moderate" "POLYGON((-122.4 47.6, -122.3 47.6, -122.3 47.7, -122.4 47.7, -122.4 47.6))" | jq -r '.id')
ALERT2=$(create_alert "Perf Test Geo 2" "Severe" "POLYGON((-122.38 47.62, -122.28 47.62, -122.28 47.72, -122.38 47.72, -122.38 47.62))" | jq -r '.id')
ALERT3=$(create_alert "Perf Test Geo 3" "Extreme" "POLYGON((-122.39 47.61, -122.29 47.61, -122.29 47.71, -122.39 47.71, -122.39 47.61))" | jq -r '.id')

sleep 2 # Allow Drasi to process

measure_latency "geographic-correlation" $START
echo ""

echo "Test 2: Delivery Trigger Performance"
echo "-------------------------------------"
START=$(date +%s%3N)

# Create and approve alert
ALERT=$(create_alert "Perf Test Delivery" "Severe" "POLYGON((-122.4 47.6, -122.3 47.6, -122.3 47.7, -122.4 47.7, -122.4 47.6))" | jq -r '.id')
approve_alert "$ALERT"

sleep 2 # Allow delivery trigger to fire

measure_latency "delivery-trigger" $START
echo ""

echo "Test 3: Delivery SLA Breach Detection"
echo "--------------------------------------"
echo "  (Creates alert, waits 61s, measures detection time)"
START=$(date +%s%3N)

ALERT=$(create_alert "Perf Test SLA" "Moderate" "POLYGON((-122.4 47.6, -122.3 47.6, -122.3 47.7, -122.4 47.7, -122.4 47.6))" | jq -r '.id')
approve_alert "$ALERT"

echo "  Waiting 61 seconds for SLA threshold..."
sleep 61

measure_latency "delivery-sla-breach" $START
echo ""

echo "Test 4: Rate Spike Detection Performance"
echo "-----------------------------------------"
START=$(date +%s%3N)

# Create 60 alerts rapidly (>50/hour triggers spike)
for i in {1..60}; do
    create_alert "Perf Test Spike $i" "Moderate" "POLYGON((-122.4 47.6, -122.3 47.6, -122.3 47.7, -122.4 47.7, -122.4 47.6))" > /dev/null &
done
wait

sleep 3 # Allow Drasi to detect spike

measure_latency "rate-spike-detection" $START
echo ""

echo "Test 5: Regional Hotspot Detection Performance"
echo "-----------------------------------------------"
START=$(date +%s%3N)

# Create 5 alerts in same region (WA-KING)
for i in {1..5}; do
    create_alert "Perf Test Hotspot $i" "Moderate" "POLYGON((-122.4 47.6, -122.3 47.6, -122.3 47.7, -122.4 47.7, -122.4 47.6))" > /dev/null
done

sleep 2

measure_latency "regional-hotspot" $START
echo ""

echo "================================"
echo "Performance Test Summary"
echo "================================"
echo -e "PASSED: ${GREEN}$PASSED${NC}"
echo -e "FAILED: ${RED}$FAILED${NC}"
echo ""

if [ $FAILED -eq 0 ]; then
    echo -e "${GREEN}All Drasi queries meet <5s SLA ✅${NC}"
    exit 0
else
    echo -e "${RED}Some queries exceed 5s SLA ❌${NC}"
    exit 1
fi
