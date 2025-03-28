import React, { useState } from 'react';
import axios from 'axios';

/**
 * Toolbar component:
 * - Allows the user to input a Toolbar ID and message text.
 * - Sends the message via a REST API endpoint to the backend.
 * - Contains detailed logs and error handling.
 */
const Toolbar = () => {
  const [toolbarId, setToolbarId] = useState('');
  const [messageText, setMessageText] = useState('');
  const [status, setStatus] = useState('');
  const [broadcast, setBroadcast] = useState(false); // new state for broadcast

  const sendMessage = async (e) => {
    e.preventDefault();
    console.log(`[Toolbar] Attempting to send message with toolbarId: ${toolbarId}, message: ${messageText}, broadcast: ${broadcast}`);

    if (!messageText.trim()) {
      console.warn("[Toolbar] Message text is empty.");
      setStatus("Message text cannot be empty.");
      return;
    }

    try {
      const payload = {
        id: broadcast ? "broadcast" : (toolbarId || 'default'),
        text: messageText,
      };
      const response = await axios.post('http://localhost:5262/api/toolbar/send', payload);
      console.log("[Toolbar] Message sent successfully. Server responded:", response.data);
      setStatus(response.data);
    } catch (error) {
      console.error("[Toolbar] Error sending message:", error);
      setStatus("Error sending the message.");
    }
  };

  return (
    <div>
      <form onSubmit={sendMessage}>
        <div>
          <label>Toolbar ID:</label>
          <input
            type="text"
            value={toolbarId}
            onChange={(e) => {
              console.log("[Toolbar] Toolbar ID changed to:", e.target.value);
              setToolbarId(e.target.value);
            }}
            placeholder="Enter toolbar ID"
            disabled={broadcast} // optionally disable when broadcasting
          />
        </div>
        <div>
          <label>Message:</label>
          <input
            type="text"
            value={messageText}
            onChange={(e) => {
              console.log("[Toolbar] Message text changed to:", e.target.value);
              setMessageText(e.target.value);
            }}
            placeholder="Type your message"
          />
        </div>
        <div>
          <label>
            <input
              type="checkbox"
              checked={broadcast}
              onChange={(e) => {
                console.log("[Toolbar] Broadcast checkbox changed to:", e.target.checked);
                setBroadcast(e.target.checked);
              }}
            />
            Send as broadcast
          </label>
        </div>
        <button type="submit">Send</button>
      </form>
      {status && <p>Status: {status}</p>}
    </div>
  );
};

export default Toolbar;
