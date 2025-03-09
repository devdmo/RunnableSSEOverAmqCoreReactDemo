curl -X POST http://localhost:5262/api/toolbar/send \
  -H "Content-Type: application/json" \
  -d '{"id": "test", "text": "Hello from curl on port 5262!"}'

