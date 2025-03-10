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

    console.log(`[InfoPage] Starting SSE connection for Info ID: ${infoId}`);
    const eventSource = new EventSource(`http://localhost:5262/api/info/stream?id=${infoId}`);

    eventSource.onopen = () => {
      console.log("[InfoPage] SSE connection established.");
      setIsConnected(true);
    };

    eventSource.onmessage = (e) => {
      console.log("[InfoPage] Message received:", e.data);
      setMessages((prev) => [...prev, e.data]);
    };

    eventSource.onerror = (err) => {
      console.error("[InfoPage] Error in SSE connection:", err);
      eventSource.close();
      setIsConnected(false);
    };

    return () => {
      console.log("[InfoPage] Closing SSE connection.");
      eventSource.close();
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
