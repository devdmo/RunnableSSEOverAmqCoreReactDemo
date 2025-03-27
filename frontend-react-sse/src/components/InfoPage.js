import React, { useState, useEffect } from 'react';

/**
 * InfoPage component:
 * - Prompts the user for an Info ID.
 * - Connects to the backend SSE endpoint to receive real-time messages.
 * - Contains detailed logs for every major step.
 */
const InfoPage = () => {
  const [messages, setMessages] = useState([]);
  const [infoId, setInfoId] = useState('');
  const [isConnected, setIsConnected] = useState(false);

  useEffect(() => {
    if (!infoId) {
      console.log("[InfoPage] No Info ID provided yet.");
      return;
    }

    let retryTimeout = 1000; // Initial retry delay in milliseconds
    let eventSource;

    const connectToSSE = () => {
      console.log(`[InfoPage] Starting SSE connection for Info ID: ${infoId}`);
      eventSource = new EventSource(`http://localhost:5262/api/info/stream?id=${infoId}`);

      eventSource.onopen = () => {
        console.log("[InfoPage] SSE connection established.");
        setIsConnected(true);
        retryTimeout = 1000; // Reset retry delay on successful connection
      };

      eventSource.onmessage = (e) => {
        console.log("[InfoPage] Message received:", e.data);
        setMessages((prev) => [...prev, e.data]);
      };

      eventSource.onerror = (err) => {
        console.error("[InfoPage] Error in SSE connection:", err);
        setIsConnected(false);
        eventSource.close();

        // Retry connection with exponential backoff
        console.log(`[InfoPage] Retrying connection in ${retryTimeout / 1000} seconds...`);
        setTimeout(() => {
          retryTimeout = Math.min(retryTimeout * 2, 30000); // Cap retry delay at 30 seconds
          connectToSSE();
        }, retryTimeout);
      };
    };

    connectToSSE();

    return () => {
      console.log("[InfoPage] Closing SSE connection.");
      if (eventSource) {
        eventSource.close();
      }
    };
  }, [infoId]);

  const handleIdSubmit = (e) => {
    e.preventDefault();
    const id = e.target.elements.infoId.value.trim();
    if (id) {
      console.log(`[InfoPage] Info ID set to: ${id}`);
      setInfoId(id);
    } else {
      console.warn("[InfoPage] Empty Info ID entered.");
    }
  };

  return (
    <div>
      {!infoId ? (
        <form onSubmit={handleIdSubmit}>
          <label>
            Enter Info Page ID:
            <input type="text" name="infoId" required />
          </label>
          <button type="submit">Connect</button>
        </form>
      ) : (
        <div>
          <h2>Messages for Info ID: {infoId}</h2>
          {isConnected ? (
            <p>SSE connection active.</p>
          ) : (
            <p>Not connected.</p>
          )}
          <ul>
            {messages.map((msg, index) => (
              <li key={index}>{msg}</li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
};

export default InfoPage;
