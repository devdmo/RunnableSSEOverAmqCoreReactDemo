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

  const sendMessage = async (e) => {
    e.preventDefault();
    console.log(\`[Toolbar] Attempting to send message with toolbarId: \${toolbarId}, message: \${messageText}\`);

    if (!messageText.trim()) {
      console.warn("[Toolbar] Message text is empty.");
      setStatus("Message text cannot be empty.");
      return;
    }

    try {
      const response = await axios.post('http://localhost:5000/api/toolbar/send', {
        id: toolbarId || 'default',
        text: messageText,
      });
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
        <button type="submit">Send</button>
      </form>
      {status && <p>Status: {status}</p>}
    </div>
  );
};

export default Toolbar;
