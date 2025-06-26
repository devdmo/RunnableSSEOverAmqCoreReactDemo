import React, { useState, useEffect } from 'react';

/**
 * InfoPage component:
 * - Prompts the user for an Info ID and optional Broadcast Group.
 * - Connects to the backend SSE endpoint to receive real-time messages.
 * - Contains detailed logs for every major step.
 */

// Centralized configuration for SSE connection
const SSE_CONFIG = {
  initialRetryTimeout: 1000, // Initial retry delay in milliseconds
  maxRetryTimeout: 30000,   // Maximum retry delay in milliseconds
};

const InfoPage = () => {
  const [messages, setMessages] = useState([]);
  const [infoId, setInfoId] = useState('');
  const [broadcastGroup, setBroadcastGroup] = useState('');
  const [isConnected, setIsConnected] = useState(false);

  useEffect(() => {
    if (!infoId) {
      console.log("[InfoPage] No Info ID provided yet.");
      return;
    }

    let retryTimeout = SSE_CONFIG.initialRetryTimeout;
    let eventSource;

    const connectToSSE = () => {
      // Build the URL with both infoId and optional broadcastGroup
      let url = `http://localhost:5262/api/info/stream?id=${infoId}`;
      if (broadcastGroup && broadcastGroup.trim() !== '') {
        url += `&broadcastGroup=${encodeURIComponent(broadcastGroup)}`;
      }

      console.log(`[InfoPage] Starting SSE connection for Info ID: ${infoId}, Broadcast Group: ${broadcastGroup || 'none'}`);
      eventSource = new EventSource(url);

      eventSource.onopen = () => {
        console.log("[InfoPage] SSE connection established.");
        setIsConnected(true);
        retryTimeout = SSE_CONFIG.initialRetryTimeout; // Reset retry delay on successful connection
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
          retryTimeout = Math.min(retryTimeout * 2, SSE_CONFIG.maxRetryTimeout);
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
  }, [infoId, broadcastGroup]);

  const handleIdSubmit = (e) => {
    e.preventDefault();
    const id = e.target.elements.infoId.value.trim();
    const group = e.target.elements.broadcastGroup.value.trim();

    if (id) {
      console.log(`[InfoPage] Info ID set to: ${id}, Broadcast Group: ${group || 'none'}`);
      setInfoId(id);
      setBroadcastGroup(group);
    } else {
      console.warn("[InfoPage] Empty Info ID entered.");
    }
  };

  return (
    <div>
      {!infoId ? (
        <form onSubmit={handleIdSubmit}>
          <div>
            <label>
              Enter Info Page ID:
              <input type="text" name="infoId" required />
            </label>
          </div>
          <div>
            <label>
              Broadcast Group (optional):
              <input type="text" name="broadcastGroup" />
            </label>
          </div>
          <button type="submit">Connect</button>
        </form>
      ) : (
        <div>
          <h2>Messages for Info ID: {infoId}</h2>
          {broadcastGroup && <h3>Broadcast Group: {broadcastGroup}</h3>}
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
