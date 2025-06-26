# Testing Dual Broadcast Group Support

## Overview
The system now supports reading from up to 2 broadcast groups simultaneously. This allows Info pages to subscribe to messages from multiple broadcast groups concurrently.

## How It Works

### Backend
1. **InfoController** accepts `broadcastGroup` and `broadcastGroup2` query parameters
2. **AMQConsumerSse** creates separate consumer sessions for each broadcast group
3. Messages from both groups are merged into the same SSE stream
4. **ToolbarController** can send messages to either or both broadcast groups

### Frontend
1. **InfoPage** allows users to specify up to 2 broadcast groups
2. **Toolbar** allows sending messages to both broadcast groups

## Testing Scenarios

### Scenario 1: Single Broadcast Group (Existing Functionality)
1. Start the backend server
2. Open InfoPage in browser
3. Enter Info ID: `test1`
4. Enter Broadcast Group: `group1`
5. Connect
6. Use Toolbar to send broadcast message to `group1`
7. Verify message appears on InfoPage

### Scenario 2: Dual Broadcast Groups (New Functionality)
1. Start the backend server
2. Open InfoPage in browser
3. Enter Info ID: `test2`
4. Enter Broadcast Group: `group1`
5. Enter Broadcast Group 2: `group2`
6. Connect
7. Use Toolbar to send broadcast message to `group1`
8. Use another Toolbar instance to send broadcast message to `group2`
9. Verify both messages appear on InfoPage

### Scenario 3: Personal + Dual Broadcast
1. Start the backend server
2. Open InfoPage in browser
3. Enter Info ID: `personal1`
4. Enter Broadcast Group: `broadcast1`
5. Enter Broadcast Group 2: `broadcast2`
6. Connect
7. Send personal message with Toolbar ID: `personal1`
8. Send broadcast message to `broadcast1`
9. Send broadcast message to `broadcast2`
10. Verify all three messages appear on InfoPage

## API Endpoints

### SSE Stream Endpoint
```
GET /api/info/stream?id={infoId}&broadcastGroup={group1}&broadcastGroup2={group2}
```

### Send Message Endpoint
```
POST /api/toolbar/send
{
  "id": "broadcast",
  "text": "Hello World",
  "broadcastGroup": "group1",
  "broadcastGroup2": "group2"
}
```

## Key Implementation Details

1. **Concurrent Consumers**: The system runs up to 3 concurrent AMQ consumers:
   - 1 for personal messages (queue)
   - 1 for first broadcast group (topic)
   - 1 for second broadcast group (topic, optional)

2. **Message Filtering**: Each consumer uses JMS selectors to filter messages:
   - Personal: `id = '{infoId}'`
   - Broadcast 1: `broadcastGroup = '{broadcastGroup}'`
   - Broadcast 2: `broadcastGroup = '{broadcastGroup2}'`

3. **Fallback Behavior**: If no broadcast groups are specified, the system falls back to receiving all broadcast messages (existing behavior)

4. **Dual Publishing**: When sending to both broadcast groups, the Toolbar publishes the message twice with different broadcastGroup properties

## Notes

- The system maintains backward compatibility with existing single broadcast group functionality
- Empty broadcast group parameters are handled gracefully
- All changes include comprehensive logging for debugging
- The frontend UI clearly shows which broadcast groups are active
