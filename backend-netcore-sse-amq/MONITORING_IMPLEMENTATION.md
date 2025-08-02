# SSE Connection Monitoring & Statistics Implementation

## Summary
Added comprehensive connection tracking, statistics, and monitoring to the SSE project to help diagnose resource exhaustion issues.

## New Components Added

### 1. ConnectionStatisticsManager.cs
- **Global singleton** for tracking all SSE connection statistics
- **Connection lifecycle management**: Open, close, error tracking
- **Message counting**: Tracks messages processed per connection
- **Event logging**: Last 100 connection events and 100 error events
- **Host-based analytics**: Connections per host, idle connections

### 2. MonitoringController.cs
- **GET /api/monitoring/statistics**: Comprehensive statistics JSON
- **GET /api/monitoring/connections**: Full connection dump with details
- **GET /api/monitoring/health**: Basic health check
- **POST /api/monitoring/test-error**: Test endpoint for error generation

### 3. Enhanced Error Logging
- **InfoController**: Full error tracking with connection IDs
- **AMQConsumerSse**: Error tracking for all consumer tasks
- **ToolbarController**: Message publishing error tracking
- **Connection-specific logging**: Each connection has unique ID for tracing

## Statistics Endpoints

### Statistics API (`/api/monitoring/statistics`)
Returns:
- Total active connections
- Total connections opened/closed
- Total messages processed
- Total errors
- Host statistics (connections per host)
- Last 5 connections opened/closed
- Last 5 error events
- Top 5 idle connections

### Connection Dump API (`/api/monitoring/connections`)
Returns:
- All active connections sorted by open time
- Connection details: host, infoId, broadcast groups
- Duration and idle time (human readable)
- Message count and error count per connection
- Last error details

### Health Check API (`/api/monitoring/health`)
Returns:
- System status
- Active connection count
- Total error count
- Memory usage
- Uptime

## Logging Enhancements

### Connection-Specific Logging
All log messages now include connection ID for tracing:
```
[SSE] Connection abc123 from 192.168.1.100 completed normally
[AMQ-abc123] Personal message received: Hello World
[AMQ-abc123] Error in personal consumer task
```

### Error Categorization
Errors are categorized for better analysis:
- `SSE_STREAM_ERROR`: General SSE stream errors
- `PERSONAL_CONSUMER_ERROR`: Personal queue consumer errors
- `BROADCAST1_CONSUMER_ERROR`: First broadcast consumer errors
- `BROADCAST2_CONSUMER_ERROR`: Second broadcast consumer errors
- `MESSAGE_PROCESSING_ERROR`: Message processing errors
- `SESSION_ROLLBACK_ERROR`: AMQ session rollback errors
- `AMQ_CONSUMER_ERROR`: General AMQ consumer errors
- `TOOLBAR_PUBLISH_ERROR`: Toolbar message publishing errors
- `INVALID_MESSAGE`: Invalid message format errors

## Usage Examples

### Check Current Statistics
```bash
curl http://localhost:5262/api/monitoring/statistics
```

### Get Connection Dump
```bash
curl http://localhost:5262/api/monitoring/connections
```

### Health Check
```bash
curl http://localhost:5262/api/monitoring/health
```

### Generate Test Error
```bash
curl -X POST http://localhost:5262/api/monitoring/test-error \
  -H "Content-Type: application/json" \
  -d '{"errorType": "TEST", "message": "Test error message"}'
```

## Connection Lifecycle

1. **Connection Open**: 
   - Assigned unique connection ID
   - Registered in statistics manager
   - Host and parameters logged

2. **Message Processing**:
   - Each message increments counter
   - Updates last activity timestamp
   - Records connection ID in logs

3. **Error Handling**:
   - All errors categorized and logged
   - Error details stored per connection
   - Host-based error tracking

4. **Connection Close**:
   - Reason for closure recorded
   - Final statistics updated
   - Connection duration calculated

## Benefits for Troubleshooting

1. **Identify Connection Leaks**: See which hosts have most connections
2. **Track Resource Usage**: Monitor active connections vs. limits
3. **Analyze Error Patterns**: See which errors occur most frequently
4. **Monitor Performance**: Track message processing rates
5. **Debug Specific Issues**: Use connection IDs to trace problems
6. **Capacity Planning**: Understand peak usage patterns

This implementation provides comprehensive visibility into the SSE system's operation and will help diagnose why IIS runs out of resources.
